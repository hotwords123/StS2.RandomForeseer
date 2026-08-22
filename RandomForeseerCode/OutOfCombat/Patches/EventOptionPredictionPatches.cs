using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(EventModel), "SetEventState")]
internal static class EventModelSetEventStatePatch
{
    private static void Prefix(EventModel __instance, ref IEnumerable<EventOption> eventOptions)
    {
        var optionsList = eventOptions.Materialize();
        eventOptions = optionsList;

        foreach (var option in optionsList)
        {
            EventOptionEventModelMap.Register(option, __instance);
        }
    }
}

[HarmonyPatch(typeof(EventOption), nameof(EventOption.HoverTips), MethodType.Getter)]
internal static class EventOptionPredictionHoverTipsPatch
{
    private static void Postfix(EventOption __instance, ref IEnumerable<IHoverTip> __result)
    {
        // Event options may reuse a model's HoverTips, which can already include predictions from global patches.
        // Remove those nested prediction tips here so the option only shows its own event prediction and avoids
        // confusing mixed results.
        __result = __result.Where(static tip => !tip.IsPredictionHoverTip());

        var predictionTips = EventOptionPrediction.GetHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
