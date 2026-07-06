using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class SimPlayerCombatState(Player player)
{
    public SimOrbQueue OrbQueue { get; } = new(player);

    private SimCardPile? _hand;
    private SimCardPile? _drawPile;
    private SimCardPile? _discardPile;
    private SimCardPile? _exhaustPile;
    private SimCardPile? _playPile;

    public SimCardPile Hand => _hand ??= SimCardPile.FromPlayerPile(PileType.Hand, player);

    public SimCardPile DrawPile => _drawPile ??= SimCardPile.FromPlayerPile(PileType.Draw, player);

    public SimCardPile DiscardPile => _discardPile ??= SimCardPile.FromPlayerPile(PileType.Discard, player);

    public SimCardPile ExhaustPile => _exhaustPile ??= SimCardPile.FromPlayerPile(PileType.Exhaust, player);

    public SimCardPile PlayPile => _playPile ??= SimCardPile.FromPlayerPile(PileType.Play, player);

    public int Energy { get; private set; } = player.PlayerCombatState?.Energy ?? 0;

    public int Stars { get; private set; } = player.PlayerCombatState?.Stars ?? 0;

    public IReadOnlyList<SimCardPile> AllPiles => [Hand, DrawPile, DiscardPile, ExhaustPile, PlayPile];

    public IEnumerable<PredictedCard> AllCards => AllPiles.SelectMany(pile => pile.Cards);

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

    public void GainEnergy(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("amount must be positive. Use LoseEnergy for energy loss.", nameof(amount));
        }

        Energy = (int)Math.Clamp(Energy + amount, 0m, 999999999m);
    }

    public void LoseEnergy(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("amount must be positive. Use GainEnergy for energy gain.", nameof(amount));
        }

        Energy = (int)Math.Clamp(Energy - amount, 0m, 999999999m);
    }
}
