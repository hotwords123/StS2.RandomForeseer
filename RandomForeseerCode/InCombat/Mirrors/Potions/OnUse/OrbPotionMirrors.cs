using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Potions;

namespace RandomForeseer.InCombat.Mirrors.Potions.OnUse;

internal static class OrbPotionMirrors
{
    public static void EssenceOfDarknessOnUse(EssenceOfDarkness _, PotionOnUseMirrorContext context)
    {
        var target = context.TargetPlayer;
        var capacity = context.State.GetPlayerCombatState(target).OrbQueue.Capacity;
        context.Simulator.OrbChannel<DarkOrb>(target, capacity);
    }
}
