using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Utils;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

/// <summary>
/// Carries a deck-transform origin through the asynchronous game flow that opens its selector.
/// </summary>
internal static class DeckTransformPredictionContext
{
    private static readonly AsyncLocal<DeckTransformPredictionSource?> Current = new();

    public static DeckTransformPredictionSource? CurrentSource => Current.Value;

    public static IDisposable EnterRelic(RelicModel relic)
    {
        return Enter(new DeckTransformPredictionSource.Relic(relic));
    }

    public static IDisposable? EnterEventOption(EventOption option)
    {
        return EventOptionEventModelMap.TryGetEvent(option, out var eventModel)
            ? Enter(new DeckTransformPredictionSource.Event(eventModel, option))
            : null;
    }

    private static IDisposable Enter(DeckTransformPredictionSource source)
    {
        var previous = Current.Value;
        Current.Value = source;
        return new DisposableAction(() => Current.Value = previous);
    }
}

internal abstract record DeckTransformPredictionSource
{
    internal sealed record Relic(RelicModel Model) : DeckTransformPredictionSource;

    internal sealed record Event(EventModel Model, EventOption Option) : DeckTransformPredictionSource;
}
