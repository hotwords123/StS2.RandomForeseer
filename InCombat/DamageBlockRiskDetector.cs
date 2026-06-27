using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

internal static class DamageBlockRiskDetector
{
    public static PredictionRisk DetectGainBlock(CardModel card)
    {
        if (card.Owner.Creature.CombatState is not { } combatState)
        {
            return PredictionRisk.None;
        }

        var simulator = new CombatPredictionSimulator(combatState);
        simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, cardSource: card);
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
                .FromCard(card)
                .WithHitCount(hitCount));
        return simulator.Snapshot();
    }
}
