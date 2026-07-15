using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class AutoPlayCardMirrors
{
    public static void HowlFromBeyondOnPlay(HowlFromBeyond card, CardOnPlayMirrorContext context)
    {
        DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
            .FromCard(card, context.CardPlay)
            .TargetingAllOpponents(context.CombatState)
            .Simulate(context.Simulator);
    }

    public static void IAmInvincibleOnPlay(IAmInvincible card, CardOnPlayMirrorContext context)
    {
        context.Simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, context.Card);
    }
}
