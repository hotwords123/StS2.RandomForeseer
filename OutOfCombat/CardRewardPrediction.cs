using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class CardRewardPrediction
{
    private static readonly HashSet<Type> WarnedUnsupportedModifierTypes = [];

    private static readonly HashSet<MethodInfo> WarnedUnsupportedAfterGeneratedHandlers = [];

    public static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int cardCount,
        CardCreationOptions options,
        Rng rewardRng,
        Rng nicheRng,
        Action? afterGenerated = null)
    {
        var rarityOdds = new CardRarityOdds(player.PlayerOdds.CardRarity.CurrentValue, rewardRng);
        options = CloneOptions(options);

        var results = CreateBaseRewards(player, cardCount, options, rewardRng, rarityOdds).ToList();
        ApplyEarlyKnownModifiers(player, results, options, rewardRng, rarityOdds);
        WarnAboutUnsupportedModifiers(player, early: true);
        ApplyLateKnownModifiers(player, results, options, nicheRng);
        WarnAboutUnsupportedModifiers(player, early: false);
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

    private static IEnumerable<CardCreationResult> CreateBaseRewards(
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
        var filteredCards = FilterForPlayerCount(player, possibleCards).ToArray();
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

        return PredictionUtils.CreatePreviewCard(canonical, player);
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
            PredictionUtils.UpgradePreviewCardInPlace(card);
        }
    }

    private static void ApplyEarlyKnownModifiers(
        Player player,
        List<CardCreationResult> results,
        CardCreationOptions options,
        Rng rewardRng,
        CardRarityOdds rarityOdds)
    {
        foreach (var modifier in player.RunState.IterateHookListeners(null))
        {
            if (modifier is LastingCandy lastingCandy)
            {
                ApplyLastingCandy(lastingCandy, player, results, options, rewardRng, rarityOdds);
            }
        }
    }

    private static void ApplyLateKnownModifiers(
        Player player,
        List<CardCreationResult> results,
        CardCreationOptions options,
        Rng nicheRng)
    {
        foreach (var modifier in player.RunState.IterateHookListeners(null))
        {
            switch (modifier)
            {
                case FrozenEgg frozenEgg:
                    UpgradeCardsByType(frozenEgg, player, results, options, CardType.Power);
                    break;
                case MoltenEgg moltenEgg:
                    UpgradeCardsByType(moltenEgg, player, results, options, CardType.Attack);
                    break;
                case ToxicEgg toxicEgg:
                    UpgradeCardsByType(toxicEgg, player, results, options, CardType.Skill);
                    break;
                case SilverCrucible silverCrucible:
                    UpgradeAllCards(silverCrucible, player, results, options);
                    break;
                case LavaLamp lavaLamp:
                    ApplyLavaLamp(lavaLamp, player, results);
                    break;
                case Glitter glitter:
                    EnchantAllValid<Glam>(glitter, player, results, 1m);
                    break;
                case FresnelLens fresnelLens:
                    EnchantAllValid<Nimble>(fresnelLens, player, results, fresnelLens.DynamicVars["NimbleAmount"].BaseValue);
                    break;
                case SilkenTress silkenTress:
                    ApplySilkenTress(silkenTress, player, results, options);
                    break;
                case WingCharm wingCharm:
                    ApplyWingCharm(wingCharm, player, results, nicheRng);
                    break;
            }
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
                UpgradeAllValidCards(modifyingRelic: null, results);
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

    private static void ApplyLastingCandy(
        LastingCandy relic,
        Player player,
        List<CardCreationResult> results,
        CardCreationOptions options,
        Rng rewardRng,
        CardRarityOdds rarityOdds)
    {
        if (relic.Owner != player ||
            options.Source != CardCreationSource.Encounter ||
            relic.CombatsSeen <= 0 ||
            relic.CombatsSeen % 2 != 0)
        {
            return;
        }

        var candidates = options.GetPossibleCards(player)
            .Where(card => card.Type == CardType.Power && results.TrueForAll(result => result.originalCard.Id != card.Id))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = options.GetPossibleCards(player)
                .Where(card => card.Type == CardType.Power)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var candyOptions = new CardCreationOptions(candidates, CardCreationSource.Other, options.RarityOdds)
            .WithFlags(CardCreationFlags.NoModifyHooks | CardCreationFlags.NoCardPoolModifications);
        var card = CreateBaseRewards(player, 1, candyOptions, rewardRng, rarityOdds).FirstOrDefault()?.Card;
        if (card != null)
        {
            var result = new CardCreationResult(card);
            result.ModifyCard(card, relic);
            results.Add(result);
        }
    }

    private static void UpgradeCardsByType(
        RelicModel relic,
        Player player,
        List<CardCreationResult> results,
        CardCreationOptions options,
        CardType type)
    {
        if (relic.Owner != player || options.Flags.HasFlag(CardCreationFlags.NoHookUpgrades))
        {
            return;
        }

        foreach (var result in results)
        {
            if (result.Card.Type == type && result.Card.IsUpgradable)
            {
                result.ModifyCard(UpgradePreview(result.Card), relic);
            }
        }
    }

    private static void UpgradeAllCards(
        SilverCrucible relic,
        Player player,
        List<CardCreationResult> results,
        CardCreationOptions options)
    {
        if (relic.Owner != player ||
            relic.TimesUsed >= relic.DynamicVars.Cards.IntValue ||
            !options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            return;
        }

        UpgradeAllValidCards(relic, results);
    }

    private static void ApplyLavaLamp(LavaLamp relic, Player player, List<CardCreationResult> results)
    {
        if (relic.Owner == player && player.RunState.CurrentRoom is MegaCrit.Sts2.Core.Rooms.CombatRoom && !relic.TookDamageThisCombat)
        {
            UpgradeAllValidCards(relic, results);
        }
    }

    private static void ApplySilkenTress(
        SilkenTress relic,
        Player player,
        List<CardCreationResult> results,
        CardCreationOptions options)
    {
        if (relic.Owner == player && !relic.IsUsedUp && options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            EnchantAllValid<Glam>(relic, player, results, 1m);
        }
    }

    private static void ApplyWingCharm(WingCharm relic, Player player, List<CardCreationResult> results, Rng nicheRng)
    {
        if (relic.Owner != player)
        {
            return;
        }

        var swift = ModelDb.Enchantment<Swift>();
        var validResults = results.Where(result => swift.CanEnchant(result.Card)).ToList();
        var selected = nicheRng.NextItem(validResults);
        if (selected != null)
        {
            selected.ModifyCard(
                EnchantPreview<Swift>(selected.Card, relic.DynamicVars["SwiftAmount"].BaseValue),
                relic);
        }
    }

    private static void UpgradeAllValidCards(RelicModel? modifyingRelic, List<CardCreationResult> results)
    {
        foreach (var result in results)
        {
            if (result.Card.IsUpgradable)
            {
                var upgradedCard = UpgradePreview(result.Card);
                if (modifyingRelic != null)
                {
                    result.ModifyCard(upgradedCard, modifyingRelic);
                }
                else
                {
                    result.ModifyCard(upgradedCard);
                }
            }
        }
    }

    private static void EnchantAllValid<T>(
        RelicModel relic,
        Player player,
        List<CardCreationResult> results,
        decimal amount)
        where T : EnchantmentModel
    {
        if (relic.Owner != player)
        {
            return;
        }

        var enchantment = ModelDb.Enchantment<T>();
        foreach (var result in results)
        {
            if (enchantment.CanEnchant(result.Card))
            {
                result.ModifyCard(EnchantPreview<T>(result.Card, amount), relic);
            }
        }
    }

    private static CardModel UpgradePreview(CardModel card)
    {
        var preview = ClonePreviewCard(card);
        PredictionUtils.UpgradePreviewCardInPlace(preview);
        return preview;
    }

    private static CardModel EnchantPreview<T>(CardModel card, decimal amount)
        where T : EnchantmentModel
    {
        var preview = ClonePreviewCard(card);
        var enchantment = ModelDb.Enchantment<T>().ToMutable();
        if (preview.Enchantment == null)
        {
            preview.EnchantInternal(enchantment, amount);
            enchantment.ModifyCard();
        }
        else if (preview.Enchantment.GetType() == enchantment.GetType())
        {
            preview.Enchantment.Amount += (int)amount;
        }

        preview.FinalizeUpgradeInternal();
        return preview;
    }

    private static CardModel ClonePreviewCard(CardModel card)
    {
        return (CardModel)card.ClonePreservingMutability();
    }

    private static IEnumerable<CardModel> FilterForPlayerCount(Player player, IEnumerable<CardModel> cards)
    {
        return player.RunState.Players.Count > 1
            ? cards.Where(card => card.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly)
            : cards.Where(card => card.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly);
    }

    private static void WarnAboutUnsupportedModifiers(Player player, bool early)
    {
        var methodName = early
            ? nameof(AbstractModel.TryModifyCardRewardOptions)
            : nameof(AbstractModel.TryModifyCardRewardOptionsLate);
        var parameters = new[]
        {
            typeof(Player),
            typeof(List<CardCreationResult>),
            typeof(CardCreationOptions)
        };

        foreach (var modifier in player.RunState.IterateHookListeners(null))
        {
            var type = modifier.GetType();
            if (IsKnownRewardOptionModifier(modifier) ||
                WarnedUnsupportedModifierTypes.Contains(type) ||
                !Overrides(type, methodName, parameters))
            {
                continue;
            }

            WarnedUnsupportedModifierTypes.Add(type);
            Entry.Logger.Warn($"Card reward prediction does not safely mirror {type.FullName}.{methodName}; preview may omit that modifier.");
        }
    }

    private static bool IsKnownRewardOptionModifier(AbstractModel modifier)
    {
        return modifier is LastingCandy or FrozenEgg or MoltenEgg or ToxicEgg or SilverCrucible or LavaLamp or Glitter or
            FresnelLens or SilkenTress or WingCharm;
    }

    private static bool Overrides(Type type, string methodName, Type[] parameters)
    {
        return type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, parameters)?.DeclaringType !=
            typeof(AbstractModel);
    }
}
