using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Telemetry;
using STS2RitsuLib.Utils.HarmonyIl;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

using CardOnPlayAction = Action<CardModel, CardOnPlayMirrorContext>;

/// <summary>
/// Infers simple, directly invoked vanilla command templates from an unregistered <see cref="CardModel.OnPlay" />.
/// </summary>
internal static class CardOnPlayInferrer
{
    public static CardOnPlayAction? Infer(Type runtimeType, MethodInfo overrideMethod)
    {
        if (!CompatibilityUtils.IsPredictionAllowedForType(runtimeType))
        {
            return null;
        }

        HarmonyIlMethodBody body;
        try
        {
            body = overrideMethod.GetOriginalIl();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn(
                $"Could not inspect original OnPlay IL for inferred card mirror {runtimeType.FullName}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "card_on_play_inferrer",
                "read_original_il",
                TelemetryContext.ForModel(runtimeType));
            return null;
        }

        HashSet<EffectKind> effects = [];
        List<CardOnPlayAction> actions = [];

        for (var i = 0; i < body.Instructions.Count; i++)
        {
            if (!HarmonyIl.TryGetCalledMethod(body.Instructions[i], out var calledMethod))
            {
                continue;
            }

            if (IsAttackExecution(calledMethod))
            {
                if (effects.Add(EffectKind.Attack))
                {
                    actions.Add(GeneralCardMirrors.GeneralAttackOnPlay);
                }
            }
            else if (IsBlockGain(calledMethod))
            {
                if (effects.Add(EffectKind.Block))
                {
                    actions.Add(GeneralCardMirrors.GeneralBlockOnPlay);
                }
            }
            else if (TryInferOwnerDraw(body.Instructions, i, calledMethod, out var mirror))
            {
                if (effects.Add(EffectKind.OwnerDraw))
                {
                    actions.Add(mirror);
                }
            }
            else if (IsVulnerableApplication(body.Instructions, i, calledMethod))
            {
                if (effects.Add(EffectKind.VulnerableApplication))
                {
                    actions.Add(GeneralCardMirrors.GeneralVulnerableApplicationOnPlay);
                }
            }
        }

        if (actions.Count == 0)
        {
            return null;
        }

        return (card, context) =>
        {
            try
            {
                foreach (var action in actions)
                {
                    action(card, context);
                }
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Inferred card mirror failed for {card.Id}: {ex}");
                ModTelemetry.CaptureException(
                    ex,
                    "card_on_play_inferrer",
                    "execute_inferred_mirror",
                    TelemetryContext.ForModel(card));
            }
        };
    }

    private static bool IsVulnerableApplication(
        IReadOnlyList<CodeInstruction> instructions,
        int callIndex,
        MethodInfo method)
    {
        return method.DeclaringType == typeof(PowerCmd) &&
               method.Name == nameof(PowerCmd.Apply) &&
               method.IsGenericMethod &&
               method.GetGenericArguments() is [var powerType] &&
               powerType == typeof(VulnerablePower) &&
               !IsConditionallyGuarded(instructions, callIndex);
    }

