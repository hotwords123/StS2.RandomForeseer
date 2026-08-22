using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips.Patches;

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.Init))]
internal static class PredictionHoverTipInitPatch
{
    private static readonly ConditionalWeakTable<NHoverTipSet, List<bool>> PredictionTextTipMasks = [];

    private static readonly Lazy<ShaderMaterial> PredictionBackgroundMaterial = new(CreatePredictionBackgroundMaterial);

    private static void Prefix(NHoverTipSet __instance, ref IEnumerable<IHoverTip> hoverTips)
    {
        var preparedHoverTips = PrepareForPresentation(hoverTips);
        hoverTips = preparedHoverTips;

        var predictionTextTipMask = IHoverTip.RemoveDupes(preparedHoverTips)
            .Where(static tip => tip is HoverTip)
            .Select(static tip => tip.IsPredictionTextHoverTip())
            .ToList();

        if (!predictionTextTipMask.Contains(true))
        {
            return;
        }

        PredictionTextTipMasks.AddOrUpdate(__instance, predictionTextTipMask);
    }

    private static void Postfix(NHoverTipSet __instance)
    {
        if (!PredictionTextTipMasks.TryGetValue(__instance, out var mask))
        {
            return;
        }

        PredictionTextTipMasks.Remove(__instance);

        var textTips = __instance._textHoverTipContainer
            .GetChildren()
            .OfType<Control>()
            .Zip(mask)
            .Where(static pair => pair.Second)
            .Select(static pair => pair.First);

        foreach (var textTip in textTips)
        {
            var background = textTip.GetNode<CanvasItem>("%Bg");
            background.Material = PredictionBackgroundMaterial.Value;
            background.SelfModulate = Colors.White;
        }
    }

    private static ShaderMaterial CreatePredictionBackgroundMaterial()
    {
        var material = new ShaderMaterial
        {
            Shader = ResourceLoader.Load<Shader>("res://shaders/hsv.gdshader")
        };
        material.SetShaderParameter("h", 0.52f);
        material.SetShaderParameter("s", 1.75f);
        material.SetShaderParameter("v", 1.15f);
        return material;
    }

    /// <summary>
    /// Materializes the complete tip set and applies presentation-only bundle decisions.
    /// </summary>
    /// <remarks>
    /// Exactly one bundle tip is expanded into individual cards only when the complete set has no independent card
    /// tip. A bundle remains a stack when another individual card or another bundle is present. One transform
    /// explanation text tip is added when any transform bundle remains unexpanded. This method must run before
    /// vanilla <see cref="NHoverTipSet.Init"/> removes duplicates and creates controls.
    /// </remarks>
    private static List<IHoverTip> PrepareForPresentation(IEnumerable<IHoverTip> hoverTips)
    {
        var tips = hoverTips.ToList();
        var bundleTips = tips.OfType<PredictionCardBundleHoverTip>().ToList();

        if (bundleTips is [var bundleTip] &&
            tips.OfType<CardHoverTip>().All(static tip => tip is PredictionCardBundleHoverTip))
        {
            var index = tips.IndexOf(bundleTip);
            tips.RemoveAt(index);
            tips.InsertRange(index, bundleTip.Cards.ToPredictionHoverTips());
            bundleTips.Clear();
        }

        var transformBundle = bundleTips.Find(static tip => tip.Kind is PredictionCardBundleKind.Transform);
        if (transformBundle is not null)
        {
            var index = tips.IndexOf(transformBundle);
            tips.Insert(index, PredictionHoverTipFactory.Text("transform_bundle_explanation"));
        }

        return tips;
    }
}
