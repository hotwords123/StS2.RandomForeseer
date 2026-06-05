using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch]
internal static class TreasureRoomRelicPredictionPatch
{
    private static readonly ConditionalWeakTable<NTreasureRoomRelicHolder, RunStateBox> RunStates = [];

    [HarmonyPatch(typeof(NTreasureRoomRelicHolder), nameof(NTreasureRoomRelicHolder.Initialize))]
    [HarmonyPostfix]
    private static void InitializePostfix(NTreasureRoomRelicHolder __instance, IRunState runState)
    {
        RunStates.Remove(__instance);
        RunStates.Add(__instance, new RunStateBox(runState));
    }

    [HarmonyPatch(typeof(NTreasureRoomRelicHolder), "OnFocus")]
    [HarmonyPostfix]
    private static void OnFocusPostfix(NTreasureRoomRelicHolder __instance)
    {
        var relic = __instance.Relic?.Model;
        if (relic == null ||
            !RunStates.TryGetValue(__instance, out var runStateBox))
        {
            return;
        }

        try
        {
            var player = LocalContext.GetMe(runStateBox.RunState.Players);
            if (player == null)
            {
                return;
            }

            var previewRelic = PredictionUtils.CreateRelic(relic, player);
            var predictionTips = OutOfCombatRelicPrediction.GetHoverTips(player, previewRelic);
            if (predictionTips.Count == 0)
            {
                return;
            }

            var tips = relic.HoverTips.Concat(predictionTips).ToList();
            NHoverTipSet.Remove(__instance);
            NHoverTipSet.CreateAndShow(__instance, tips)?.SetAlignmentForRelic(__instance.Relic!);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Treasure room relic prediction failed: {ex}");
        }
    }

    [HarmonyPatch(typeof(NTreasureRoomRelicHolder), "OnUnfocus")]
    [HarmonyPostfix]
    private static void OnUnfocusPostfix(NTreasureRoomRelicHolder __instance)
    {
        NHoverTipSet.Remove(__instance);
    }

    private sealed class RunStateBox(IRunState runState)
    {
        public IRunState RunState { get; } = runState;
    }
}
