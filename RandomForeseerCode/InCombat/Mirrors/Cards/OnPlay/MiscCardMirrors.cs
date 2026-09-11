using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

// Card-specific entry points that do not currently share a dedicated effect group.
internal static class MiscCardMirrors
{
    public static void AlchemizeOnPlay(Alchemize card, CardOnPlayMirrorContext context)
    {
        var potion = PotionFactory.CreateRandomPotionInCombat(
            card.Owner,
            context.Rng.CombatPotionGeneration);

        // Vanilla calls PotionCmd.TryToProcure. The generated potion is already determined, but
        // potion-slot mutation and its hooks are outside the simulator's current state domains.
        context.Simulator.History.PotionGenerated(potion);
    }

    public static void MadScienceOnPlay(MadScience card, CardOnPlayMirrorContext context)
    {
        switch (card)
        {
            case { TinkerTimeType: CardType.Attack, TinkerTimeRider: TinkerTime.RiderEffect.Sapping }:
                VulnerableCardMirrors.MadScienceSappingOnPlay(card, context);
                break;

            case { TinkerTimeType: CardType.Skill, TinkerTimeRider: TinkerTime.RiderEffect.Chaos }:
                CardGenerationCardMirrors.MadScienceChaosOnPlay(card, context);
                break;

            default:
                context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                break;
        }
    }

    public static void SecondWindOnPlay(SecondWind card, CardOnPlayMirrorContext context)
    {
        var cardsToExhaust = context.OwnerState.Hand.Cards
            .Where(predicted => predicted.Preview.Type != CardType.Attack)
            .ToArray();

        foreach (var cardToExhaust in cardsToExhaust)
        {
            context.Simulator.Exhaust(cardToExhaust);
            context.GainBlock(card.Owner.Creature);
        }
    }
}
