using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class OrbPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card, target: null)?.ToHoverTips() ?? [];
    }

    public static OrbPredictionResult? Predict(CardModel card, Creature? target)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) ||
            !IsSupported(card) ||
            !card.TryResolveTarget(ref target) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return null;
        }

        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);
        simulator.ManualPlay(predictedCard, target);

        List<IHoverTip> extraTips = [];

        if (card is Chaos)
        {
            var channeledOrbs = simulator.History
                .OfType<CombatPredictionOrbChanneledEntry>()
                .Where(entry => ReferenceEquals(entry.Trace?.Source, card))
                .Select(entry => entry.Orb);
            extraTips.AddRange(PredictionHoverTips.Orbs(channeledOrbs));
        }

        return new(DamagePredictionResult.FromDamageHistory(simulator), extraTips);
    }

    private static bool IsSupported(CardModel card)
    {
        return card is
            BallLightning or
            Chaos or
            Chill or
            ColdSnap or
            ConsumingShadow or
            Coolheaded or
            Darkness or
            Dualcast or
            Fusion or
            Glacier or
            Glasswork or
            IceLance or
            Ignition or
            MeteorStrike or
            MultiCast or
            Null or
            Quadcast or
            Rainbow or
            Refract or
            ShadowShield or
            Shatter or
            Spinner { IsUpgraded: true } or
            Tempest or
            TeslaCoil or
            Voltaic or
            Zap;
    }
}

internal sealed record OrbPredictionResult(
    DamagePredictionResult DamagePrediction,
    IReadOnlyList<IHoverTip> ExtraHoverTips)
{
    public bool IsEmpty => !DamagePrediction.HasTargets && ExtraHoverTips.Count == 0;

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        if (IsEmpty)
        {
            return [];
        }

        var hoverTips = ExtraHoverTips.ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(hoverTips, "orb", DamagePrediction.Risk);
        return hoverTips;
    }
}
