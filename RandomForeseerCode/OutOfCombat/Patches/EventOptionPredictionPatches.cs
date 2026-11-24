using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Patches;

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

[HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton.Create))]
internal static class EventOptionButtonCreatePatch
{
    private static void Prefix(EventModel eventModel, EventOption option)
    {
        // Mods may rebuild event buttons after replacing CurrentOptions directly, bypassing SetEventState.
        EventOptionEventModelMap.Register(option, eventModel);
    }
}
