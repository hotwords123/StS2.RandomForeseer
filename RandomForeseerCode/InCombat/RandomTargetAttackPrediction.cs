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
    PredictedCard source,
    CardPlay cardPlay)
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

        simulator.ManualPlay(predictedCard, target: null, (_, cardPlay) =>
        {
            new RandomTargetAttackPrediction(simulator, playerCombatState, predictedCard, cardPlay)
                .Simulate();
        });

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

    private void Simulate()
    {
        switch (source.Preview)
        {
            case FlakCannon:
                SimulateFlakCannon();
                break;
            case Ricochet:
                SimulateRandomAttack(source.Preview.DynamicVars.Repeat.IntValue);
                break;
            case RipAndTear:
                SimulateRandomAttack(2);
                break;
            case Stardust:
                SimulateRandomAttack(source.ResolveStarXValue(simulator.State));
                break;
            case SweepingGaze:
                SimulateSweepingGaze();
                break;
            case SwordBoomerang:
                SimulateRandomAttack(source.Preview.DynamicVars.Repeat.IntValue);
                break;
            case Volley:
                SimulateRandomAttack(source.ResolveEnergyXValue(simulator.State));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported card type for random target attack prediction: {source.Preview.Id}");
        }
    }

    private void SimulateRandomAttack(int hitCount)
    {
        DamageCmd.Attack(source.Preview.DynamicVars.Damage.BaseValue)
            .FromCard(source.Preview, cardPlay)
            .WithHitCount(hitCount)
            .TargetingRandomOpponents(simulator.State.CombatState)
            .Simulate(simulator);
    }

    private void SimulateFlakCannon()
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

        SimulateRandomAttack(statuses.Count);
    }

    private void SimulateSweepingGaze()
    {
        if (source.Preview.Owner.Osty is not { } osty || !simulator.State.GetCreature(osty).IsAlive)
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
