using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace RandomForeseer;

internal static class TransformPreviewPatchShared
{
    // All current targets call the three-argument overload at IL level. When source code omits
    // cardToTransformation, the C# compiler emits an explicit ldnull for the optional argument.
    private static readonly MethodInfo FromDeckForTransformation3 =
        AccessTools.Method(
            typeof(CardSelectCmd),
            nameof(CardSelectCmd.FromDeckForTransformation),
            [typeof(Player), typeof(CardSelectorPrefs), typeof(Func<CardModel, CardTransformation>)])
        ?? throw new MissingMethodException(nameof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForTransformation));

    // Use this from a [HarmonyPatch] TargetMethod() for async transform methods. Harmony must patch
    // the compiler-generated MoveNext body, because the original async method only starts the state machine.
    public static MethodBase TargetMoveNext(MethodInfo? asyncMethod)
    {
        if (asyncMethod == null)
        {
            throw new MissingMethodException("Could not find async patch target.");
        }

        var attribute = asyncMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException($"{asyncMethod.FullDescription()} is not async.");
        }

        return AccessTools.Method(attribute.StateMachineType, "MoveNext")
            ?? throw new MissingMethodException(attribute.StateMachineType.FullName, "MoveNext");
    }

    // Replaces the default preview factory argument:
    //   ldnull
    //   call CardSelectCmd.FromDeckForTransformation(player, prefs, null)
    //
    // with:
    //   ldarg.0
    //   ldfld <>4__this
    //   call MakePredictor(source)
    //   call CardSelectCmd.FromDeckForTransformation(player, prefs, predictor)
    //
    // Each concrete patch supplies a MakePredictor method for the RNG that the actual transform uses.
    public static IEnumerable<CodeInstruction> AddPredictor(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original,
        MethodInfo predictorFactory)
    {
        // Async instance methods store their original "this" in the generated state machine.
        var ownerField = AccessTools.Field(original.DeclaringType, "<>4__this")
            ?? throw new InvalidOperationException($"Could not find async owner field on {original.FullDescription()}.");
        var instructionList = new List<CodeInstruction>(instructions);

        var result = new List<CodeInstruction>(instructionList.Count + 2);
        var replacementCount = 0;

        for (var i = 0; i < instructionList.Count; i++)
        {
            var instruction = instructionList[i];
            if (instruction.opcode == OpCodes.Ldnull
                && i + 1 < instructionList.Count
                && instructionList[i + 1].Calls(FromDeckForTransformation3))
            {
                // Preserve any branch labels/exception blocks that pointed at the removed ldnull.
                var loadThis = new CodeInstruction(OpCodes.Ldarg_0);
                loadThis.labels.AddRange(instruction.labels);
                loadThis.blocks.AddRange(instruction.blocks);

                result.Add(loadThis);
                result.Add(new CodeInstruction(OpCodes.Ldfld, ownerField));
                result.Add(new CodeInstruction(OpCodes.Call, predictorFactory));
                replacementCount++;
            }
            else
            {
                result.Add(instruction);
            }
        }

        if (replacementCount != 1)
        {
            throw new InvalidOperationException(
                $"Expected to patch exactly one transform selector call in {original.FullDescription()}, patched {replacementCount}.");
        }

        return result;
    }
}

[HarmonyPatch(typeof(NDeckTransformSelectScreen), "OpenPreviewScreen")]
internal static class DeckTransformSelectScreenResetPredictionPatch
{
    private static readonly FieldInfo CardToTransformationField =
        AccessTools.Field(typeof(NDeckTransformSelectScreen), "_cardToTransformation")
        ?? throw new MissingFieldException(nameof(NDeckTransformSelectScreen), "_cardToTransformation");

    private static void Prefix(NDeckTransformSelectScreen __instance)
    {
        if (CardToTransformationField.GetValue(__instance) is not Func<CardModel, CardTransformation> predictor)
        {
            return;
        }

        // The same selection screen delegate can be reused after canceling the preview.
        // Resetting here keeps the cloned RNG aligned with the real RNG for each preview opening.
        (predictor.Target as IResettableTransformPreviewPredictor)?.Reset();
    }
}

[HarmonyPatch]
internal static class AstrolabeTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(Astrolabe), nameof(Astrolabe.AfterObtained)));

    // Patch the omitted optional preview factory argument and provide a predictor using Astrolabe's
    // actual transform RNG. Astrolabe's predictor upgrades the preview card to match the real result.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(AstrolabeTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(Astrolabe source) =>
        TransformPreviewPredictor.Make(source.Owner.RunState.Rng.Niche, upgradePreview: true);
}

[HarmonyPatch]
internal static class NewLeafTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(NewLeaf), nameof(NewLeaf.AfterObtained)));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(NewLeafTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(NewLeaf source) =>
        TransformPreviewPredictor.Make(source.Owner.RunState.Rng.Niche);
}

[HarmonyPatch]
internal static class AromaOfChaosTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(AromaOfChaos), "LetGo"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(AromaOfChaosTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(AromaOfChaos source) =>
        TransformPreviewPredictor.Make(source.Rng);
}

[HarmonyPatch]
internal static class EndlessConveyorTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(EndlessConveyor), "JellyLiver"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(EndlessConveyorTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(EndlessConveyor source) =>
        TransformPreviewPredictor.Make(source.Rng);
}

[HarmonyPatch]
internal static class MorphicGroveTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(MorphicGrove), "Group"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(MorphicGroveTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(MorphicGrove source) =>
        TransformPreviewPredictor.Make(source.Owner!.RunState.Rng.Niche);
}

[HarmonyPatch]
internal static class SymbioteTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(Symbiote), "KillWithFire"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(SymbioteTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(Symbiote source) =>
        TransformPreviewPredictor.Make(source.Rng);
}

[HarmonyPatch]
internal static class TrialTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(Trial), "NondescriptInnocent"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(TrialTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(Trial source) =>
        TransformPreviewPredictor.Make(source.Owner!.RunState.Rng.Niche);
}

[HarmonyPatch]
internal static class WhisperingHollowTransformPreviewPatch
{
    private static MethodBase TargetMethod() =>
        TransformPreviewPatchShared.TargetMoveNext(AccessTools.Method(typeof(WhisperingHollow), "Hug"));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TransformPreviewPatchShared.AddPredictor(
            instructions,
            original,
            AccessTools.Method(typeof(WhisperingHollowTransformPreviewPatch), nameof(MakePredictor)));

    private static Func<CardModel, CardTransformation>? MakePredictor(WhisperingHollow source) =>
        TransformPreviewPredictor.Make(source.Rng);
}
