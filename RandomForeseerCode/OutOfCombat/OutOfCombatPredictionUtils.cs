using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class OutOfCombatPredictionUtils
{
    public static IReadOnlyList<CardModel> PredictDistinctDeckTransformResults(
        Player player,
        Rng realRng,
        bool upgradeResults = false,
        int rngCounterOffset = 0,
        IEnumerable<CardModel>? extraTransformableCards = null)
    {
        return PileType.Deck.GetPile(player).Cards
            .Concat(extraTransformableCards ?? [])
            .Where(card => card.Type != CardType.Quest && card.IsTransformable)
            .Select(card =>
            {
                var rng = realRng.Clone();
                if (rngCounterOffset != 0)
                {
                    rng.FastForwardCounter(rng.Counter + rngCounterOffset);
                }

                return PredictionUtils.PredictTransformResult(card, rng, isInCombat: false);
            })
            .DistinctBy(card => card.Id)
            .Select(card => PredictionUtils.ToUpgradedCardIf(card, upgradeResults))
            .ToList();
    }

    public static IReadOnlyList<IReadOnlyList<CardModel>> PredictDistinctDeckTransformResultBundles(
        Player player,
        Rng realRng,
        int transformCount,
        bool upgradeResults = false,
        IEnumerable<CardModel>? extraTransformableCards = null)
    {
        return Enumerable.Range(0, transformCount)
            .Select(slot => PredictDistinctDeckTransformResults(
                player,
                realRng,
                upgradeResults,
                rngCounterOffset: slot,
                extraTransformableCards))
            .ToList();
    }

    public static void FastForwardDeckTransforms(RunPredictionContext context, int transformCount)
    {
        transformCount = Math.Min(
            transformCount,
            PileType.Deck.GetPile(context.Player).Cards
                .Count(card => card.Type != CardType.Quest && card.IsTransformable));
        context.SharedRng.Niche.FastForwardCounter(context.SharedRng.Niche.Counter + transformCount);
    }

    public static CardCreationOptions CreateCharacterCardRewardOptions(Player player)
    {
        return new CardCreationOptions(
                [player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.IsCardReward);
    }

    public static CardCreationOptions CreateMonsterCombatCardRewardOptions(Player player)
    {
        // StS2 v0.108.0 marks generated combat card rewards with IsFromCombat; Lasting Candy now gates on it.
        return CardCreationOptions.ForRoom(player, RoomType.Monster)
            .WithFlags(CardCreationFlags.IsCardReward | CardCreationFlags.IsFromCombat);
    }

    public static IReadOnlyList<IReadOnlyList<CardModel>> PredictCardRewardBundles(
        Player player,
        int rewardCount,
        int optionCount,
        Func<CardCreationOptions> optionsFactory)
    {
        // TODO: Migrate this to accept RunPredictionContext as a parameter.
        var context = new RunPredictionContext(player);

        return Enumerable.Range(0, rewardCount)
            .Select(_ => CardRewardPrediction.PredictCards(
                context,
                optionCount,
                optionsFactory().WithFlags(CardCreationFlags.IsCardReward)))
            .ToList();
    }

    public static IReadOnlyList<CardModel> PredictUpgradedDeckCards(
        Player player,
        int count,
        Func<CardModel, bool> filter,
        Rng? rng = null)
    {
        rng ??= player.RunState.Rng.Niche.Clone();

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
        rng ??= player.PlayerRng.Rewards.Clone();
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

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count)
    {
        return PredictRelicRewards(player, count, player.PlayerRng.Rewards.Clone());
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count, Rng rng)
    {
        var grabBag = player.RelicGrabBag.Clone();
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
        var grabBag = player.RelicGrabBag.Clone();
        return rarities
            .Select(rarity => grabBag.PullFromFront(rarity, filter ?? (_ => true), player.RunState) ?? RelicFactory.FallbackRelic)
            .ToList();
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(RunPredictionContext context, int count)
    {
        var relics = new List<RelicModel>(count);
        for (var i = 0; i < count; i++)
        {
            var rarity = RelicFactory.RollRarity(context.Rng.Rewards);
            relics.Add(context.RelicGrabBag.PullFromFront(rarity, context.RunState) ?? RelicFactory.FallbackRelic);
        }

        return relics;
    }

    public static IReadOnlyList<IHoverTip> RelicTipsWithPickup(Player player, IReadOnlyList<RelicModel> relics)
    {
        var tips = PredictionHoverTips.Relics(relics).ToList();
        foreach (var relic in relics)
        {
            // TODO: Add a context-based overload once relic pickup prediction is fully
            // migrated to RunPredictionContext, so callers with advanced prediction state
            // can preserve Rewards/Niche/odds/deck changes through pickup effects.
            tips.AddRange(RelicPickupPrediction.GetHoverTips(player, relic));
        }

        return tips;
    }

    public static void FastForwardMonsterRoomRewards(RunPredictionContext context)
    {
        // Normal monster rewards are generated before event-specific follow-up rewards.
        // Mirrors RewardsSet.GenerateWithoutOffering up through the normal CardReward.
        FastForwardBeforeMonsterCardReward(context);

        var options = CreateMonsterCombatCardRewardOptions(context.Player);
        _ = CardRewardPrediction.PredictCards(context, 3, options);
    }

    public static void FastForwardBeforeMonsterCardReward(RunPredictionContext context)
    {
        // Mirrors the pre-card part of RewardsSet.GenerateWithoutOffering for a monster room:
        // GoldReward.Populate, PotionRewardOdds.Roll, and optional PotionReward.Populate.
        // StS2 v0.108.0 made forced potion rewards return from Roll before consuming odds RNG;
        // PotionRewardOdds.Roll owns that branch, while the NextInt below is still the gold roll.
        var shouldAddPotionReward = context.PotionRewardOdds.Roll(
            context.Player,
            RoomType.Monster);

        _ = context.Rng.Rewards.NextInt(1);
        if (shouldAddPotionReward)
        {
            _ = PotionFactory.CreateRandomPotionOutOfCombat(context.Player, context.Rng.Rewards);
        }
    }
}
