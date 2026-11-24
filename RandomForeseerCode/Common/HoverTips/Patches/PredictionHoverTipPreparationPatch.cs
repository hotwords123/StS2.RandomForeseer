using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.Data;

namespace RandomForeseer.Common.HoverTips.Patches;

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.Init))]
internal static class PredictionHoverTipPreparationPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        var tips = hoverTips.ToList();
        if (owner is NEventOptionButton)
        {
            // Event options may reuse a model's HoverTips, which can already include predictions from global patches.
            // Remove those nested prediction tips here so the option only shows its own event prediction and avoids
            // confusing mixed results.
            tips.RemoveAll(static tip => tip.IsPredictionHoverTip());
        }

        tips.AddRange(ControlHoverTipPredictionRegistry.GetHoverTips(owner));
        PrepareForPresentation(tips);
        hoverTips = ApplyPredictionOrder(tips, ModData.Settings);
    }

    /// <summary>
    /// Stably orders prediction text and card tips around other tips according to their independent settings.
    /// </summary>
    private static List<IHoverTip> ApplyPredictionOrder(List<IHoverTip> tips, ModSettings settings)
    {
        return [.. tips.OrderBy(tip =>
        {
            if (tip.IsPredictionTextHoverTip())
            {
                return settings.PredictionTextHoverTipOrder == PredictionHoverTipOrder.BeforeVanilla ? 0 : 2;
            }

            if (tip.IsPredictionCardHoverTip())
            {
                return settings.PredictionCardHoverTipOrder == PredictionHoverTipOrder.BeforeVanilla ? 0 : 2;
            }

            return 1;
        })];
    }

    /// <summary>
    /// Applies presentation-only bundle decisions to the complete tip set.
    /// </summary>
    /// <remarks>
    /// Exactly one bundle tip is expanded into individual cards only when the complete set has no independent card
    /// tip. A bundle remains a stack when another individual card or another bundle is present. One transform
    /// explanation text tip is added when any transform bundle remains unexpanded. This method must run before
    /// vanilla <see cref="NHoverTipSet.Init"/> removes duplicates and creates controls.
    /// </remarks>
    private static void PrepareForPresentation(List<IHoverTip> tips)
    {
        var bundleTips = tips.OfType<PredictionCardBundleHoverTip>().ToList();

        if (bundleTips is [var bundleTip] &&
            tips.OfType<CardHoverTip>().All(static tip => tip is PredictionCardBundleHoverTip))
        {
            var index = tips.IndexOf(bundleTip);
            tips.RemoveAt(index);
            tips.InsertRange(index, bundleTip.Cards.Select(card =>
                PredictionHoverTipFactory.Card(card, bundleTip.IsDimmed, bundleTip.Source)));
            bundleTips.Clear();
        }

        var transformBundle = bundleTips.Find(static tip => tip.Kind is PredictionCardBundleKind.Transform);
        if (transformBundle is not null)
        {
            var index = tips.IndexOf(transformBundle);
            tips.Insert(index, PredictionHoverTipFactory.Text("transform_bundle_explanation"));
        }
    }
}
