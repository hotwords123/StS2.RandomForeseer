using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class DamageBlockRiskDetector
{
    public static PredictionRisk DetectGainBlock(CardModel card)
    {
        if (card.Owner.Creature.CombatState is not { } combatState)
        {
            return PredictionRisk.None;
        }

        var simulator = new CombatPredictionSimulator(combatState);
        simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, new PredictedCard(card));
        return simulator.Snapshot();
    }

    public static PredictionRisk DetectAttack(CardModel card, int hitCount = 1)
    {
        if (card.Owner.Creature.CombatState is not { } combatState)
        {
            return PredictionRisk.None;
        }

        var simulator = new CombatPredictionSimulator(combatState);
        simulator.Execute(
            DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
                // StS2 v0.108.0 added CardPlay to AttackCommand.FromCard; risk detection
                // runs from hover preview, so it intentionally has no live CardPlay.
                .FromCard(card, null)
                .WithHitCount(hitCount));
        return simulator.Snapshot();
    }
}
