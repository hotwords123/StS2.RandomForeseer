using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(NTopBar))]
internal static class NextActPredictionTopBarPatches
{
    [HarmonyPatch(nameof(NTopBar.Initialize))]
    [HarmonyPostfix]
    private static void Initialize(NTopBar __instance)
    {
        try
        {
            NextActPrediction.Initialize(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Next-Act prediction failed to initialize: {ex}");
            ModTelemetry.CaptureException(ex, "next_act_prediction", "initialize");
        }
    }
}

[HarmonyPatch(typeof(NRewardsScreen))]
internal static class NextActPredictionRewardsScreenPatches
{
    [HarmonyPatch(nameof(NRewardsScreen.ShowScreen))]
    [HarmonyPostfix]
    private static void ShowPrediction(bool isTerminal, IRunState runState)
    {
        try
        {
            NextActPrediction.ShowIfEligible(isTerminal, runState);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Next-Act prediction failed on the rewards screen: {ex}");
            ModTelemetry.CaptureException(ex, "next_act_prediction", "show_on_rewards_screen");
        }
    }
}
