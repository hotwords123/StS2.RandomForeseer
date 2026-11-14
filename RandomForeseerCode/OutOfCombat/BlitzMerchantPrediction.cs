using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Blitz;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.Common.HoverTips;
using RandomForeseer.Data;

namespace RandomForeseer.OutOfCombat;

/// <summary>
/// Previews the immediate rewards of selecting a <see cref="BlitzMerchantEntry"/>.
/// </summary>
internal static class BlitzMerchantPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(BlitzMerchantEntry entry)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.BlitzMerchantPredictionEnabled ||
            entry.Status != BlitzMerchantEntry.BlitzEntryStatus.Available ||
            entry.Player.RunState is not { GameMode: GameMode.Blitz, CurrentRoom: MerchantRoom } ||
            RunManager.Instance.MerchantSynchronizer.GetInventoryFor(entry.Player).BlitzChoicesRemaining <= 0)
        {
            return [];
        }

        var context = new RunPredictionContext(entry.Player);
        return [.. Predict(entry, context).SelectMany(ToHoverTips)];
    }

    /// <summary>
    /// Mirrors the immediate reward generation in the Blitz merchant entries' OnTrySelect methods.
    /// Does not resolve reward claims or player-selected deck changes.
    /// </summary>
    internal static IReadOnlyList<BlitzMerchantRewardPrediction> Predict(
        BlitzMerchantEntry entry,
        RunPredictionContext context)
    {
        List<BlitzMerchantRewardPrediction> rewards = [];
        if (entry is BlitzMerchantGlamEntry &&
            !context.Player.Deck.Cards.Any(ModelDb.Enchantment<Glam>().CanEnchant))
        {
            return rewards;
        }

        if (entry.IsCursed)
        {
            rewards.Add(new BlitzMerchantRewardPrediction.Card(PredictCurse(context)));
        }

        switch (entry)
        {
            case BlitzMerchantCardRewardsEntry:
                for (var i = 0; i < 2; i++)
                {
                    AddCardReward(context, rewards, CreateOptions(context));
                }

                break;

            case BlitzMerchantUpgradedCardRewardEntry:
                AddCardReward(
                    context,
                    rewards,
                    CreateOptions(context).WithFlags(CardCreationFlags.NoUpgrades),
                    afterGenerated: PredictionUtils.UpgradeCard);
                break;

            case BlitzMerchantColorlessCardRewardEntry:
                AddCardReward(context, rewards, CreateOptions(context, ModelDb.CardPool<ColorlessCardPool>()));
                break;

            case BlitzMerchantEnchantedCardRewardEntry:
                AddCardReward(
                    context,
                    rewards,
                    CreateOptions(context),
                    afterGenerated: card => EnchantCard(context, card));
                break;

            case BlitzMerchantPrismaticCardRewardEntry prismatic:
                AddCardReward(context, rewards, CreateOptions(context, prismatic._character!.CardPool));
                break;

            case BlitzMerchantRareCardRewardEntry:
                AddCardReward(
                    context,
                    rewards,
                    CreateOptions(context).WithFilter(card => card.Rarity == CardRarity.Rare));
                break;

            case BlitzMerchantUpgradedRareCardRewardEntry:
                AddCardReward(
                    context,
                    rewards,
                    CreateOptions(context).WithFilter(card => card.Rarity == CardRarity.Rare),
                    afterGenerated: PredictionUtils.UpgradeCard);
                break;

            case BlitzMerchantCardRewardOfEachRarityEntry:
                foreach (var rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
                {
                    var options = CardCreationOptions.ForNonCombatWithUniformOdds(
                            [context.Player.Character.CardPool],
                            card => card.Rarity == rarity)
                        .WithFlags(CardCreationFlags.NoRarityModification);
                    AddCardReward(context, rewards, options);
                }

                break;

            case BlitzMerchantUpgradedRareCardEntry:
                // Vanilla calls CardFactory directly, without CardReward's IsCardReward flag. Its factory registers
                // cards in the live RunState; CardRewardPrediction creates detached previews instead.
                var rareOptions = CardCreationOptions.ForNonCombatWithUniformOdds(
                        [context.Player.Character.CardPool],
                        card => card.Rarity == CardRarity.Rare)
                    .WithRngOverride(context.Rng.Shops);
                var rareCard = CardRewardPrediction.PredictCards(context, 1, rareOptions)[0];
                PredictionUtils.UpgradeCard(rareCard);
                rewards.Add(new BlitzMerchantRewardPrediction.Card(rareCard));
                break;

            case BlitzMerchantRandomCardUpgradesEntry:
                var upgradedCards = OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    context.Player,
                    2,
                    card => card.IsUpgradable,
                    context.Rng.Shops);
                rewards.AddRange(upgradedCards.Select(card => new BlitzMerchantRewardPrediction.Card(card)));
                break;

            case BlitzMerchantCardTransformEntry:
                var transformations = OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(
                    context.Player,
                    context.Rng.Transformations);
                rewards.Add(new BlitzMerchantRewardPrediction.Transform(transformations));
                break;

            case BlitzMerchantPotionsEntry:
                // Separate PotionReward instances each generate one potion, allowing duplicates across rewards.
                for (var i = 0; i < 2; i++)
                {
                    var potion = PotionFactory.CreateRandomPotionOutOfCombat(context.Player, context.Rng.Shops);
                    rewards.Add(new BlitzMerchantRewardPrediction.Potion(potion));
                }

                break;

            case BlitzMerchantRelicEntry:
                AddRelic(context, rewards);
                break;

            case BlitzMerchantTwoRelicsEntry:
                AddRelic(context, rewards);
                AddRelic(context, rewards);
                break;
        }

        return rewards;
    }

    /// <summary>
    /// Mirrors <see cref="BlitzMerchantEntry.GiveCurse"/> without adding the curse to the live deck.
    /// </summary>
    /// <remarks>
    /// Vanilla Blitz additionally filters curses by IsAllowedInRun; the vanilla sources mirrored by
    /// <see cref="OutOfCombatPredictionUtils.PredictCurses"/> only filter by CanBeGeneratedByModifiers.
    /// </remarks>
    private static CardModel PredictCurse(RunPredictionContext context)
    {
        var player = context.Player;
        var candidates = context.Player.GetUnlockedCurseCards()
            .Where(card => card.CanBeGeneratedByModifiers && card.IsAllowedInRun(context.RunState))
            .OrderBy(card => card.Id);
        var curse = context.Rng.Shops.NextItem(candidates)
                    ?? throw new InvalidOperationException("Could not predict a Blitz merchant curse.");
        return PredictionUtils.CreateCard(curse, player);
    }

    private static CardCreationOptions CreateOptions(RunPredictionContext context, CardPoolModel? pool = null)
    {
        return new CardCreationOptions(
                [pool ?? context.Player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.Blitz)
            .WithRngOverride(context.Rng.Shops);
    }

    private static void AddCardReward(
        RunPredictionContext context,
        List<BlitzMerchantRewardPrediction> rewards,
        CardCreationOptions options,
        Action<CardModel>? afterGenerated = null)
    {
        var cards = CardRewardPrediction.PredictCards(context, 3, options.WithFlags(CardCreationFlags.IsCardReward));

        // Mirrors the entries' CardReward.AfterGenerated handlers, after card reward hooks. Running the original
        // handlers would create live enchantment/upgrade events, so only modify detached prediction cards here.
        if (afterGenerated != null)
        {
            foreach (var card in cards)
            {
                afterGenerated(card);
            }
        }

        rewards.Add(new BlitzMerchantRewardPrediction.CardReward(cards));
    }

    private static void EnchantCard(RunPredictionContext context, CardModel card)
    {
        if (card.Enchantment is not null)
        {
            return;
        }

        var (enchantment, amount) = context.Rng.Shops.NextItem(
            BlitzRunModel.shopEnchantmentOptions.Where(option => option.Item1.CanEnchant(card)));
        PredictionUtils.EnchantCard(enchantment.ToMutable(), card, amount);
    }

    private static void AddRelic(RunPredictionContext context, List<BlitzMerchantRewardPrediction> rewards)
    {
        var rarity = RelicFactory.RollRarity(context.Rng.Shops);
        var relic = context.RelicGrabBag.PullFromBack(rarity, _ => true, context.RunState)
                    ?? RelicFactory.FallbackRelic;
        rewards.Add(new BlitzMerchantRewardPrediction.Relic(relic));
    }

    private static IEnumerable<IHoverTip> ToHoverTips(BlitzMerchantRewardPrediction reward)
    {
        return reward switch
        {
            BlitzMerchantRewardPrediction.Card card => [PredictionHoverTipFactory.Card(card.Model)],
            BlitzMerchantRewardPrediction.CardReward { Cards.Count: > 0 } cards =>
                [PredictionHoverTipFactory.CardBundle(cards.Cards)],
            BlitzMerchantRewardPrediction.Transform { Cards.Count: > 0 } transform =>
                [PredictionHoverTipFactory.CardBundle(transform.Cards, PredictionCardBundleKind.Transform)],
            BlitzMerchantRewardPrediction.Potion potion => [PredictionHoverTipFactory.Potion(potion.Model)],
            BlitzMerchantRewardPrediction.Relic relic => [PredictionHoverTipFactory.Relic(relic.Model)],
            _ => []
        };
    }
}

/// <summary>Preserves reward boundaries independently of HoverTip presentation.</summary>
internal abstract record BlitzMerchantRewardPrediction
{
    internal sealed record Card(CardModel Model) : BlitzMerchantRewardPrediction;

    internal sealed record CardReward(IReadOnlyList<CardModel> Cards) : BlitzMerchantRewardPrediction;

    internal sealed record Transform(IReadOnlyList<CardModel> Cards) : BlitzMerchantRewardPrediction;

    internal sealed record Potion(PotionModel Model) : BlitzMerchantRewardPrediction;

    internal sealed record Relic(RelicModel Model) : BlitzMerchantRewardPrediction;
}
