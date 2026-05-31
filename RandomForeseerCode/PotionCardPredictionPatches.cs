using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer;

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.HoverTips), MethodType.Getter)]
internal static class PotionCardPredictionHoverTipsPatch
{
    private static void Postfix(PotionModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        IReadOnlyList<IHoverTip> predictionTips;
        try
        {
            predictionTips = PotionCardPrediction.GetHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion card prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}

[HarmonyPatch(typeof(NHoverTipCardContainer))]
internal static class PotionPredictionCardHoverTipLayoutPatch
{
    private static readonly StringName PredictionCardMetaKey = "RandomForeseerPotionPredictionCard";
    private const float Padding = 4f;

    [HarmonyPatch(nameof(NHoverTipCardContainer.Add))]
    [HarmonyPostfix]
    private static void MarkPredictionCardTip(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PotionPredictionCardHoverTip)
        {
            return;
        }

        var control = __instance.GetChildren().OfType<Control>().LastOrDefault();
        control?.SetMeta(PredictionCardMetaKey, Variant.From(true));
    }

    [HarmonyPatch(nameof(NHoverTipCardContainer.LayoutResizeAndReposition))]
    [HarmonyPrefix]
    private static bool LayoutPredictionCardTipsHorizontally(
        NHoverTipCardContainer __instance,
        Vector2 globalStartLocation,
        HoverTipAlignment alignment)
    {
        var tips = __instance.GetChildren().OfType<Control>().ToList();
        if (!tips.Any(tip => tip.HasMeta(PredictionCardMetaKey)))
        {
            return true;
        }

        var size = Vector2.Zero;
        var nextPosition = Vector2.Zero;
        foreach (var tip in tips)
        {
            tip.Position = nextPosition;
            size = new Vector2(
                Mathf.Max(nextPosition.X + tip.Size.X, size.X),
                Mathf.Max(tip.Size.Y, size.Y));
            nextPosition += Vector2.Right * (tip.Size.X + Padding);
        }

        __instance.Size = size;
        __instance.GlobalPosition = alignment switch
        {
            HoverTipAlignment.Left => globalStartLocation + Vector2.Left * size.X,
            _ => globalStartLocation
        };

        return false;
    }
}
