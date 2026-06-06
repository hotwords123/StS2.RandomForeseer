using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class OutOfCombatPredictionUtils
{
    public static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int count,
        CardCreationOptions options)
    {
        return PredictCards(
            player,
            count,
            options,
            PredictionUtils.CloneRng(player.PlayerRng.Rewards),
            PredictionUtils.CloneRng(player.RunState.Rng.Niche));
    }

    public static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int count,
        CardCreationOptions options,
        Rng rewardRng,
        Rng nicheRng,
        Action? afterGenerated = null,
        IEnumerable<AbstractModel>? extraResultModifiers = null)
    {
        return CardRewardPrediction.PredictCards(
            player,
            count,
            options,
            rewardRng,
            nicheRng,
            afterGenerated,
            extraResultModifiers);
    }

    public static IReadOnlyList<CardModel> PredictDistinctDeckTransformResults(
        Player player,
        Rng realRng,
        Action<CardModel>? afterGenerated = null,
        int rngCounterOffset = 0,
        IEnumerable<CardModel>? extraTransformableCards = null)
    {
        return PileType.Deck.GetPile(player).Cards
            .Concat(extraTransformableCards ?? [])
            .Where(card => card.Type != CardType.Quest && card.IsTransformable)
            .Select(card =>
            {
                var rng = PredictionUtils.CloneRng(realRng);
                if (rngCounterOffset != 0)
                {
                    rng.FastForwardCounter(rng.Counter + rngCounterOffset);
                }

                var preview = PredictTransformResult(card, rng).ToMutable();
                afterGenerated?.Invoke(preview);
                return preview;
            })
            .DistinctBy(card => card.Id)
            .ToList();
    }

    public static CardModel PredictTransformResult(CardModel original, Rng rng, bool isInCombat = false)
    {
        var options = CardFactory.GetDefaultTransformationOptions(original, isInCombat);
        var result = rng.NextItem(options) ?? throw new InvalidOperationException($"Could not predict a transform result for {original.Id}.");
        return result;
    }

    public static IReadOnlyList<IReadOnlyList<CardModel>> PredictDistinctDeckTransformResultBundles(
        Player player,
        Rng realRng,
        int transformCount,
        Action<CardModel>? afterGenerated = null,
        IEnumerable<CardModel>? extraTransformableCards = null)
    {
        return Enumerable.Range(0, transformCount)
            .Select(slot => PredictDistinctDeckTransformResults(
                player,
                realRng,
                afterGenerated,
                rngCounterOffset: slot,
                extraTransformableCards))
            .ToList();
    }


    public static CardCreationOptions CreateCharacterCardRewardOptions(Player player)
    {
        return new CardCreationOptions(
                [player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.IsCardReward);
    }

    public static IReadOnlyList<IReadOnlyList<CardModel>> PredictCardRewardBundles(
        Player player,
        int rewardCount,
        int optionCount,
        CardCreationOptions options)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);

        return Enumerable.Range(0, rewardCount)
            .Select(_ => PredictCards(player, optionCount, options, rewardRng, nicheRng))
            .ToList();
    }

    public static IReadOnlyList<CardModel> PredictUpgradedDeckCards(
        Player player,
        int count,
        Func<CardModel, bool> filter,
        Rng? rng = null)
    {
        rng ??= PredictionUtils.CloneRng(player.RunState.Rng.Niche);

        return PileType.Deck.GetPile(player).Cards
            .Where(filter)
            .ToList()
            .StableShuffle(rng)
            .Take(count)
            .Select(PredictionUtils.ToUpgradedCard)
            .ToList();
    }

    public static IReadOnlyList<CardModel> PredictUpgradedDeckCardsByNextItem(
        Player player,
        int count,
        Func<CardModel, bool> filter,
        Rng rng)
    {
        var candidates = PileType.Deck.GetPile(player).Cards
            .Where(filter)
            .ToList();
        var cards = new List<CardModel>();

        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var card = rng.NextItem(candidates);
            if (card == null)
            {
                break;
            }

            candidates.Remove(card);
            cards.Add(PredictionUtils.ToUpgradedCard(card));
        }

        return cards;
    }

    public static IReadOnlyList<CardModel> PredictDowngradedDeckCardsByNextItem(
        Player player,
        int count,
        Func<CardModel, bool> filter,
        Rng rng)
    {
        var candidates = PileType.Deck.GetPile(player).Cards
            .Where(filter)
            .ToList();
        var cards = new List<CardModel>();

        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var card = rng.NextItem(candidates);
            if (card == null)
            {
                break;
            }

            candidates.Remove(card);
            var preview = (CardModel)card.MutableClone();
            preview.DowngradeInternal();
            cards.Add(preview);
        }

        return cards;
    }

    public static IReadOnlyList<PotionModel> PredictUniformPotions(
        Player player,
        int count,
        Rng? rng = null,
        Func<PotionModel, bool>? filter = null)
    {
        rng ??= PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        filter ??= (_ => true);

        var options = player.Character.PotionPool
            .GetUnlockedPotions(player.UnlockState)
            .Concat(ModelDb.PotionPool<SharedPotionPool>().GetUnlockedPotions(player.UnlockState))
            .Where(filter)
            .ToList();
        var potions = new List<PotionModel>();

        for (var i = 0; i < count; i++)
        {
            var potion = rng.NextItem(options);
            if (potion == null)
            {
                break;
            }

            potions.Add(potion);
        }

        return potions;
    }

    public static IReadOnlyList<PotionModel> PredictPotionRewards(Player player, int count, Rng rng)
    {
        return Enumerable.Range(0, count)
            .Select(_ => PotionFactory.CreateRandomPotionOutOfCombat(player, rng))
            .ToList();
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count)
    {
        return PredictRelicRewards(player, count, PredictionUtils.CloneRng(player.PlayerRng.Rewards));
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count, Rng rng)
    {
        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var relics = new List<RelicModel>();

        for (var i = 0; i < count; i++)
        {
            var rarity = RelicFactory.RollRarity(rng);
            relics.Add(grabBag.PullFromFront(rarity, player.RunState) ?? RelicFactory.FallbackRelic);
        }

        return relics;
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(
        Player player,
        IEnumerable<RelicRarity> rarities,
        Func<RelicModel, bool>? filter = null)
    {

        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        return rarities
            .Select(rarity => grabBag.PullFromFront(rarity, filter ?? (_ => true), player.RunState) ?? RelicFactory.FallbackRelic)
            .ToList();
    }

    public static IReadOnlyList<IHoverTip> PredictRelicsWithPickup(
        Player player,
        EventOption option,
        IReadOnlyList<RelicModel> relics)
    {
        return option.TextKey.Contains("LOCKED", StringComparison.Ordinal)
            ? []
            : RelicTipsWithPickup(player, relics);
    }

    public static IReadOnlyList<IHoverTip> RelicTipsWithPickup(Player player, IReadOnlyList<RelicModel> relics)
    {
        var tips = PredictionHoverTips.Relics(relics).ToList();
        foreach (var relic in relics)
        {
            tips.AddRange(RelicPickupPrediction.GetHoverTips(player, relic));
        }

        return tips;
    }

    public static void FastForwardBeforeFirstMonsterCardReward(Player player, Rng rewardRng)
    {
        FastForwardBeforeMonsterCardReward(
            player,
            rewardRng,
            GoldReward.defaultMinGoldAmount,
            GoldReward.defaultMaxGoldAmount);
    }

    public static void FastForwardMonsterRoomRewards(
        Player player,
        Rng rewardRng,
        Rng nicheRng,
        int minGoldReward,
        int maxGoldReward)
    {
        // Normal monster rewards are generated before event-specific follow-up rewards.
        FastForwardBeforeMonsterCardReward(player, rewardRng, minGoldReward, maxGoldReward);
        PredictCards(
            player,
            3,
            CardCreationOptions.ForRoom(player, RoomType.Monster),
            rewardRng,
            nicheRng);
    }

    private static void FastForwardBeforeMonsterCardReward(
        Player player,
        Rng rewardRng,
        int minGoldReward,
        int maxGoldReward)
    {
        var forcePotionReward = Hook.ShouldForcePotionReward(player.RunState, player, RoomType.Monster);
        var potionRewardRoll = rewardRng.NextFloat();
        var shouldAddPotionReward = forcePotionReward || potionRewardRoll < player.PlayerOdds.PotionReward.CurrentValue;

        rewardRng.NextInt(minGoldReward, maxGoldReward + 1);
        if (shouldAddPotionReward)
        {
            PotionFactory.CreateRandomPotionOutOfCombat(player, rewardRng);
        }
    }
}