    private static bool TryInferOwnerDraw(
        IReadOnlyList<CodeInstruction> instructions,
        int callIndex,
        MethodInfo method,
        [NotNullWhen(true)] out CardOnPlayAction? action)
    {
        action = null;
        if (method.DeclaringType != typeof(CardPileCmd) || method.Name != nameof(CardPileCmd.Draw) ||
            IsConditionallyGuarded(instructions, callIndex))
        {
            return false;
        }

        var parameters = method.GetParameters();
        // Typical two-argument form (receiver/context loads omitted):
        //   ... load card
        //   callvirt CardModel.get_Owner  // callIndex - 1
        //   call CardPileCmd.Draw         // callIndex
        // The overload itself fixes the count at one, so only the player-producing call needs a positional check.
        if (parameters.Length == 2 &&
            parameters[0].ParameterType == typeof(PlayerChoiceContext) &&
            IsCardModelOwnerGetter(instructions, callIndex - 1))
        {
            action = GeneralCardMirrors.GeneralOwnerDrawOneOnPlay;
            return true;
        }

        // Typical four-argument tail after choiceContext and count have been pushed:
        //   ... count recipe ends here     // callIndex - 4
        //   load card                      // callIndex - 3
        //   callvirt CardModel.get_Owner   // callIndex - 2
        //   ldc.i4.0                       // callIndex - 1: fromHandDraw = false
        //   call CardPileCmd.Draw          // callIndex
        // The card load can vary, so the matcher anchors on the two stable instructions immediately before Draw.
        if (parameters.Length != 4 ||
            parameters[0].ParameterType != typeof(PlayerChoiceContext) ||
            parameters[1].ParameterType != typeof(decimal) ||
            !IsCardModelOwnerGetter(instructions, callIndex - 2) ||
            !LoadsFalse(instructions, callIndex - 1))
        {
            return false;
        }

        // With the stable four-argument tail above, the instruction at -4 is also the end of every supported count
        // recipe: decimal.One, Cards.BaseValue, int-to-decimal Cards.IntValue, or the stored async variant.
        if (LoadsDecimalOne(instructions, callIndex - 4))
        {
            action = GeneralCardMirrors.GeneralOwnerDrawOneOnPlay;
            return true;
        }

        if (LoadsCardsValue(instructions, callIndex - 4) ||
            LoadsStoredCardsValue(instructions, callIndex - 4))
        {
            action = GeneralCardMirrors.GeneralOwnerDrawOnPlay;
            return true;
        }

        return false;
    }

