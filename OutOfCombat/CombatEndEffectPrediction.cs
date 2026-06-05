using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class CombatEndEffectPrediction
{
    public static void FastForwardMonsterRoomCombatEndHooks(
        Player targetPlayer,
        Rng? rewardRng = null,
        Rng? nicheRng = null,
        IList<CardModel>? targetPlayerDeckState = null)
    {
        // Known deterministic AfterCombatEnd consumers that can affect out-of-combat predictions:
        // Fishing Rod consumes Niche and may upgrade a deck card; Pael's Tooth consumes the target
        // player's Rewards RNG and may add an upgraded card to their deck before event rewards resolve.
        var targetRewardRng = rewardRng
            ?? (targetPlayerDeckState != null ? PredictionUtils.CloneRng(targetPlayer.PlayerRng.Rewards) : null);
        foreach (var runPlayer in targetPlayer.RunState.Players)
        {
            FastForwardPlayerMonsterRoomCombatEndHooks(
                runPlayer,
                runPlayer == targetPlayer ? targetRewardRng : null,
                nicheRng,
                runPlayer == targetPlayer ? targetPlayerDeckState : null);
        }
    }

    private static void FastForwardPlayerMonsterRoomCombatEndHooks(
        Player player,
        Rng? rewardRng,
        Rng? nicheRng,
        IList<CardModel>? deckState)
    {
        if (!player.IsActiveForHooks)
        {
            return;
        }

        foreach (var relic in player.Relics.Where(relic => !relic.IsMelted))
        {
            switch (relic)
            {
                case FishingRod fishingRod:
                    FastForwardFishingRodAfterCombatEnd(player, fishingRod, nicheRng, deckState);
                    break;
                case PaelsTooth paelsTooth when rewardRng != null:
                    FastForwardPaelsToothAfterCombatEnd(player, paelsTooth, rewardRng, deckState);
                    break;
            }
        }
    }

    private static void FastForwardFishingRodAfterCombatEnd(
        Player player,
        FishingRod fishingRod,
        Rng? nicheRng,
        IList<CardModel>? deckState)
    {
        if (nicheRng == null ||
            (fishingRod.CombatsSeen + 1) % fishingRod.DynamicVars["Combats"].IntValue != 0)
        {
            return;
        }

        IEnumerable<CardModel> deckCards = deckState != null
            ? deckState
            : PileType.Deck.GetPile(player).Cards;
        var candidates = deckCards.Where(card => card.IsUpgradable).ToList();
        var card = nicheRng.NextItem(candidates);
        if (card != null && deckState != null)
        {
            PredictionUtils.UpgradePreviewCardInPlace(card);
        }
    }

    private static void FastForwardPaelsToothAfterCombatEnd(
        Player player,
        PaelsTooth paelsTooth,
        Rng rewardRng,
        IList<CardModel>? deckState)
    {
        if (player.Creature.IsDead || paelsTooth.SerializableCards.Count == 0)
        {
            return;
        }

        var serializableCard = rewardRng.NextItem(paelsTooth.SerializableCards);
        if (serializableCard == null || deckState == null)
        {
            return;
        }

        var card = CardModel.FromSerializable(serializableCard);
        card.Owner = player;
        PredictionUtils.UpgradePreviewCardInPlace(card);
        deckState.Add(card);
    }
}
