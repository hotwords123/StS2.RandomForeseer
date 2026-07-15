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
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class CardRewardPrediction
{
    private static readonly HashSet<MethodInfo> WarnedUnsupportedAfterGeneratedHandlers = [];

    // Convenience wrapper with freshly cloned prediction state; mirrors a standalone CardFactory.CreateForReward call.
    // CardCreationOptions is mutable and may be modified by vanilla hooks during prediction. Callers must pass
    // a caller-owned options instance for each PredictCards call and must not reuse it across predictions.
    public static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int cardCount,
        CardCreationOptions options,
        Action? afterGenerated = null,
        IEnumerable<AbstractModel>? extraResultModifiers = null)
    {
        return PredictCards(
            new RunPredictionContext(player),
            cardCount,
            options,
            afterGenerated,
            extraResultModifiers);
    }

    // Mirrors the shared generation body used by two vanilla call shapes:
    // 1. Direct CardFactory.CreateForReward calls, which use the caller's CardCreationOptions as-is.
    // 2. CardReward.Populate for new CardReward(options, ...), where CardReward first adds IsCardReward.
    // Callers must mirror their vanilla shape and add IsCardReward before calling this helper when needed.
    // CardCreationOptions is mutable and may be modified by vanilla hooks during prediction. Callers must pass
    // a caller-owned options instance for each PredictCards call and must not reuse it across predictions.
    public static IReadOnlyList<CardModel> PredictCards(
        RunPredictionContext context,
        int cardCount,
        CardCreationOptions options,
        Action? afterGenerated = null,
        IEnumerable<AbstractModel>? extraResultModifiers = null)
    {
        var results = CreateBaseRewards(
                context.Player,
                cardCount,
                options,
                context.Rng.Rewards,
                context.CardRarityOdds)
            .ToList();

        ApplyRewardModifiers(context, results, options, afterGenerated, extraResultModifiers);

        return results.Select(result => result.Card).ToList();
    }

    // Mirrors the manually-set CardReward.Populate branch, such as new CardReward(cardsToOffer, ...):
    // the cards already exist, so only the card reward option modifier pass is applied.
    // CardCreationOptions is mutable and may be modified by vanilla hooks during prediction. Callers must pass
    // a caller-owned options instance for each call and must not reuse it across predictions.
    public static IReadOnlyList<CardModel> ApplyRewardModifiersToExistingCards(
        RunPredictionContext context,
        IEnumerable<CardModel> cards,
        CardCreationOptions options,
        Action? afterGenerated = null,
        IEnumerable<AbstractModel>? extraResultModifiers = null)
    {
        var results = cards.Select(card => new CardCreationResult(card)).ToList();
        ApplyRewardModifiers(context, results, options, afterGenerated, extraResultModifiers);
        return results.Select(result => result.Card).ToList();
    }

    // Mirrors Hook.TryModifyCardRewardOptions plus known CardReward.AfterGenerated handlers.
    // It currently does not mirror Hook.AfterModifyingCardRewardOptions yet; one-shot/usage state
    // such as Silken Tress and Silver Crucible needs a fuller prediction context.
    private static void ApplyRewardModifiers(
        RunPredictionContext context,
        List<CardCreationResult> results,
        CardCreationOptions options,
        Action? afterGenerated = null,
        IEnumerable<AbstractModel>? extraResultModifiers = null)
    {
        _ = HookMirrors.TryModifyCardRewardOptions(
            context,
            results,
            options,
            out _,
            extraResultModifiers);
        // TODO: Run Hook.AfterModifyingCardRewardOptions
        ApplyKnownAfterGeneratedModifiers(afterGenerated, results);
    }

    // Creates a CardCreationOptions copy for callers that need to adapt an existing
    // vanilla-owned options instance before passing it into a prediction method.
    public static CardCreationOptions CloneOptions(CardCreationOptions options)
    {
        // StS2 v0.108.0 removed custom card pools from CardCreationOptions. Vanilla now represents
        // all narrowed pools as source card pools plus a CardPoolFilter so later pool modifiers can still apply.
        var clone = new CardCreationOptions(
            options.CardPools.ToArray(),
            options.Source,
            options.RarityOdds,
            options.CardPoolFilter);

        clone.WithFlags(options.Flags);
        if (options.RngOverride != null)
        {
            clone.WithRngOverride(options.RngOverride.Clone());
        }

        return clone;
    }

    // Mirrors the public CardFactory.CreateForReward loop before Hook.TryModifyCardRewardOptions.
    // The supplied CardCreationOptions follows the containing prediction call's fresh-instance contract.
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

    // Mirrors CardFactory.CreateForReward's private single-card helper, using PredictionUtils.CreateCard
    // instead of RunState.CreateCard so previews do not enter run state.
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
                    $"Could not predict a valid card reward rarity. Odds: {options.RarityOdds}, " +
                    $"card pool: {string.Join(",", filteredCards.Select(card => card.Id))}");
            }

            candidates = filteredCards.Where(card => card.Rarity == selectedRarity);
        }

        var canonical = (options.RngOverride ?? rewardRng).NextItem(candidates);
        if (canonical == null)
        {
            throw new InvalidOperationException(
                $"Could not predict a valid card reward. Selected rarity: {selectedRarity}, " +
                $"card pool: {string.Join(",", filteredCards.Select(card => card.Id))}");
        }

        return PredictionUtils.CreateCard(canonical, player);
    }

    // Mirrors CardFactory.RollForRarity, including the encounter/ForceRarityOddsChange cases
    // that advance the shared CardRarityOdds state.
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

    // Mirrors CardFactory.GetNextAllowedRarity.
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

    // Mirrors CardFactory.RollForUpgrade, mutating only the preview card.
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

    // Mirrors known CardReward.AfterGenerated callbacks. Currently only TheFutureOfPotions' local
    // UpgradeCardsInReward handler is safe to identify and reproduce.
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

    // Identifies TheFutureOfPotions.Trade's local UpgradeCardsInReward handler without depending on compiler names.
    private static bool IsTheFutureOfPotionsUpgradeCardsInReward(Delegate handler)
    {
        var declaringTypeName = handler.Method.DeclaringType?.FullName ?? string.Empty;
        var targetTypeName = handler.Target?.GetType().FullName ?? string.Empty;

        return handler.Method.Name.Contains("UpgradeCardsInReward", StringComparison.Ordinal) &&
            (declaringTypeName.Contains("TheFutureOfPotions", StringComparison.Ordinal) ||
                targetTypeName.Contains("TheFutureOfPotions", StringComparison.Ordinal));
    }

    // Mirrors TheFutureOfPotions' UpgradeCardsInReward callback.
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
