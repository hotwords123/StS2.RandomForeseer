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

    private List<PredictedCard>? _handCards;
    private List<PredictedCard>? _drawPileCards;
    private List<PredictedCard>? _discardPileCards;
    private List<PredictedCard>? _exhaustPileCards;
    private List<PredictedCard>? _playPileCards;

    public List<PredictedCard> HandCards =>
        _handCards ??= PredictedCard.FromCards(PileType.Hand.GetPile(player).Cards);

    public List<PredictedCard> DrawPileCards =>
        _drawPileCards ??= PredictedCard.FromCards(PileType.Draw.GetPile(player).Cards);

    public List<PredictedCard> DiscardPileCards =>
        _discardPileCards ??= PredictedCard.FromCards(PileType.Discard.GetPile(player).Cards);

    public List<PredictedCard> ExhaustPileCards =>
        _exhaustPileCards ??= PredictedCard.FromCards(PileType.Exhaust.GetPile(player).Cards);

    public List<PredictedCard> PlayPileCards =>
        _playPileCards ??= PredictedCard.FromCards(PileType.Play.GetPile(player).Cards);

    public IEnumerable<PredictedCard> AllCards =>
        HandCards
            .Concat(DrawPileCards)
            .Concat(DiscardPileCards)
            .Concat(ExhaustPileCards)
            .Concat(PlayPileCards);

    public CombatCardDrawPredictionState CardDrawState { get; } = new()
    {
        StatusCardsDrawnThisTurn = CountStatusCardsDrawnThisTurn(combatState, player),
        BoundCardsAfflictedThisTurn = CountBoundCardsAfflictedThisTurn(combatState, player)
    };

    public PredictedCard? FindCard(CardModel card)
    {
        return AllCards.FirstOrDefault(predicted => predicted.References(card));
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
