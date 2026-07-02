using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class SimPlayerCombatState(Player player, ICombatState combatState)
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

    public IEnumerable<PredictedCard> AllCards =>
        GetCards(PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play);

    public CombatCardDrawPredictionState CardDrawState { get; } = new()
    {
        StatusCardsDrawnThisTurn = CountStatusCardsDrawnThisTurn(combatState, player),
        BoundCardsAfflictedThisTurn = CountBoundCardsAfflictedThisTurn(combatState, player)
    };

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

    public IEnumerable<PredictedCard> GetCards(params PileType[] piles)
    {
        return piles.SelectMany(type => GetCardPile(type)?.Cards ?? []);
    }

    private static int CountStatusCardsDrawnThisTurn(ICombatState combatState, Player player)
    {
        return CombatManager.Instance.History.Entries
            .OfType<CardDrawnEntry>()
            .Count(entry =>
                entry.HappenedThisTurn(combatState) &&
                entry.Actor == player.Creature &&
                entry.Card.Type == CardType.Status);
    }

    private static int CountBoundCardsAfflictedThisTurn(ICombatState combatState, Player player)
    {
        return CombatManager.Instance.History.Entries
            .OfType<CardAfflictedEntry>()
            .Count(entry =>
                entry.HappenedThisTurn(combatState) &&
                entry.Actor == player.Creature &&
                entry.Affliction is Bound);
    }
}

internal sealed class CombatCardDrawPredictionState
{
    public int StatusCardsDrawnThisTurn { get; set; }

    public int BoundCardsAfflictedThisTurn { get; set; }
}
