using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Patches;

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseUnsynchronized))]
internal static class BlitzMerchantPurchasePredictionPatch
{
    [HarmonyPrefix]
    private static void Prefix(MerchantEntry __instance, out IDisposable? __state)
    {
        // Both local and remote purchases reach this boundary before consuming RNG or opening selectors.
        __state = __instance is BlitzMerchantEntry
            ? BlitzMerchantPurchasePredictionState.Enter(__instance.Player)
            : null;
    }

    [HarmonyPostfix]
    private static void Postfix(ref Task<bool> __result, ref IDisposable? __state)
    {
        if (__state is not null)
        {
            __result = __result.WithFinally(__state.Dispose);
            __state = null;
        }
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable? __state)
    {
        // Only handles synchronous failures; the returned Task owns cleanup after the Postfix hands it off.
        __state?.Dispose();
    }
}
