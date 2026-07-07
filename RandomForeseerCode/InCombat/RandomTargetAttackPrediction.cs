using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class RandomTargetAttackPrediction(
    CombatPredictionSimulator simulator,
    SimPlayerCombatState playerCombatState,
    PredictedCard source)
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

        var playerCombatState = simulator.State.GetPlayerCombatState(card.Owner);
        var predictedCard = playerCombatState.FindCard(card) ?? new PredictedCard(card);
        var predictor = new RandomTargetAttackPrediction(simulator, playerCombatState, predictedCard);

        simulator.ManualPlay(predictedCard, target: null, predictor.GetOnPlayDelegate());

        return new(DamagePredictionResult.FromDamageHistory(simulator));
    }

    private static bool IsSupported(CardModel card)
    {
        return card is FlakCannon
            or Ricochet
            or RipAndTear
            or Stardust
            or SweepingGaze
            or SwordBoomerang
            or Volley;
    }

    private OnPlayDelegate GetOnPlayDelegate()
    {
        return source.Preview switch
        {
            FlakCannon => SimulateFlakCannon,
            Ricochet => (_, cardPlay) =>
                SimulateRandomAttack(cardPlay, source.Preview.DynamicVars.Repeat.IntValue),
            RipAndTear => (_, cardPlay) =>
                SimulateRandomAttack(cardPlay, 2),
            Stardust => (_, cardPlay) =>
                SimulateRandomAttack(cardPlay, source.ResolveStarXValue(simulator.State)),
            SweepingGaze => SimulateSweepingGaze,
            SwordBoomerang => (_, cardPlay) =>
                SimulateRandomAttack(cardPlay, source.Preview.DynamicVars.Repeat.IntValue),
            Volley => (_, cardPlay) =>
                SimulateRandomAttack(cardPlay, source.ResolveEnergyXValue(simulator.State)),
            _ => throw new InvalidOperationException(
                $"Unsupported card type for random target attack prediction: {source.Preview.Id}")
        };
    }

    private void SimulateRandomAttack(CardPlay cardPlay, int hitCount)
    {
        DamageCmd.Attack(source.Preview.DynamicVars.Damage.BaseValue)
            .FromCard(source.Preview, cardPlay)
            .WithHitCount(hitCount)
            .TargetingRandomOpponents(simulator.State.CombatState)
            .Simulate(simulator);
    }

    private void SimulateFlakCannon(PredictedCard _, CardPlay cardPlay)
    {
        var statuses = playerCombatState.AllCards
            .Where(card =>
                card.Preview.Type is CardType.Status &&
                !playerCombatState.ExhaustPile.Cards.Contains(card))
            .ToList();

        foreach (var status in statuses)
        {
            simulator.Exhaust(status);
        }

        SimulateRandomAttack(cardPlay, statuses.Count);
    }

    private void SimulateSweepingGaze(PredictedCard _, CardPlay cardPlay)
    {
        if (source.Preview.Owner.Osty is not { } osty ||
            !simulator.State.GetCreature(osty).IsAlive)
        {
            return;
        }

        DamageCmd.Attack(source.Preview.DynamicVars.OstyDamage.BaseValue)
            .FromOsty(osty, source.Preview, cardPlay)
            .TargetingRandomOpponents(simulator.State.CombatState)
            .Simulate(simulator);
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
