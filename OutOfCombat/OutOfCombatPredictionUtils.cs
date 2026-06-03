using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Random;
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

                var preview = PredictTransformResult(card, rng);
                afterGenerated?.Invoke(preview);
                return preview;
            })
            .DistinctBy(card => card.Id)
            .ToList();
    }

    public static CardModel PredictTransformResult(CardModel original, Rng rng, bool isInCombat = false)
    {
        var options = CardFactory.GetDefaultTransformationOptions(original, isInCombat);
        var canonical = rng.NextItem(options) ?? throw new InvalidOperationException($"Could not predict a transform result for {original.Id}.");
        return PredictionUtils.CreatePreviewCard(canonical, original.Owner);
    }

    public static IReadOnlyList<IReadOnlyList<CardModel>> PredictDistinctDeckTransformResultBundles(
        Player player,
        Rng realRng,
        int transformCount,
        Action<CardModel>? afterGenerated = null,
        IEnumerable<CardModel>? extraTransformableCards = null)
    {
        return Enumerable.Range(0, transformCount)
            .Select(slot => (IReadOnlyList<CardModel>)PredictDistinctDeckTransformResults(
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
            .Select(_ => (IReadOnlyList<CardModel>)PredictCards(player, optionCount, options, rewardRng, nicheRng))
            .ToList();
    }

    public static IReadOnlyList<CardModel> PredictUpgradedDeckCards(
        Player player,
        int count,
        Func<CardModel, bool> filter)
    {
        return PredictUpgradedDeckCards(player, count, filter, player.RunState.Rng.Niche);
    }

    public static IReadOnlyList<CardModel> PredictUpgradedDeckCards(
        Player player,
        int count,
        Func<CardModel, bool> filter,
        Rng realRng)
    {
        var rng = PredictionUtils.CloneRng(realRng);
        return PileType.Deck.GetPile(player).Cards
            .Where(filter)
            .ToList()
            .StableShuffle(rng)
            .Take(count)
            .Select(PredictionUtils.ToUpgradedPreviewCard)
            .ToList();
    }

    public static IReadOnlyList<CardModel> PredictUpgradedDeckCardsByNextItem(
        Player player,
        int count,
        Func<CardModel, bool> filter,
        Rng realRng)
    {
        var rng = PredictionUtils.CloneRng(realRng);
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
            cards.Add(PredictionUtils.ToUpgradedPreviewCard(card));
        }

        return cards;
    }

    public static IReadOnlyList<CardModel> PredictDowngradedDeckCardsByNextItem(
        Player player,
        int count,
        Func<CardModel, bool> filter,
        Rng realRng)
    {
        var rng = PredictionUtils.CloneRng(realRng);
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

    public static IReadOnlyList<PotionModel> PredictPotions(Player player, int count, Rng rng)
    {
        return PotionFactory.CreateRandomPotionsOutOfCombat(player, count, PredictionUtils.CloneRng(rng))
            .Select(potion => potion.ToMutable())
            .ToList();
    }

    public static IReadOnlyList<PotionModel> PredictPotionRewards(Player player, int count, Rng rng)
    {
        var previewRng = PredictionUtils.CloneRng(rng);
        return Enumerable.Range(0, count)
            .Select(_ => PotionFactory.CreateRandomPotionOutOfCombat(player, previewRng).ToMutable())
            .ToList();
    }

    public static IReadOnlyList<PotionModel> PredictUniformPotions(Player player, int count, Rng rng)
    {
        return PredictUniformPotions(player, count, rng, potion => true);
    }

    public static IReadOnlyList<PotionModel> PredictUniformPotions(
        Player player,
        int count,
        Rng rng,
        Func<PotionModel, bool> filter)
    {
        var previewRng = PredictionUtils.CloneRng(rng);
        var options = player.Character.PotionPool
            .GetUnlockedPotions(player.UnlockState)
            .Concat(ModelDb.PotionPool<SharedPotionPool>().GetUnlockedPotions(player.UnlockState))
            .Where(filter)
            .ToList();
        var potions = new List<PotionModel>();

        for (var i = 0; i < count; i++)
        {
            var potion = previewRng.NextItem(options);
            if (potion == null)
            {
                break;
            }

            potions.Add(potion.ToMutable());
        }

        return potions;
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count)
    {
        return PredictRelicRewards(player, count, player.PlayerRng.Rewards);
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count, Rng rng)
    {
        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var relics = new List<RelicModel>();
        var previewRng = PredictionUtils.CloneRng(rng);

        for (var i = 0; i < count; i++)
        {
            var rarity = RelicFactory.RollRarity(previewRng);
            relics.Add((grabBag.PullFromFront(rarity, player.RunState) ?? RelicFactory.FallbackRelic).ToMutable());
        }

        return relics;
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(
        Player player,
        int count,
        Rng rng,
        Func<RelicModel, bool> filter)
    {
        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var relics = new List<RelicModel>();
        var previewRng = PredictionUtils.CloneRng(rng);

        for (var i = 0; i < count; i++)
        {
            var rarity = RelicFactory.RollRarity(previewRng);
            relics.Add((grabBag.PullFromFront(rarity, filter, player.RunState) ?? RelicFactory.FallbackRelic).ToMutable());
        }

        return relics;
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(
        Player player,
        IEnumerable<RelicRarity> rarities,
        Func<RelicModel, bool> filter)
    {
        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        return rarities
            .Select(rarity => (grabBag.PullFromFront(rarity, filter, player.RunState) ?? RelicFactory.FallbackRelic).ToMutable())
            .ToList();
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, IEnumerable<RelicRarity> rarities)
    {
        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        return rarities
            .Select(rarity => (grabBag.PullFromFront(rarity, player.RunState) ?? RelicFactory.FallbackRelic).ToMutable())
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
            tips.AddRange(OutOfCombatRelicPrediction.GetHoverTips(player, relic));
        }

        return tips;
    }
}
