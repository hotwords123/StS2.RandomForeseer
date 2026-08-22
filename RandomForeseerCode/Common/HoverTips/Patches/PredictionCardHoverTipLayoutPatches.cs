using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips.Patches;

/// <summary>
/// Bridges vanilla card-container creation and layout to prediction-aware controls.
/// </summary>
[HarmonyPatch(typeof(NHoverTipCardContainer))]
internal static class PredictionCardHoverTipContainerPatches
{
    private static readonly Color DimmedCardModulate = new(0.6f, 0.6f, 0.6f);

    /// <summary>
    /// Replaces vanilla's single-card node creation for logical bundles with one custom stack root.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> after a bundle stack has been added so vanilla does not also render its first card;
    /// otherwise <see langword="true"/> to preserve normal card-tip creation.
    /// </returns>
    [HarmonyPatch(nameof(NHoverTipCardContainer.Add))]
    [HarmonyPrefix]
    private static bool AddPredictionCardBundleTip(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PredictionCardBundleHoverTip bundleTip)
        {
            return true;
        }

        var control = PredictionHoverTipControlFactory.CreateAndAddStack(__instance, bundleTip);
        PredictionCardHoverTipLayoutState.MarkPredictionCard(control);
        return false;
    }

    /// <summary>
    /// Marks the control vanilla just created for an individual prediction card and applies optional dimming.
    /// </summary>
    /// <remarks>
    /// Vanilla appends exactly one top-level control during <see cref="NHoverTipCardContainer.Add"/>, so the
    /// container's last child corresponds to
    /// <paramref name="cardTip"/> at this postfix.
    /// </remarks>
    [HarmonyPatch(nameof(NHoverTipCardContainer.Add))]
    [HarmonyPostfix]
    private static void MarkPredictionCardTip(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PredictionCardHoverTip predictionTip)
        {
            return;
        }

        var control = __instance.GetChildren().OfType<Control>().LastOrDefault();
        PredictionCardHoverTipLayoutState.MarkPredictionCard(control);
        if (predictionTip.IsDimmed)
        {
            control?.GetNode<NCard>("%Card").Modulate = DimmedCardModulate;
        }
    }

    /// <summary>
    /// Gives prediction layout the first opportunity to size and position the card container.
    /// </summary>
    /// <remarks>
    /// <see cref="PredictionCardHoverTipLayout.TryLayoutPredictionCardTips"/> returns whether it handled the layout,
    /// while a Harmony prefix returns whether the original should run, so the result must be inverted.
    /// </remarks>
    [HarmonyPatch(nameof(NHoverTipCardContainer.LayoutResizeAndReposition))]
    [HarmonyPrefix]
    private static bool LayoutPredictionCardTips(
        NHoverTipCardContainer __instance,
        Vector2 globalStartLocation,
        HoverTipAlignment alignment)
    {
        var handled = PredictionCardHoverTipLayout.TryLayoutPredictionCardTips(
            __instance,
            globalStartLocation,
            alignment);
        return !handled;
    }
}

/// <summary>
/// Captures precise HoverTip source geometry and applies fallback placement after vanilla alignment.
/// </summary>
[HarmonyPatch(typeof(NHoverTipSet))]
internal static class PredictionCardHoverTipSetAlignmentPatches
{
    /// <summary>
    /// Records the hovered card's exact hitbox before vanilla reduces it to a side anchor for alignment.
    /// </summary>
    /// <remarks>
    /// Card-holder text uses an additional gap, so that spacing is recorded with the source geometry for fallback.
    /// </remarks>
    [HarmonyPatch(nameof(NHoverTipSet.SetAlignmentForCardHolder))]
    [HarmonyPrefix]
    private static void RecordCardHolderSourceRect(NHoverTipSet __instance, NCardHolder holder)
    {
        var container = __instance._cardHoverTipContainer;
        if (ShouldRecordSourceRect(container))
        {
            // LayoutResizeAndReposition only receives a side anchor. Record the hovered card rect so fallback
            // placement can center above the card instead of guessing from the left/right edge.
            PredictionCardHoverTipLayoutState.RecordSourceRect(
                container,
                holder.Hitbox.GetGlobalRect(),
                HoverTip.GetHoverTipAlignment(holder),
                PredictionCardHoverTipLayout.CardHolderTextGap);
        }
    }

    /// <summary>
    /// Checks whether vanilla card-holder alignment still requires prediction top/bottom fallback.
    /// </summary>
    [HarmonyPatch(nameof(NHoverTipSet.SetAlignmentForCardHolder))]
    [HarmonyPostfix]
    private static void ApplyCardHolderFallbackLayout(NHoverTipSet __instance)
    {
        PredictionCardHoverTipLayout.ApplyFallbackLayoutIfStillOverflowing(__instance);
    }

    /// <summary>
    /// Records the hovered relic icon bounds before vanilla aligns its complete HoverTip set.
    /// </summary>
    [HarmonyPatch(nameof(NHoverTipSet.SetAlignmentForRelic))]
    [HarmonyPrefix]
    private static void RecordRelicSourceRect(NHoverTipSet __instance, NRelic relic)
    {
        var container = __instance._cardHoverTipContainer;
        if (ShouldRecordSourceRect(container))
        {
            PredictionCardHoverTipLayoutState.RecordSourceRect(
                container,
                relic.Icon.GetGlobalRect(),
                HoverTip.GetHoverTipAlignment(relic));
        }
    }

    /// <summary>
    /// Checks whether vanilla relic alignment still requires prediction top/bottom fallback.
    /// </summary>
    [HarmonyPatch(nameof(NHoverTipSet.SetAlignmentForRelic))]
    [HarmonyPostfix]
    private static void ApplyRelicFallbackLayout(NHoverTipSet __instance)
    {
        PredictionCardHoverTipLayout.ApplyFallbackLayoutIfStillOverflowing(__instance);
    }

    /// <summary>
    /// Records arbitrary hovered-control bounds for callers using the generic alignment entry point.
    /// </summary>
    [HarmonyPatch(nameof(NHoverTipSet.SetAlignment))]
    [HarmonyPrefix]
    private static void RecordControlSourceRect(NHoverTipSet __instance, Control node, HoverTipAlignment alignment)
    {
        var container = __instance._cardHoverTipContainer;
        if (ShouldRecordSourceRect(container))
        {
            PredictionCardHoverTipLayoutState.RecordSourceRect(container, node.GetGlobalRect(), alignment);
        }
    }

    /// <summary>
    /// Checks whether generic vanilla alignment still requires prediction top/bottom fallback.
    /// </summary>
    [HarmonyPatch(nameof(NHoverTipSet.SetAlignment))]
    [HarmonyPostfix]
    private static void ApplyControlFallbackLayout(NHoverTipSet __instance)
    {
        PredictionCardHoverTipLayout.ApplyFallbackLayoutIfStillOverflowing(__instance);
    }

    /// <summary>
    /// Limits source tracking to initialized containers that actually need prediction fallback behavior.
    /// </summary>
    private static bool ShouldRecordSourceRect(NHoverTipCardContainer? container)
    {
        return container != null && PredictionCardHoverTipLayoutState.HasPredictionCard(container);
    }
}
