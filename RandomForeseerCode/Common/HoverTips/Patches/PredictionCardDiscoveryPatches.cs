using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Saves;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips.Patches;

[HarmonyPatch(typeof(NHoverTipCardContainer), nameof(NHoverTipCardContainer.Add))]
internal static class PredictionCardDiscoveryScopePatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Enter(CardHoverTip cardTip, out IDisposable? __state)
    {
        // This must precede the bundle prefix, which creates its NCard nodes before skipping vanilla Add.
        __state = PredictionCardDiscoveryScope.Enter(cardTip);
    }

    [HarmonyFinalizer]
    private static void Exit(IDisposable? __state)
    {
        __state?.Dispose();
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.MarkCardAsSeen))]
internal static class PredictionCardDiscoverySavePatch
{
    [HarmonyPrefix]
    private static bool SkipPredictionCard(CardModel card)
    {
        return !PredictionCardDiscoveryScope.Contains(card);
    }
}
