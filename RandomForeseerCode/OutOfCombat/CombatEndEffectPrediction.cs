using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class CombatEndEffectPrediction
{
    public static void FastForwardMonsterRoomCombatEndHooks(
        RunPredictionContext context)
    {
        // Known deterministic AfterCombatEnd consumers that can affect out-of-combat predictions:
        // Fishing Rod consumes run-level Niche and may upgrade a deck card; Pael's Tooth consumes
        // each owner's Rewards RNG and may add an upgraded card to their deck before rewards resolve.
        foreach (var runPlayer in context.RunState.Players)
        {
            if (!runPlayer.IsActiveForHooks)
            {
                continue;
            }

            var playerContext = context.ForPlayer(runPlayer);

            foreach (var relic in runPlayer.Relics.Where(relic => !relic.IsMelted))
            {
                switch (relic)
                {
                    case FishingRod fishingRod:
                        FastForwardFishingRodAfterCombatEnd(playerContext, fishingRod);
                        break;
                    case PaelsTooth paelsTooth:
                        FastForwardPaelsToothAfterCombatEnd(playerContext, paelsTooth);
                        break;
                }
            }
        }
    }

    private static void FastForwardFishingRodAfterCombatEnd(
        RunPredictionContext context,
        FishingRod fishingRod)
    {
        if ((fishingRod.CombatsSeen + 1) % fishingRod.DynamicVars["Combats"].IntValue != 0)
        {
            return;
        }

        var candidates = context.Deck.Cards
            .Where(card => card.Preview.IsUpgradable)
            .ToList();
        context.SharedRng.Niche.NextItem(candidates)?.Upgrade();
    }

    private static void FastForwardPaelsToothAfterCombatEnd(
        RunPredictionContext context,
        PaelsTooth paelsTooth)
    {
        if (context.Player.Creature.IsDead || paelsTooth.SerializableCards.Count == 0)
        {
            return;
        }

        var serializableCard = context.Rng.Rewards.NextItem(paelsTooth.SerializableCards);
        if (serializableCard == null)
        {
            return;
        }

        var card = CardModel.FromSerializable(serializableCard);
        card.Owner = context.Player;
        context.Deck.Add(PredictedCard.FromGenerated(card).Upgrade());
    }
}
