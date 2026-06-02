using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.Common;

[HarmonyPatch(typeof(NHoverTipSet), "Init")]
internal static class PredictionTextHoverTipBackgroundPatch
{
    private static readonly ConditionalWeakTable<NHoverTipSet, PredictionTextTipMaskBox> PredictionTextTipMasks = [];

    private static readonly Lazy<ShaderMaterial> PredictionBackgroundMaterial = new(CreatePredictionBackgroundMaterial);

    private static void Prefix(NHoverTipSet __instance, ref IEnumerable<IHoverTip> hoverTips)
    {
        var materializedTips = hoverTips.ToList();
        hoverTips = materializedTips;

        var predictionTextTipMask = IHoverTip.RemoveDupes(materializedTips)
            .Where(tip => tip is HoverTip)
            .Select(PredictionHoverTips.IsPredictionTextHoverTip)
            .ToList();

        if (!predictionTextTipMask.Any(isPrediction => isPrediction))
        {
            return;
        }

        PredictionTextTipMasks.Remove(__instance);
        PredictionTextTipMasks.Add(__instance, new PredictionTextTipMaskBox(predictionTextTipMask));
    }

    private static void Postfix(NHoverTipSet __instance)
    {
        if (!PredictionTextTipMasks.TryGetValue(__instance, out var box))
        {
            return;
        }

        PredictionTextTipMasks.Remove(__instance);

        var textTips = __instance._textHoverTipContainer
            .GetChildren()
            .OfType<Control>()
            .ToList();

        for (var i = 0; i < Math.Min(textTips.Count, box.Mask.Count); i++)
        {
            if (!box.Mask[i])
            {
                continue;
            }

            var background = textTips[i].GetNode<CanvasItem>("%Bg");
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

    private sealed class PredictionTextTipMaskBox(IReadOnlyList<bool> mask)
    {
        public IReadOnlyList<bool> Mask { get; } = mask;
    }
}
