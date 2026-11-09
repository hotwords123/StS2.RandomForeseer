using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Potions;

namespace RandomForeseer.InCombat.Mirrors.Potions.OnUse;

internal static class AutoPlayPotionMirrors
{
    public static void DistilledChaosOnUse(DistilledChaos potion, PotionOnUseMirrorContext context)
    {
        context.Simulator.AutoPlayFromDrawPile(
            context.TargetPlayer,
            potion.DynamicVars.Repeat.IntValue,
            CardPilePosition.Top);
    }
}
