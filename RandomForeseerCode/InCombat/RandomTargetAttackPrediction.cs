using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class RandomTargetAttackPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card)?.ToHoverTips() ?? [];
    }

    public static RandomTargetAttackPredictionResult? Predict(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableRandomTargetAttackPrediction) ||
            !IsSupported(card) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return null;
        }

        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);
        simulator.ManualPlay(predictedCard, target: null);

        return new(DamagePredictionResult.FromDamageHistory(simulator));
    }

    private static bool IsSupported(CardModel card)
    {
        return card is
            FlakCannon or
            Ricochet or
            RipAndTear or
            Stardust or
            SweepingGaze or
            SwordBoomerang or
            Volley;
    }
}

internal sealed record RandomTargetAttackPredictionResult(DamagePredictionResult DamagePrediction)
{
    public bool IsEmpty => !DamagePrediction.HasTargets;

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        if (IsEmpty)
        {
            return [];
        }

        List<IHoverTip> hoverTips = [];
        PredictionHoverTips.AddDriftWarningIfNeeded(hoverTips, "random_target_attack", DamagePrediction.Risk);
        return hoverTips;
    }
}
