using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models.Cards;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class PotionGenerationCardMirrors
{
    public static void AlchemizeOnPlay(Alchemize card, CardOnPlayMirrorContext context)
    {
        var potion = PotionFactory.CreateRandomPotionInCombat(
            card.Owner,
            context.Rng.CombatPotionGeneration);

        // Vanilla calls PotionCmd.TryToProcure. The generated potion is already determined, but
        // potion-slot mutation and its hooks are outside the simulator's current state domains.
        context.Simulator.History.PotionGenerated(potion, context.OriginalCard);
    }
}
