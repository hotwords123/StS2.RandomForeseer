using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.OutOfCombat.Hooks;

namespace RandomForeseer.OutOfCombat;

internal static class CardRewardPrediction
{
    private static readonly HashSet<MethodInfo> WarnedUnsupportedAfterGeneratedHandlers = [];

    public static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int cardCount,
        CardCreationOptions options,
        Rng rewardRng,
        Rng nicheRng,
        Action? afterGenerated = null,
        IEnumerable<AbstractModel>? extraResultModifiers = null)
    {
        var rarityOdds = new CardRarityOdds(player.PlayerOdds.CardRarity.CurrentValue, rewardRng);
        options = CloneOptions(options);

        var results = CreateBaseRewards(player, cardCount, options, rewardRng, rarityOdds).ToList();
        var hookContext = new CardRewardHookContext
        {
            Player = player,
            Results = results,
            Options = options,
            RewardRng = rewardRng,
            NicheRng = nicheRng,
            RarityOdds = rarityOdds,
            ExtraModifiers = extraResultModifiers?.ToList() ?? []
        };

        CardRewardHook.RunEarly(hookContext);
        CardRewardHook.RunLate(hookContext);
        ApplyKnownAfterGeneratedModifiers(afterGenerated, results);

        return results.Select(result => result.Card).ToList();
    }

    public static CardCreationOptions CloneOptions(CardCreationOptions options)
    {
        var clone = options.CustomCardPool != null
            ? new CardCreationOptions(options.CustomCardPool.ToArray(), options.Source, options.RarityOdds)
            : new CardCreationOptions(options.CardPools.ToArray(), options.Source, options.RarityOdds, options.CardPoolFilter);

        clone.WithFlags(options.Flags);
        if (options.RngOverride != null)
        {
            clone.WithRngOverride(PredictionUtils.CloneRng(options.RngOverride));
        }

        return clone;
    }

    internal static IEnumerable<CardCreationResult> CreateBaseRewards(
        Player player,
        int cardCount,
        CardCreationOptions options,
        Rng rewardRng,
        CardRarityOdds rarityOdds)
    {
        var blacklist = new List<CardModel>();

        for (var i = 0; i < cardCount; i++)
        {
            var card = CreateForReward(player, blacklist, options, rewardRng, rarityOdds);
            blacklist.Add(card.CanonicalInstance);

            if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll))
            {
                RollForUpgrade(player, card, 0m, options.RngOverride ?? rewardRng);
            }

            yield return new CardCreationResult(card);
        }
    }

    private static CardModel CreateForReward(
        Player player,
        IEnumerable<CardModel> blacklist,
        CardCreationOptions options,
        Rng rewardRng,
        CardRarityOdds rarityOdds)
    {
        options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);

        var possibleCards = options.GetPossibleCards(player)
            .Except(blacklist)
            .ToList();
        var filteredCards = CardFactory.FilterForPlayerCount(player.RunState, possibleCards).ToArray();
        var selectedRarity = (CardRarity?)null;

        IEnumerable<CardModel> candidates;
        if (options.RarityOdds == CardRarityOddsType.Uniform)
        {
            candidates = filteredCards.Where(card => card.Rarity is not CardRarity.Basic and not CardRarity.Ancient);
        }
        else
        {
            var allowedRarities = filteredCards.Select(card => card.Rarity).ToHashSet();
            selectedRarity = RollForRarity(
                rarityOdds,
                options.RarityOdds,
                options.Source,
                allowedRarities,
                options.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange));
            if (selectedRarity == CardRarity.None)
            {
                throw new InvalidOperationException(
                    $"Could not predict a valid card reward rarity. Odds: {options.RarityOdds}, card pool: {string.Join(",", filteredCards.Select(card => card.Id))}");
            }

            candidates = filteredCards.Where(card => card.Rarity == selectedRarity);
        }

        var canonical = (options.RngOverride ?? rewardRng).NextItem(candidates);
        if (canonical == null)
        {
            throw new InvalidOperationException(
                $"Could not predict a valid card reward. Selected rarity: {selectedRarity}, card pool: {string.Join(",", filteredCards.Select(card => card.Id))}");
        }

        return canonical.ToMutable();
    }

    private static CardRarity RollForRarity(
        CardRarityOdds rarityOdds,
        CardRarityOddsType rollMethod,
        CardCreationSource source,
        HashSet<CardRarity> allowedRarities,
        bool forceRarityOddsChange)
    {
        var shouldChangeOdds =
            forceRarityOddsChange ||
            source == CardCreationSource.Encounter &&
            rollMethod is CardRarityOddsType.RegularEncounter or CardRarityOddsType.EliteEncounter or CardRarityOddsType.BossEncounter;
        var rarity = shouldChangeOdds
            ? rarityOdds.Roll(rollMethod)
            : rarityOdds.RollWithBaseOdds(rollMethod);

        return GetNextAllowedRarity(rarity, allowedRarities.Contains);
    }

    private static CardRarity GetNextAllowedRarity(CardRarity rarity, Func<CardRarity, bool> isAllowed)
    {
        var firstRarity = rarity;
        while (!isAllowed(rarity) && rarity != CardRarity.None)
        {
            rarity = rarity.GetNextHighestRarityWithWrapping();
            if (rarity == firstRarity)
            {
                return CardRarity.None;
            }
        }

        return rarity;
    }

    private static void RollForUpgrade(Player player, CardModel card, decimal baseChance, Rng rng)
    {
        var roll = (decimal)rng.NextFloat();
        if (!card.IsUpgradable)
        {
            return;
        }

        var originalOdds = baseChance;
        if (card.Rarity != CardRarity.Rare)
        {
            originalOdds += (decimal)player.RunState.CurrentActIndex *
                (decimal)AscensionHelper.GetValueIfAscension(AscensionLevel.Scarcity, 0.125m, 0.25m);
        }

        originalOdds = Hook.ModifyCardRewardUpgradeOdds(player.RunState, player, card, originalOdds);
        if (roll <= originalOdds)
        {
            PredictionUtils.UpgradeCardInPlace(card);
        }
    }

    private static void ApplyKnownAfterGeneratedModifiers(Action? afterGenerated, List<CardCreationResult> results)
    {
        if (afterGenerated == null)
        {
            return;
        }

        foreach (var handler in afterGenerated.GetInvocationList())
        {
            if (IsTheFutureOfPotionsUpgradeCardsInReward(handler))
            {
                UpgradeAllValidCards(results);
            }
            else if (WarnedUnsupportedAfterGeneratedHandlers.Add(handler.Method))
            {
                Entry.Logger.Warn(
                    $"Card reward prediction does not safely mirror CardReward.AfterGenerated handler {handler.Method.DeclaringType?.FullName}.{handler.Method.Name}; preview may omit that modifier.");
            }
        }
    }

    private static bool IsTheFutureOfPotionsUpgradeCardsInReward(Delegate handler)
    {
        var declaringTypeName = handler.Method.DeclaringType?.FullName ?? string.Empty;
        var targetTypeName = handler.Target?.GetType().FullName ?? string.Empty;

        return handler.Method.Name.Contains("UpgradeCardsInReward", StringComparison.Ordinal) &&
            (declaringTypeName.Contains("TheFutureOfPotions", StringComparison.Ordinal) ||
                targetTypeName.Contains("TheFutureOfPotions", StringComparison.Ordinal));
    }

    private static void UpgradeAllValidCards(List<CardCreationResult> results)
    {
        foreach (var result in results)
        {
            if (result.Card.IsUpgradable)
            {
                var upgradedCard = PredictionUtils.ToUpgradedCard(result.Card);
                result.ModifyCard(upgradedCard);
            }
        }
    }
}
