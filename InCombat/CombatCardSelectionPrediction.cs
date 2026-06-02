using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class CombatCardSelectionPrediction
{
    public static CombatCardSelectionPredictionResult GetPrediction(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction) ||
            !card.IsMutable ||
            card.Pile?.Type != PileType.Hand ||
            card.Owner.Creature.CombatState == null)
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var previewRng = PredictionUtils.CloneRng(card.Owner.RunState.Rng.CombatCardSelection);
        return Predict(card, previewRng);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        var prediction = GetPrediction(card);
        var tips = PredictionHoverTips.Cards(prediction.HoverTipCards).ToList();
        if (prediction.HasDriftRisk && RandomForeseerSettings.EnableCombatCardSelectionDriftWarnings)
        {
            tips.Add(PredictionHoverTips.CardSelectionDriftWarning());
        }

        return tips;
    }

    private static CombatCardSelectionPredictionResult Predict(CardModel card, MegaCrit.Sts2.Core.Random.Rng previewRng)
    {
        return card switch
        {
            TrueGrit when !card.IsUpgraded => WithDriftRisk(HighlightHandCard(PredictHandCard(card, c => true, previewRng))),
            Cinder => WithDriftRisk(HighlightHandCard(PredictHandCard(card, c => true, previewRng))),
            Thrash => WithDriftRisk(HighlightHandCard(PredictHandCard(card, c => c.Type == CardType.Attack, previewRng))),
            HiddenGem => HoverTipCards(PredictHiddenGem(card, previewRng)),
            DrainPower => WithDriftRisk(HoverTipCards(PredictDrainPower(card, previewRng))),
            Anointed => HoverTipCards(PredictAnointed(card, previewRng)),
            SeekerStrike => WithDriftRisk(HoverTipCards(PredictSeekerStrike(card, previewRng))),
            Uproar => WithDriftRisk(HoverTipCards(PredictUproar(card))),
            _ => CombatCardSelectionPredictionResult.Empty
        };
    }

    private static CardModel? PredictHandCard(
        CardModel source,
        Func<CardModel, bool> filter,
        MegaCrit.Sts2.Core.Random.Rng previewRng)
    {
        var candidates = PileType.Hand.GetPile(source.Owner).Cards
            .Where(card => card != source)
            .Where(filter);

        return previewRng.NextItem(candidates);
    }

    private static CardModel? PredictHiddenGem(CardModel source, MegaCrit.Sts2.Core.Random.Rng previewRng)
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

    private static IReadOnlyList<CardModel> PredictDrainPower(CardModel source, MegaCrit.Sts2.Core.Random.Rng previewRng)
    {
        return PileType.Discard.GetPile(source.Owner).Cards
            .Where(card => card.IsUpgradable)
            .TakeRandom(source.DynamicVars.Cards.IntValue, previewRng)
            .Select(PredictionUtils.ToUpgradedPreviewCard)
            .ToList();
    }

    private static IReadOnlyList<CardModel> PredictAnointed(CardModel source, MegaCrit.Sts2.Core.Random.Rng previewRng)
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
            .Select(ToExistingCardPreview)
            .ToList();
    }

    private static IReadOnlyList<CardModel> PredictSeekerStrike(CardModel source, MegaCrit.Sts2.Core.Random.Rng previewRng)
    {
        return PileType.Draw.GetPile(source.Owner).Cards
            .ToList()
            .StableShuffle(previewRng)
            .Take(source.DynamicVars.Cards.IntValue)
            .Select(ToExistingCardPreview)
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

        return predicted == null
            ? null
            : ToExistingCardPreview(predicted);
    }

    private static CardModel ToExistingCardPreview(CardModel card)
    {
        return (CardModel)card.MutableClone();
    }

    private static CombatCardSelectionPredictionResult HighlightHandCard(CardModel? card)
    {
        return card == null
            ? CombatCardSelectionPredictionResult.Empty
            : new CombatCardSelectionPredictionResult([ToExistingCardPreview(card)], new HashSet<CardModel> { card });
    }

    private static CombatCardSelectionPredictionResult HoverTipCards(CardModel? card)
    {
        return card == null
            ? CombatCardSelectionPredictionResult.Empty
            : new CombatCardSelectionPredictionResult([card], new HashSet<CardModel>());
    }

    private static CombatCardSelectionPredictionResult HoverTipCards(IReadOnlyList<CardModel> cards)
    {
        return cards.Count == 0
            ? CombatCardSelectionPredictionResult.Empty
            : new CombatCardSelectionPredictionResult(cards, new HashSet<CardModel>());
    }

    private static CombatCardSelectionPredictionResult WithDriftRisk(CombatCardSelectionPredictionResult result)
    {
        return result == CombatCardSelectionPredictionResult.Empty
            ? result
            : result with { HasDriftRisk = true };
    }
}

internal sealed record CombatCardSelectionPredictionResult(
    IReadOnlyList<CardModel> HoverTipCards,
    IReadOnlySet<CardModel> HandCardsToHighlight,
    bool HasDriftRisk = false)
{
    public static CombatCardSelectionPredictionResult Empty { get; } = new([], new HashSet<CardModel>());
}
