using Godot;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace RandomForeseer.OutOfCombat;

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

    public static void Register<TEvent>(
        Func<TEvent, EventOption, IReadOnlyList<IHoverTip>> provider,
        PredictionFairness fairness = PredictionFairness.Fair)
        where TEvent : EventModel
    {
        Register((eventModel, option) =>
        {
            if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableEventOptionPrediction) ||
                !RandomForeseerSettings.IsFairPredictionAllowed(fairness) ||
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

    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        return owner is NEventOptionButton button
            ? GetHoverTips(button.Event, button.Option)
            : [];
    }

    private static IReadOnlyList<IHoverTip> GetRelicHoverTips(EventModel eventModel, EventOption option)
    {
        if (eventModel.Owner == null || option.Relic == null)
        {
            return [];
        }

        return RelicPickupPrediction.GetHoverTips(eventModel.Owner, option.Relic);
    }
}
