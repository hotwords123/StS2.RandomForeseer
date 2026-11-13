using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat.Mirrors.Potions.OnUse;

internal static class EntropicBrewMirrors
{
    public static void OnUse(EntropicBrew _, PotionOnUseMirrorContext context)
    {
        var target = context.TargetPlayer;
        var potions = Generate(target, context.Rng.ForPlayer(target).NonCombatGeneration);

        foreach (var generatedPotion in potions)
        {
            // Potion-slot mutation and procurement hooks are outside the current simulator state domains.
            context.History.PotionGenerated(generatedPotion);
        }
    }

    /// <summary>Generates potion reward models without advancing real RNG or mutating potion slots.</summary>
    /// <param name="target">The player whose potion pool and belt capacity apply.</param>
    /// <param name="rng">A prediction-owned clone of the target player's non-combat-generation RNG.</param>
    public static IReadOnlyList<PotionModel> Generate(Player target, Rng rng)
    {
        // The player may discard existing potions before drinking Entropic Brew, so preserve the existing
        // presentation policy of showing enough future results to fill the entire belt rather than only open slots.
        return PredictionUtils.PredictPotionRewards(target, target.PotionSlots.Count, rng);
    }
}
