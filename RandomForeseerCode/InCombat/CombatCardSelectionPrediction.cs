using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardSelectionPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card, target: null).ToHoverTips();
    }

    public static CombatCardSelectionPredictionResult Predict(CardModel card, Creature? target)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction) ||
            !IsSupported(card) ||
            !card.TryResolveTarget(ref target) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);
        simulator.ManualPlay(predictedCard, target);

        var history = simulator.History
            .OfType<CombatPredictionCardsSelectedEntry>()
            .Where(entry => ReferenceEquals(entry.SourceModel, card))
            .ToList();
        var cardBundles = history
            .Select(entry => entry.Cards)
            .ToList();

        return new(cardBundles, simulator.History.GetRisk(history));
    }

    private static bool IsSupported(CardModel card)
    {
        return card is
            Anointed or
            BeatDown or
            Catastrophe or
            Cinder or
            DrainPower or
            HiddenGem or
            SeekerStrike or
            Thrash or
            TrueGrit { IsUpgraded: false } or
            Uproar;
    }
}

internal sealed record CombatCardSelectionPredictionResult(
    IReadOnlyList<IReadOnlyList<PredictedCard>> CardBundles,
    PredictionRisk Risk)
{
    public static CombatCardSelectionPredictionResult Empty { get; } = new([], PredictionRisk.None);

    public bool IsEmpty => !CardBundles.Any(bundle => bundle.Count > 0);

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        if (IsEmpty)
        {
            return [];
        }

        var bundles = CardBundles
            .Select(bundle => bundle.Select(card => card.Preview).ToList())
            .ToList();
        var tips = PredictionHoverTips.CardBundles(bundles).ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(tips, "card_selection", Risk);
        return tips;
    }
}
