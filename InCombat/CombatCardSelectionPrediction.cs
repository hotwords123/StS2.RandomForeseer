using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class CombatCardSelectionPrediction
{
    public static CombatCardSelectionPredictionResult GetPrediction(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction))
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var previewRng = PredictionUtils.CloneRng(card.Owner.RunState.Rng.CombatCardSelection);
        return Predict(card, previewRng);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return GetPrediction(card).ToHoverTips();
    }

    private static CombatCardSelectionPredictionResult Predict(CardModel card, Rng previewRng)
    {
        return card switch
        {
            Cinder => CombatCardSelectionPredictionResult.FromSelectedCard(
                PredictHandCard(card, _ => true, previewRng),
                DamageBlockRiskDetector.DetectAttack(card)),
            HiddenGem => CombatCardSelectionPredictionResult.FromSelectedCard(
                PredictHiddenGem(card, previewRng),
                PredictionRisk.None),
            Thrash => CombatCardSelectionPredictionResult.FromSelectedCard(
                PredictHandCard(card, c => c.Type == CardType.Attack, previewRng),
                DamageBlockRiskDetector.DetectAttack(card, hitCount: 2)),
            TrueGrit { IsUpgraded: false } => CombatCardSelectionPredictionResult.FromSelectedCard(
                PredictHandCard(card, _ => true, previewRng),
                DamageBlockRiskDetector.DetectGainBlock(card)),
            Uproar => CombatCardSelectionPredictionResult.FromSelectedCard(
                PredictUproar(card),
                DamageBlockRiskDetector.DetectAttack(card, hitCount: 2)),

            Anointed => CombatCardSelectionPredictionResult.FromSelectedCards(
                PredictAnointed(card, previewRng),
                PredictionRisk.None),
            DrainPower => CombatCardSelectionPredictionResult.FromSelectedCards(
                PredictDrainPower(card, previewRng),
                DamageBlockRiskDetector.DetectAttack(card)),
            SeekerStrike => CombatCardSelectionPredictionResult.FromSelectedCards(
                PredictSeekerStrike(card, previewRng),
                DamageBlockRiskDetector.DetectAttack(card)),

            _ => CombatCardSelectionPredictionResult.Empty
        };
    }

    private static CardModel? PredictHandCard(
        CardModel source,
        Func<CardModel, bool> filter,
        Rng previewRng)
    {
        var candidates = PileType.Hand.GetPile(source.Owner).Cards
            .Where(card => card != source && filter(card));

        return previewRng.NextItem(candidates);
    }

    private static CardModel? PredictHiddenGem(CardModel source, Rng previewRng)
    {
        var drawPileCards = PileType.Draw.GetPile(source.Owner).Cards.ToList();
        if (drawPileCards.Count == 0)
        {
            return null;
        }

        var eligibleCards = drawPileCards
            .Where(card =>
                !card.Keywords.Contains(CardKeyword.Unplayable) &&
                card.Type is not CardType.Status and not CardType.Curse &&
                card.GetEnchantedReplayCount() < 1)
            .ToList();
        var preferredCards = eligibleCards
            .Where(card => card.Type is CardType.Attack or CardType.Skill or CardType.Power)
            .ToList();

        var predicted = previewRng.NextItem(preferredCards.Count == 0 ? eligibleCards : preferredCards);
        if (predicted == null)
        {
            return null;
        }

        var preview = (CardModel)predicted.MutableClone();
        preview.BaseReplayCount += source.DynamicVars["Replay"].IntValue;
        return preview;
    }

    private static IReadOnlyList<CardModel> PredictDrainPower(CardModel source, Rng previewRng)
    {
        return PileType.Discard.GetPile(source.Owner).Cards
            .Where(card => card.IsUpgradable)
            .TakeRandom(source.DynamicVars.Cards.IntValue, previewRng)
            .Select(PredictionUtils.ToUpgradedCard)
            .ToList();
    }

    private static IReadOnlyList<CardModel> PredictAnointed(CardModel source, Rng previewRng)
    {
        var cardsInHandAfterPlay = PileType.Hand.GetPile(source.Owner).Cards.Count(card => card != source);
        var count = CardPile.MaxCardsInHand - cardsInHandAfterPlay;
        if (count <= 0)
        {
            return [];
        }

        return PileType.Draw.GetPile(source.Owner).Cards
            .Where(card => card.Rarity == CardRarity.Rare)
            .TakeRandom(count, previewRng)
            .ToList();
    }

    private static IReadOnlyList<CardModel> PredictSeekerStrike(CardModel source, Rng previewRng)
    {
        return PileType.Draw.GetPile(source.Owner).Cards
            .ToList()
            .StableShuffle(previewRng)
            .Take(source.DynamicVars.Cards.IntValue)
            .ToList();
    }

    private static CardModel? PredictUproar(CardModel source)
    {
        var previewRng = PredictionUtils.CloneRng(source.Owner.RunState.Rng.Shuffle);
        var drawPileCards = PileType.Draw.GetPile(source.Owner).Cards;
        var predicted = drawPileCards
            .Where(card => card.Type == CardType.Attack && !card.Keywords.Contains(CardKeyword.Unplayable))
            .ToList()
            .StableShuffle(previewRng)
            .FirstOrDefault();

        predicted ??= drawPileCards
            .Where(card => card.Type == CardType.Attack)
            .ToList()
            .StableShuffle(previewRng)
            .FirstOrDefault();

        return predicted;
    }
}

internal sealed record CombatCardSelectionPredictionResult(
    IReadOnlyList<CardModel> SelectedCards,
    PredictionRisk Risk)
{
    public bool HasDriftRisk => Risk is { HasRisk: true };

    public static CombatCardSelectionPredictionResult FromSelectedCards(IReadOnlyList<CardModel> selectedCards, PredictionRisk risk)
    {
        return selectedCards.Count > 0
            ? new(selectedCards, risk)
            : Empty;
    }

    public static CombatCardSelectionPredictionResult FromSelectedCard(CardModel? selectedCard, PredictionRisk risk)
    {
        return selectedCard != null
            ? new([selectedCard], risk)
            : Empty;
    }

    public static CombatCardSelectionPredictionResult Empty { get; } = new([], PredictionRisk.None);

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        var tips = PredictionHoverTips.Cards(SelectedCards).ToList();
        if (HasDriftRisk && RandomForeseerSettings.EnableDriftWarnings)
        {
            tips.Add(PredictionHoverTips.DriftWarning("card_selection", Risk));
        }

        return tips;
    }
}
