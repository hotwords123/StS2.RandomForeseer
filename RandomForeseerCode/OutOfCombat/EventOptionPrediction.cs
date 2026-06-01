using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace RandomForeseer;

internal static class EventOptionPredictionRegistry
{
    private static readonly List<Func<EventModel, EventOption, IReadOnlyList<IHoverTip>>> Providers = [];

    static EventOptionPredictionRegistry()
    {
        Register(GetNeowRelicHoverTips);
    }

    public static void Register(Func<EventModel, EventOption, IReadOnlyList<IHoverTip>> provider)
    {
        Providers.Add(provider);
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

    private static IReadOnlyList<IHoverTip> GetNeowRelicHoverTips(EventModel eventModel, EventOption option)
    {
        if (eventModel is not Neow neow || neow.Owner == null || option.Relic == null)
        {
            return [];
        }

        return OutOfCombatRelicPrediction.GetHoverTips(neow.Owner, option.Relic);
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
