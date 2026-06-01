using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace RandomForeseer;

internal interface IEventOptionPredictionProvider
{
    IReadOnlyList<IHoverTip> GetHoverTips(EventModel eventModel, EventOption option);
}

internal static class EventOptionPredictionRegistry
{
    private static readonly List<IEventOptionPredictionProvider> Providers = [];

    static EventOptionPredictionRegistry()
    {
        Register(NeowRelicPrediction.Provider);
    }

    public static void Register(IEventOptionPredictionProvider provider)
    {
        Providers.Add(provider);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(EventModel eventModel, EventOption option)
    {
        var tips = new List<IHoverTip>();
        foreach (var provider in Providers)
        {
            tips.AddRange(provider.GetHoverTips(eventModel, option));
        }

        return tips;
    }
}

[HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton.Create))]
internal static class EventOptionPredictionPatch
{
    private static readonly ConditionalWeakTable<EventOption, OriginalHoverTipsBox> OriginalHoverTips = [];

    private static void Prefix(EventModel eventModel, EventOption option)
    {
        if (!OriginalHoverTips.TryGetValue(option, out var box))
        {
            box = new OriginalHoverTipsBox(option.HoverTips.ToList());
            OriginalHoverTips.Add(option, box);
        }

        var originalTips = box.Tips;
        var predictionTips = EventOptionPredictionRegistry.GetHoverTips(eventModel, option);
        option.HoverTips = predictionTips.Count > 0
            ? originalTips.Concat(predictionTips).ToList()
            : originalTips;
    }

    private sealed class OriginalHoverTipsBox(IReadOnlyList<IHoverTip> tips)
    {
        public IReadOnlyList<IHoverTip> Tips { get; } = tips;
    }
}
