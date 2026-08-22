using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class ChooseACardPredictionContext
{
    private static readonly List<Registration> Registrations = [];

    public static bool TryGet(CardModel card, [NotNullWhen(true)] out AbstractModel? source)
    {
        lock (Registrations)
        {
            if (Registrations.FindLast(item => item.Cards.Contains(card)) is { } registration)
            {
                source = registration.Source;
                return true;
            }
        }

        source = null;
        return false;
    }

    public static Registration? Register(IReadOnlyList<CardModel> cards, AbstractModel? source)
    {
        if (cards.Count == 0 || source is null)
        {
            return null;
        }

        var registration = new Registration(cards, source);
        lock (Registrations)
        {
            Registrations.Add(registration);
        }

        return registration;
    }

    public static void Unregister(Registration registration)
    {
        lock (Registrations)
        {
            Registrations.Remove(registration);
        }
    }

    internal sealed class Registration(IEnumerable<CardModel> cards, AbstractModel source)
    {
        public HashSet<CardModel> Cards { get; } = [.. cards];

        public AbstractModel Source { get; } = source;
    }
}
