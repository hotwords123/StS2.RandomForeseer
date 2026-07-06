using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class CombatCardSelectionPrediction(
    CombatPredictionSimulator simulator,
    SimPlayerCombatState playerCombatState,
    PredictedCard source,
    Creature? target)
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card, target: null).ToHoverTips();
    }

    public static CombatCardSelectionPredictionResult Predict(CardModel card, Creature? target)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var playerCombatState = simulator.State.GetPlayerCombatState(card.Owner);
        var predictedCard = playerCombatState.FindCard(card) ?? new PredictedCard(card);

        using (simulator.PushSource(card))
        {
            simulator.AddToPile(predictedCard, PileType.Play);
            return new CombatCardSelectionPrediction(simulator, playerCombatState, predictedCard, target).Predict();
        }
    }

    private CombatCardSelectionPredictionResult Predict()
    {
        return source.Preview switch
        {
            Anointed => PredictAnointed(),
            Cinder => PredictCinder(),
            DrainPower => PredictDrainPower(),
            HiddenGem => PredictHiddenGem(),
            SeekerStrike => PredictSeekerStrike(),
            Thrash => PredictThrash(),
            TrueGrit { IsUpgraded: false } => PredictTrueGrit(),
            Uproar => PredictUproar(),
            _ => CombatCardSelectionPredictionResult.Empty
        };
    }

    private CombatCardSelectionPredictionResult PredictHandCard(Func<CardModel, bool> filter)
    {
        var candidates = playerCombatState.Hand.Cards
            .Select(predictedCard => predictedCard.Preview)
            .Where(filter);
        var selectedCard = simulator.Rng.CombatCardSelection.NextItem(candidates);
        return new(selectedCard, simulator.Snapshot());
    }

    private bool TrySimulateTargetedAttack(int hitCount = 1)
    {
        return simulator.TrySimulateTargetedAttack(source, target, hitCount);
    }

    private CombatCardSelectionPredictionResult PredictAnointed()
    {
        var cardsInHandAfterPlay = playerCombatState.Hand.Cards.Count;
        var count = CardPile.MaxCardsInHand - cardsInHandAfterPlay;
        if (count <= 0)
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var selectedCards = playerCombatState.DrawPile.Cards
            .Where(card => card.Preview.Rarity == CardRarity.Rare)
            .TakeRandom(count, simulator.Rng.CombatCardSelection)
            .Select(card => card.Preview)
            .ToList();

        return new(selectedCards, simulator.Snapshot());
    }

    private CombatCardSelectionPredictionResult PredictCinder()
    {
        return TrySimulateTargetedAttack()
            ? PredictHandCard(_ => true)
            : CombatCardSelectionPredictionResult.Empty;
    }

    private CombatCardSelectionPredictionResult PredictDrainPower()
    {
        if (!TrySimulateTargetedAttack())
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var selectedCards = playerCombatState.DiscardPile.Cards
            .Where(card => card.Preview.IsUpgradable)
            .TakeRandom(source.Preview.DynamicVars.Cards.IntValue, simulator.Rng.CombatCardSelection)
            .Select(card => card.Upgrade().Preview)
            .ToList();

        return new(selectedCards, simulator.Snapshot());
    }

    private CombatCardSelectionPredictionResult PredictHiddenGem()
    {
        var drawPile = playerCombatState.DrawPile;
        if (drawPile.IsEmpty)
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var eligibleCards = drawPile.Cards
            .Where(card =>
                !card.Preview.Keywords.Contains(CardKeyword.Unplayable) &&
                card.Preview.Type is not CardType.Status and not CardType.Curse &&
                card.Preview.GetEnchantedReplayCount() < 1)
            .ToList();
        var preferredCards = eligibleCards
            .Where(card => card.Preview.Type is CardType.Attack or CardType.Skill or CardType.Power)
            .ToList();

        var predicted = simulator.Rng.CombatCardSelection.NextItem(
            preferredCards.Count == 0 ? eligibleCards : preferredCards);
        if (predicted == null)
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        predicted.MutablePreview.BaseReplayCount += source.Preview.DynamicVars["Replay"].IntValue;
        return new(predicted.Preview, PredictionRisk.None);
    }

    private CombatCardSelectionPredictionResult PredictSeekerStrike()
    {
        if (!TrySimulateTargetedAttack())
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var selectedCards = playerCombatState.DrawPile.Cards
            .ToList()
            .StableShuffle(simulator.Rng.CombatCardSelection)
            .Take(source.Preview.DynamicVars.Cards.IntValue)
            .Select(card => card.Preview)
            .ToList();

        return new(selectedCards, simulator.Snapshot());
    }

    private CombatCardSelectionPredictionResult PredictThrash()
    {
        return TrySimulateTargetedAttack(hitCount: 2)
            ? PredictHandCard(card => card.Type == CardType.Attack)
            : CombatCardSelectionPredictionResult.Empty;
    }

    private CombatCardSelectionPredictionResult PredictTrueGrit()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);
        return PredictHandCard(_ => true);
    }

    private CombatCardSelectionPredictionResult PredictUproar()
    {
        if (!TrySimulateTargetedAttack(hitCount: 2))
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var attackCards = playerCombatState.DrawPile.Cards
            .Where(card => card.Preview.Type == CardType.Attack)
            .ToList();

        var predicted = attackCards
            .Where(card => !card.Preview.Keywords.Contains(CardKeyword.Unplayable))
            .ToList()
            .StableShuffle(simulator.Rng.Shuffle)
            .FirstOrDefault();

        predicted ??= attackCards
            .StableShuffle(simulator.Rng.Shuffle)
            .FirstOrDefault();

        return new(predicted?.Preview, simulator.Snapshot());
    }
}

internal sealed record CombatCardSelectionPredictionResult(
    IReadOnlyList<CardModel> SelectedCards,
    PredictionRisk Risk)
{
    public CombatCardSelectionPredictionResult(CardModel? selectedCard, PredictionRisk risk)
        : this(selectedCard != null ? [selectedCard] : [], risk)
    { }

    public static CombatCardSelectionPredictionResult Empty { get; } = new([], PredictionRisk.None);

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        if (SelectedCards.Count == 0)
        {
            return [];
        }

        var tips = PredictionHoverTips.Cards(SelectedCards).ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(tips, "card_selection", Risk);
        return tips;
    }
}
