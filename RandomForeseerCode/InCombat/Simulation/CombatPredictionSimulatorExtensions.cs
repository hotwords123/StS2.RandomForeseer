using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class CombatPredictionSimulatorExtensions
{
    // Convenience extension method to simulate an attack command.
    public static void Simulate(this AttackCommand attackCommand, CombatPredictionSimulator simulator)
    {
        simulator.ExecuteAttack(attackCommand);
    }

    // Convenience extension method to simulate a single-targeted attack command.
    public static bool TrySimulateTargetedAttack(
        this CombatPredictionSimulator simulator,
        PredictedCard source,
        Creature? target,
        int hitCount = 1)
    {
        if (target is null || !source.Preview.CanPlayTargeting(target))
        {
            return false;
        }

        DamageCmd.Attack(source.Preview.DynamicVars.Damage.BaseValue)
            .FromCard(source.Preview, null)
            .WithHitCount(hitCount)
            .Targeting(target)
            .Simulate(simulator);

        return true;
    }

    // Exhausts the player's hand.
    // Used by Glowwater Potion and Pael's Eye to mirror their effects.
    public static void ExhaustHand(this CombatPredictionSimulator simulator, Player player)
    {
        var cards = simulator.State.GetPlayerCombatState(player).Hand.Cards.ToArray();
        foreach (var card in cards)
        {
            simulator.Exhaust(card);
        }
    }

    // Moves all cards in the player's hand to the draw pile.
    // Used by Bottled Potential and Reboot to mirror their effects.
    public static void MoveHandToDrawPile(this CombatPredictionSimulator simulator, Player player)
    {
        var cards = simulator.State.GetPlayerCombatState(player).Hand.Cards.ToArray();
        simulator.AddToPile(cards, PileType.Draw);
    }
}
