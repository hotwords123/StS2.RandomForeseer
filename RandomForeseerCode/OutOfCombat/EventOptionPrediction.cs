using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace RandomForeseer;

internal static class EventOptionPredictionRegistry
{
    private static readonly List<Func<EventModel, EventOption, IReadOnlyList<IHoverTip>>> Providers = [];

    static EventOptionPredictionRegistry()
    {
        Register(GetRelicHoverTips);
    }

    public static void Register(Func<EventModel, EventOption, IReadOnlyList<IHoverTip>> provider)
    {
        Providers.Add(provider);
    }

    public static void Register<TEvent>(Func<TEvent, EventOption, IReadOnlyList<IHoverTip>> provider)
        where TEvent : EventModel
    {
        Register((eventModel, option) =>
        {
            if (!RandomForeseerSettings.EnableEventOptionPrediction ||
                eventModel.Owner == null ||
                option.IsLocked ||
                eventModel is not TEvent typedEvent)
            {
                return [];
            }

            try
            {
                return provider(typedEvent, option);
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Event option prediction failed for {eventModel.Id} {option.TextKey}: {ex}");
                return [];
            }
        });
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(EventModel eventModel, EventOption option)
    {
        var tips = new List<IHoverTip>();
        foreach (var provider in Providers)
        {
            tips.AddRange(provider(eventModel, option));
        }

        return tips;
    }

    private static IReadOnlyList<IHoverTip> GetRelicHoverTips(EventModel eventModel, EventOption option)
    {
        if (eventModel.Owner == null || option.Relic == null)
        {
            return [];
        }

        return OutOfCombatRelicPrediction.GetHoverTips(eventModel.Owner, option.Relic);
    }
}

[HarmonyPatch]
internal static class EventOptionPredictionPatch
{
    private static readonly ConditionalWeakTable<EventOption, OriginalHoverTipsBox> OriginalHoverTips = [];

    [HarmonyPatch(typeof(NEventOptionButton), "OnFocus")]
    [HarmonyPrefix]
    private static void OnFocusPrefix(NEventOptionButton __instance)
    {
        var option = __instance.Option;
        if (!OriginalHoverTips.TryGetValue(option, out var box))
        {
            box = new OriginalHoverTipsBox(option.HoverTips.ToList());
            OriginalHoverTips.Add(option, box);
        }

        var originalTips = box.Tips;
        var predictionTips = EventOptionPredictionRegistry.GetHoverTips(__instance.Event, option);
        option.HoverTips = predictionTips.Count > 0
            ? originalTips.Concat(predictionTips).ToList()
            : originalTips;
    }

    [HarmonyPatch(typeof(NEventOptionButton), "OnUnfocus")]
    [HarmonyPostfix]
    private static void OnUnfocusPostfix(NEventOptionButton __instance)
    {
        var option = __instance.Option;
        if (OriginalHoverTips.TryGetValue(option, out var box))
        {
            option.HoverTips = box.Tips;
        }
    }

    private sealed class OriginalHoverTipsBox(IReadOnlyList<IHoverTip> tips)
    {
        public IReadOnlyList<IHoverTip> Tips { get; } = tips;
    }
}
