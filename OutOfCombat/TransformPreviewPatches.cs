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
using STS2RitsuLib.Utils.HarmonyIl;

namespace RandomForeseer.OutOfCombat;

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

        var attribute = asyncMethod.GetCustomAttribute<AsyncStateMachineAttribute>()
            ?? throw new InvalidOperationException($"{asyncMethod.FullDescription()} is not async.");

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
        var instructionList = instructions.ToList();

        try
        {
            // Async instance methods store their original "this" in the generated state machine.
            var ownerField = AccessTools.Field(original.DeclaringType, "<>4__this")
                ?? throw new InvalidOperationException(
                    $"Could not find async owner field on {original.FullDescription()}.");

            var rewriter = HarmonyIlRewriter.From(instructionList, original);
            var pattern = HarmonyIlPattern.Sequence(
                instruction => instruction.opcode == OpCodes.Ldnull,
                HarmonyIl.IsCall(FromDeckForTransformation3));
            var callWithNull = rewriter
                .FindMatches(pattern, $"transform selector call in {original.FullDescription()}")
                .RequireSingle();

            rewriter.Replace(
                // Replace only the optional-argument ldnull; keep the following selector call intact.
                new HarmonyIlMatch(callWithNull.Index, 1),
                [
                    HarmonyIl.Ldarg(0),
                    HarmonyIl.Ldfld(ownerField),
                    HarmonyIl.Call(predictorFactory),
                ]);

            return rewriter.InstructionsChecked("transform selector preview factory argument");
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Transform preview transpiler failed for {original.FullDescription()}: {ex}");
            return instructionList;
        }
    }
}

[HarmonyPatch(typeof(NDeckTransformSelectScreen), "OpenPreviewScreen")]
internal static class DeckTransformSelectScreenResetPredictionPatch
{
    private static void Prefix(NDeckTransformSelectScreen __instance)
    {
        if (__instance._cardToTransformation is not Func<CardModel, CardTransformation> predictor)
        {
            return;
        }

        // The same selection screen delegate can be reused after canceling the preview.
        // Resetting here keeps the cloned RNG aligned with the real RNG for each preview opening.
        (predictor.Target as TransformPreviewPredictor)?.Reset();
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
        TransformPreviewPredictor.Make(
            source.Owner.RunState.Rng.Niche,
            upgradePreview: true,
            PredictionFairness.UnfairInSingleplayer);
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
        TransformPreviewPredictor.Make(
            source.Owner.RunState.Rng.Niche,
            fairness: PredictionFairness.UnfairInSingleplayer);
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
        TransformPreviewPredictor.Make(source.Rng);
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
        TransformPreviewPredictor.Make(source.Rng);
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
