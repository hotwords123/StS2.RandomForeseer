using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class SimPlayerCombatState(PlayerCombatState liveState)
{
    public SimOrbQueue OrbQueue => field ??= new SimOrbQueue(liveState.OrbQueue);

    public SimCardPile Hand => field ??= new SimCardPile(liveState.Hand);

    public SimCardPile DrawPile => field ??= new SimCardPile(liveState.DrawPile);

    public SimCardPile DiscardPile => field ??= new SimCardPile(liveState.DiscardPile);

    public SimCardPile ExhaustPile => field ??= new SimCardPile(liveState.ExhaustPile);

    public SimCardPile PlayPile => field ??= new SimCardPile(liveState.PlayPile);

    public IReadOnlyList<SimCardPile> AllPiles => [Hand, DrawPile, DiscardPile, ExhaustPile, PlayPile];

    public IEnumerable<PredictedCard> AllCards => AllPiles.SelectMany(pile => pile.Cards);

    public int Energy { get; private set; } = liveState.Energy;

    public int Stars { get; private set; } = liveState.Stars;

    public PredictedCard? FindCard(CardModel card)
    {
        return AllCards.FirstOrDefault(predicted => predicted.References(card));
    }

    public SimCardPile? GetCardPile(PileType type)
    {
        return type switch
        {
            PileType.None => null,
            PileType.Draw => DrawPile,
            PileType.Hand => Hand,
            PileType.Discard => DiscardPile,
            PileType.Exhaust => ExhaustPile,
            PileType.Play => PlayPile,
            PileType.Deck => throw new ArgumentOutOfRangeException(nameof(type), type, "Deck is not a combat pile."),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown pile type: {type}.")
        };
    }

    // Mirrors PlayerCombatState.GainEnergy.
    public void GainEnergy(decimal amount)
    {
        Energy = (int)Math.Clamp(Energy + amount, 0m, 999999999m);
    }

    // Mirrors PlayerCombatState.LoseEnergy.
    public void LoseEnergy(decimal amount)
    {
        Energy = (int)Math.Clamp(Energy - amount, 0m, 999999999m);
    }

    // Mirrors PlayerCombatState.GainStars.
    public void GainStars(decimal amount)
    {
        Stars = (int)Math.Clamp(Stars + amount, 0m, 999999999m);
    }

    // Mirrors PlayerCombatState.LoseStars.
    public void LoseStars(decimal amount)
    {
        Stars = (int)Math.Clamp(Stars - amount, 0m, 999999999m);
    }

    /// <summary>
    /// Mirrors <see cref="PlayerCombatState.EndOfTurnCleanup"/> using only the mutable preview cards.
    /// </summary>
    public void EndOfTurnCleanup()
    {
        foreach (var card in AllCards)
        {
            card.MutablePreview.EndOfTurnCleanup();
        }
    }
}
