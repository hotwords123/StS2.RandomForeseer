using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.Common.HoverTips.Patches;

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.Init))]
internal static class PredictionTextHoverTipBackgroundPatch
{
    private static readonly Lazy<ShaderMaterial> PredictionBackgroundMaterial = new(CreatePredictionBackgroundMaterial);

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(ref IEnumerable<IHoverTip> hoverTips, out List<bool>? __state)
    {
        var hoverTipList = hoverTips.Materialize();
        hoverTips = hoverTipList;

        var mask = IHoverTip.RemoveDupes(hoverTipList)
            .Where(static tip => tip is HoverTip)
            .Select(static tip => tip.IsPredictionTextHoverTip())
            .ToList();

        __state = mask.Contains(true) ? mask : null;
    }

    private static void Postfix(NHoverTipSet __instance, List<bool>? __state)
    {
        if (__state is null)
        {
            return;
        }

        var textTips = __instance._textHoverTipContainer.GetChildren().OfType<Control>();
        foreach (var (textTip, isPrediction) in textTips.Zip(__state))
        {
            if (!isPrediction)
            {
                continue;
            }

            var background = textTip.GetNodeOrNull<CanvasItem>("%Bg");
            if (background is null)
            {
                continue;
            }

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
}