    private static bool IsConditionallyGuarded(IReadOnlyList<CodeInstruction> instructions, int callIndex)
    {
        // A directly guarded draw usually starts its argument preparation like this:
        //   ... load condition
        //   brfalse label                   // contextLoadIndex - 2
        //   ldarg.0                         // contextLoadIndex - 1: async state machine
        //   ldfld PlayerChoiceContext       // contextLoadIndex
        //   ... remaining Draw arguments
        //   call CardPileCmd.Draw           // callIndex
        // We search backward from Draw for the context field because the count/player recipes have different lengths.
        var contextLoadIndex = -1;
        for (var i = callIndex - 1; i >= Math.Max(0, callIndex - 16); i--)
        {
            if (instructions[i].operand is FieldInfo { FieldType: var fieldType } &&
                fieldType == typeof(PlayerChoiceContext))
            {
                contextLoadIndex = i;
                break;
            }
        }

        var branchIndex = contextLoadIndex - 2;
        if (branchIndex < 0 || instructions[branchIndex].opcode.FlowControl != FlowControl.Cond_Branch)
        {
            return false;
        }

        // The initial async state dispatch also branches around the first await. It is compiler control flow,
        // not a gameplay condition, and always reads the state stored in local 0 near the start of MoveNext.
        // Typical prefix:
        //   ldloc.0
        //   brfalse ...                     // branchIndex
        // Treat this early local-0 branch as the state-machine switch rather than a conditional card effect.
        if (branchIndex < 14)
        {
            for (var i = Math.Max(0, branchIndex - 2); i < branchIndex; i++)
            {
                if (HarmonyIl.TryGetLocalLoadIndex(instructions[i], out var localIndex) && localIndex == 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsCardModelOwnerGetter(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        return index >= 0 &&
            HarmonyIl.TryGetCalledMethod(instructions[index], out var method) &&
            method.DeclaringType == typeof(CardModel) &&
            method.Name == $"get_{nameof(CardModel.Owner)}";
    }

    private static bool LoadsFalse(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        return index >= 0 && HarmonyIl.LoadsInt32(instructions[index], 0);
    }

    private static bool LoadsDecimalOne(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        return index >= 0 && instructions[index].opcode == OpCodes.Ldsfld &&
            instructions[index].operand is FieldInfo { DeclaringType: var declaringType, Name: nameof(decimal.One) } &&
            declaringType == typeof(decimal);
    }

    private static bool LoadsCardsValue(IReadOnlyList<CodeInstruction> instructions, int valueEndIndex)
    {
        // Direct decimal recipe:
        //   call DynamicVarSet.get_Cards    // valueEndIndex - 1
        //   call DynamicVar.get_BaseValue   // valueEndIndex
        if (IsDynamicVarGetter(instructions, valueEndIndex, nameof(DynamicVar.BaseValue)))
        {
            return IsCardsGetter(instructions, valueEndIndex - 1);
        }

        // Direct integer recipe, converted to CardPileCmd.Draw's decimal count:
        //   call DynamicVarSet.get_Cards    // valueEndIndex - 2
        //   call DynamicVar.get_IntValue    // valueEndIndex - 1
        //   call decimal.op_Implicit(int)   // valueEndIndex
        return IsDecimalFromInt(instructions, valueEndIndex) &&
            IsDynamicVarGetter(instructions, valueEndIndex - 1, nameof(DynamicVar.IntValue)) &&
            IsCardsGetter(instructions, valueEndIndex - 2);
    }

    private static bool LoadsStoredCardsValue(IReadOnlyList<CodeInstruction> instructions, int valueEndIndex)
    {
        // Some async methods, such as Prepared.OnPlay, preserve the count in a generated state-machine field:
        //   call DynamicVarSet.get_Cards
        //   call DynamicVar.get_IntValue
        //   stfld int32 <...>count          // searched assignment
        //   ...
        //   ldfld int32 <...>count          // valueEndIndex - 1
        //   call decimal.op_Implicit(int)   // valueEndIndex
        // Match the same FieldInfo at both ends, then verify that the nearby assignment came from Cards.IntValue.
        if (!IsDecimalFromInt(instructions, valueEndIndex) || valueEndIndex < 1 ||
            instructions[valueEndIndex - 1].operand is not FieldInfo countField)
        {
            return false;
        }

        for (var i = valueEndIndex - 2; i >= Math.Max(2, valueEndIndex - 32); i--)
        {
            if (instructions[i].opcode == OpCodes.Stfld && Equals(instructions[i].operand, countField))
            {
                return IsDynamicVarGetter(instructions, i - 1, nameof(DynamicVar.IntValue)) &&
                    IsCardsGetter(instructions, i - 2);
            }
        }

        return false;
    }

    private static bool IsCardsGetter(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        return index >= 0 &&
            HarmonyIl.TryGetCalledMethod(instructions[index], out var method) &&
            method.DeclaringType == typeof(DynamicVarSet) &&
            method.Name == $"get_{nameof(DynamicVarSet.Cards)}";
    }

    private static bool IsDynamicVarGetter(
        IReadOnlyList<CodeInstruction> instructions,
        int index,
        string propertyName)
    {
        return index >= 0 &&
            HarmonyIl.TryGetCalledMethod(instructions[index], out var method) &&
            typeof(DynamicVar).IsAssignableFrom(method.DeclaringType) &&
            method.Name == $"get_{propertyName}";
    }

    private static bool IsDecimalFromInt(IReadOnlyList<CodeInstruction> instructions, int index)
    {
        return index >= 0 &&
            HarmonyIl.TryGetCalledMethod(instructions[index], out var method) &&
            method.DeclaringType == typeof(decimal) &&
            method.Name == "op_Implicit" &&
            method.GetParameters() is [var param] &&
            param.ParameterType == typeof(int);
    }

    private static bool IsAttackExecution(MethodInfo method)
    {
        return method.DeclaringType == typeof(AttackCommand) && method.Name == nameof(AttackCommand.Execute);
    }

    private static bool IsBlockGain(MethodInfo method)
    {
        return method.DeclaringType == typeof(CreatureCmd) && method.Name == nameof(CreatureCmd.GainBlock);
    }

    private enum EffectKind
    {
        Attack,
        Block,
        OwnerDraw,
        VulnerableApplication
    }
}
