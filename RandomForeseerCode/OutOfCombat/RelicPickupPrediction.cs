using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class RelicPickupPrediction
{
    private static readonly HashSet<Type> WarnedRelicTypes = [];

    public static IReadOnlyList<IHoverTip> GetHoverTips(Player player, RelicModel relic)
    {
        if (!IsPredictionEnabled(relic))
        {
            return [];
        }

        try
        {
            var context = new RunPredictionContext(player);

            return relic switch
            {
                // Neow
                ArcaneScroll when IsSingleplayerUnfairPredictionAllowed(relic) =>
                    [.. PredictRareCharacterCards(context, 1).ToPredictionHoverTips()],
                HeftyTablet => [.. PredictRareCharacterCards(context, 3).ToPredictionHoverTips()],
                Kaleidoscope =>
                    [.. PredictKaleidoscopeBundles(context).ToPredictionCardBundleHoverTips()],
                LargeCapsule when IsSingleplayerUnfairPredictionAllowed(relic) =>
                    [.. OutOfCombatPredictionUtils.PredictRelicRewards(
                        context,
                        relic.DynamicVars["Relics"].IntValue).ToPredictionHoverTips()],
                LeadPaperweight => [.. PredictColorlessCards(context, 2).ToPredictionHoverTips()],
                LeafyPoultice when IsSingleplayerUnfairPredictionAllowed(relic) =>
                    [.. PredictLeafyPoultice(player).ToPredictionHoverTips()],
                LostCoffer => PredictLostCofferTips(context),
                MassiveScroll => [.. PredictMultiplayerCards(context, 3).ToPredictionHoverTips()],
                NeowsBones => PredictNeowsBonesTips(context, relic),
                NewLeaf when IsSingleplayerUnfairPredictionAllowed(relic) =>
                    [.. PredictNewLeaf(player).ToPredictionHoverTips()],
                PhialHolster when IsSingleplayerUnfairPredictionAllowed(relic) =>
                    [.. PotionFactory.CreateRandomPotionsOutOfCombat(
                        player,
                        relic.DynamicVars["Potions"].IntValue,
                        player.RunState.Rng.CombatPotionGeneration.Clone()).ToPredictionHoverTips()],
                ScrollBoxes =>
                    [.. PredictScrollBoxes(context)
                        .ToPredictionCardBundleHoverTips(PredictionCardBundleKind.ScrollBoxes)],
                SilkenTress silkenTress when IsAllModesUnfairPredictionAllowed() =>
                    RewardPagePredictionContext.HasOtherPendingReward(silkenTress)
                        ? [PredictionHoverTipFactory.Text("silken_tress_reward_offset")]
                        : PredictSilkenTressRewardTips(context, silkenTress),
                SmallCapsule =>
                    [.. OutOfCombatPredictionUtils.PredictRelicRewards(context, 1).ToPredictionHoverTips()],

                // Darv
                Astrolabe when IsSingleplayerUnfairPredictionAllowed() =>
                    [.. PredictAstrolabeBundles(player)
                        .ToPredictionCardBundleHoverTips(PredictionCardBundleKind.Transform)],
                CallingBell => [.. OutOfCombatPredictionUtils.PredictRelicRewards(
                    player,
                    [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare]).ToPredictionHoverTips()],
                PandorasBox when IsSingleplayerUnfairPredictionAllowed() =>
                    [.. PredictPandorasBox(player).ToPredictionHoverTips()],

                // Nonupeipe
                BeautifulBracelet beautifulBracelet when IsSingleplayerUnfairPredictionAllowed() =>
                    [.. PredictBeautifulBracelet(context, beautifulBracelet).ToPredictionHoverTips()],

                // Orobas
                AlchemicalCoffer when IsSingleplayerUnfairPredictionAllowed() =>
                    [.. PotionFactory.CreateRandomPotionsOutOfCombat(
                        player,
                        relic.DynamicVars["PotionSlots"].IntValue,
                        player.RunState.Rng.CombatPotionGeneration.Clone()).ToPredictionHoverTips()],
                GlassEye => [.. PredictGlassEyeBundles(context).ToPredictionCardBundleHoverTips()],
                SandCastle when IsSingleplayerUnfairPredictionAllowed() =>
                    [.. PredictSandCastle(player, relic.DynamicVars.Cards.IntValue).ToPredictionHoverTips()],
                SeaGlass seaGlass => [.. PredictSeaGlassBundles(context, seaGlass).ToPredictionCardBundleHoverTips()],

                // Tezcatara
                ToyBox =>
                    [.. PredictToyBoxRelics(context, relic.DynamicVars["Relics"].IntValue).ToPredictionHoverTips()],

                // Vakuu
                DistinguishedCape when IsSingleplayerUnfairPredictionAllowed() =>
                    [.. PredictCurses(context, relic.DynamicVars["Curses"].IntValue).ToPredictionHoverTips()],

                // Non-Ancient relics
                Cauldron => [.. PredictionUtils.PredictPotionRewards(
                    player,
                    relic.DynamicVars["Potions"].IntValue,
                    player.PlayerRng.Rewards.Clone()).ToPredictionHoverTips()],
                FragrantMushroom => [.. OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.IsUpgradable).ToPredictionHoverTips()],
                Orrery => [.. OutOfCombatPredictionUtils.PredictCardRewardBundles(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    3,
                    () => OutOfCombatPredictionUtils.CreateCharacterCardRewardOptions(player)).ToPredictionCardBundleHoverTips()],
                WarPaint => [.. OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.Type == CardType.Skill && card.IsUpgradable).ToPredictionHoverTips()],
                Whetstone => [.. OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.Type == CardType.Attack && card.IsUpgradable).ToPredictionHoverTips()],
                _ => []
            };
        }
        catch (Exception ex)
        {
            WarnOnce(relic.GetType(), $"Could not predict relic pickup effect for {relic.Id}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "relic_pickup_prediction",
                "build_hover_tips",
                TelemetryContext.ForModel(relic));
            return [];
        }
    }

    private static bool IsPredictionEnabled(RelicModel relic)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled)
        {
            return false;
        }

        return relic.Rarity switch
        {
            RelicRarity.Ancient => settings.AncientRelicPickupPredictionEnabled,
            RelicRarity.Event => settings.EventOptionPredictionEnabled,
            _ => settings.RelicPickupPredictionEnabled
        };
    }

    private static IReadOnlyList<CardModel> PredictRareCharacterCards(RunPredictionContext context, int count)
    {
        var options = new CardCreationOptions(
                [context.Player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.Uniform,
                card => card.Rarity == CardRarity.Rare)
            .WithFlags(CardCreationFlags.NoUpgradeRoll);

        return CardRewardPrediction.PredictCards(context, count, options);
    }

    private static IReadOnlyList<CardModel> PredictColorlessCards(RunPredictionContext context, int count)
    {
        var options = new CardCreationOptions(
            [ModelDb.CardPool<ColorlessCardPool>()],
            CardCreationSource.Other,
            CardRarityOddsType.RegularEncounter);

        return CardRewardPrediction.PredictCards(context, count, options);
    }

    private static IReadOnlyList<CardModel> PredictMultiplayerCards(RunPredictionContext context, int count)
    {
        // StS2 v0.108.0 replaced custom CardCreationOptions card lists with card pools plus filters.
        // This mirrors MassiveScroll.AfterObtained so Prismatic-style pool modifiers can still see the pools.
        var options = new CardCreationOptions(
            [context.Player.Character.CardPool, ModelDb.CardPool<ColorlessCardPool>()],
            CardCreationSource.Other,
            CardRarityOddsType.RegularEncounter,
            card => card.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly);

        return CardRewardPrediction.PredictCards(context, count, options);
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictKaleidoscopeBundles(RunPredictionContext context)
    {
        var bundles = new List<IReadOnlyList<CardModel>>();

        for (var i = 0; i < 2; i++)
        {
            var bundle = new List<CardModel>();
            var pools = context.Player.UnlockState.CharacterCardPools
                .Where(pool => pool != context.Player.Character.CardPool)
                .ToList()
                .StableShuffle(context.SharedRng.Niche)
                .Take(3);

            foreach (var pool in pools)
            {
                var options = new CardCreationOptions(
                        [pool],
                        CardCreationSource.Other,
                        CardRarityOddsType.RegularEncounter)
                    .WithFlags(CardCreationFlags.NoCardPoolModifications);
                bundle.AddRange(CardRewardPrediction.PredictCards(context, 1, options));
            }

            var rewardOptions = new CardCreationOptions([], CardCreationSource.Other, CardRarityOddsType.Uniform)
                .WithFlags(CardCreationFlags.NoCardPoolModifications |
                    CardCreationFlags.NoCardModelModifications |
                    CardCreationFlags.IsCardReward);
            bundles.Add(CardRewardPrediction.ApplyRewardModifiersToExistingCards(context, bundle, rewardOptions));
        }

        return bundles;
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictAstrolabeBundles(Player player)
    {
        var bundles = OutOfCombatPredictionUtils.PredictDistinctDeckTransformResultBundles(
            player,
            player.RunState.Rng.Niche,
            3);
        return [.. bundles.Select(cards => cards.Select(PredictionUtils.CreateUpgradedCard).ToArray())];
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictGlassEyeBundles(RunPredictionContext context)
    {
        var rarities = new[]
        {
            CardRarity.Common,
            CardRarity.Common,
            CardRarity.Uncommon,
            CardRarity.Uncommon,
            CardRarity.Rare
        };

        return rarities
            .Select(rarity =>
            {
                var options = CardCreationOptions
                    .ForNonCombatWithUniformOdds([context.Player.Character.CardPool], card => card.Rarity == rarity)
                    .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.IsCardReward);
                return CardRewardPrediction.PredictCards(context, 3, options);
            })
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictSeaGlassBundles(RunPredictionContext context, SeaGlass seaGlass)
    {
        if (seaGlass.CharacterId == null)
        {
            return [];
        }

        var character = ModelDb.GetById<CharacterModel>(seaGlass.CharacterId);
        var cardCount = seaGlass.DynamicVars.Cards.IntValue / 3;

        return new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare }
            .Select(rarity =>
            {
                var options = CardCreationOptions
                    .ForNonCombatWithUniformOdds([character.CardPool], card => card.Rarity == rarity)
                    .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications);
                return CardRewardPrediction.PredictCards(context, cardCount, options);
            })
            .ToList();
    }

    private static IReadOnlyList<IHoverTip> PredictLostCofferTips(RunPredictionContext context)
    {
        var options = OutOfCombatPredictionUtils.CreateCharacterCardRewardOptions(context.Player);
        var tips = CardRewardPrediction.PredictCards(context, 3, options).ToPredictionHoverTips().ToList();

        var potion = PotionFactory.CreateRandomPotionOutOfCombat(context.Player, context.Rng.Rewards);
        tips.Add(PredictionHoverTipFactory.Potion(potion));
        return tips;
    }

    private static IReadOnlyList<IHoverTip> PredictNeowsBonesTips(RunPredictionContext context, RelicModel relic)
    {
        var validRelics = ModelDb.Event<Neow>().AllPossibleOptions
            .Select(option => option.Relic)
            .OfType<RelicModel>()
            .Where(relic => relic.IsAllowedAtNeow(context.Player) && relic is not NeowsBones)
            .ToList();
        context.Rng.Rewards.Shuffle(validRelics);

        var predictedRelics = validRelics.Take(relic.DynamicVars["Relics"].IntValue).ToList();
        var tips = predictedRelics.ToPredictionHoverTips().ToList();

        if (IsSingleplayerUnfairPredictionAllowed() && predictedRelics is [var firstRelic, var secondRelic])
        {
            var curseCount = relic.DynamicVars["Curses"].IntValue;
            var clonedContext = context.Clone();

            FastForwardRelicPickup(context, firstRelic);
            FastForwardRelicPickup(context, secondRelic);
            var firstCurses = PredictCurses(context, curseCount);

            FastForwardRelicPickup(clonedContext, secondRelic);
            FastForwardRelicPickup(clonedContext, firstRelic);
            var secondCurses = PredictCurses(clonedContext, curseCount);

            if (firstCurses.SequenceEqual(secondCurses))
            {
                tips.AddRange(firstCurses.ToPredictionHoverTips());
            }
            else
            {
                tips.Add(PredictionHoverTipFactory.Text("neows_bones_pickup_order", description =>
                {
                    description.Add("FirstRelic", firstRelic.Title.GetFormattedText());
                    description.Add("SecondRelic", secondRelic.Title.GetFormattedText());
                    description.Add("NeowsBones", relic.Title.GetFormattedText());
                    description.Add("Curses", curseCount);
                    description.Add("FirstCurses", firstCurses.Select(card => card.Title).ToList());
                    description.Add("SecondCurses", secondCurses.Select(card => card.Title).ToList());
                }));
            }
        }

        return tips;
    }

    private static IReadOnlyList<CardModel> PredictCurses(RunPredictionContext context, int count)
    {
        var availableCurses = context.Player.GetUnlockedCurseCards()
            .Where(card => card.CanBeGeneratedByModifiers)
            .OrderBy(card => card.Id)
            .ToList();
        var curses = new List<CardModel>(count);

        for (var i = 0; i < count; i++)
        {
            var card = context.SharedRng.Niche.NextItem(availableCurses);
            if (card == null)
            {
                break;
            }

            availableCurses.Remove(card);
            curses.Add(card);
        }

        return curses;
    }

    /// <summary>
    /// Mirrors supported immediate pickup RNG so callers can continue predicting from the state after acquisition.
    /// </summary>
    internal static void FastForwardRelicPickup(RunPredictionContext context, RelicModel relic)
    {
        switch (relic)
        {
            case ArcaneScroll:
                PredictRareCharacterCards(context, relic.DynamicVars.Cards.IntValue);
                break;
            case HeftyTablet:
                PredictRareCharacterCards(context, relic.DynamicVars.Cards.IntValue);
                break;
            case Kaleidoscope:
                PredictKaleidoscopeBundles(context);
                break;
            case LeadPaperweight:
                PredictColorlessCards(context, 2);
                break;
            case LostCoffer:
                PredictLostCofferTips(context);
                break;
            case MassiveScroll:
                PredictMultiplayerCards(context, 3);
                break;
            case NewLeaf:
                OutOfCombatPredictionUtils.FastForwardDeckTransforms(context, relic.DynamicVars.Cards.IntValue);
                break;
            case ScrollBoxes:
                PredictScrollBoxes(context);
                break;
            case LargeCapsule:
                FastForwardRelicRewardsPickup(context, relic.DynamicVars["Relics"].IntValue);
                break;
            case SmallCapsule:
                // Small Capsule's offered relic can technically be skipped; forecast the normal path where it is taken.
                FastForwardRelicRewardsPickup(context, 1);
                break;
            case WarPaint:
                OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    context.Player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.Type == CardType.Skill && card.IsUpgradable,
                    context.SharedRng.Niche);
                break;
            case Whetstone:
                OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    context.Player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.Type == CardType.Attack && card.IsUpgradable,
                    context.SharedRng.Niche);
                break;
            case Cauldron:
                PredictionUtils.PredictPotionRewards(
                    context.Player,
                    relic.DynamicVars[Cauldron._potionsKey].IntValue,
                    context.Rng.Rewards);
                break;
            case Orrery:
                for (var i = 0; i < relic.DynamicVars.Cards.IntValue; i++)
                {
                    var options = OutOfCombatPredictionUtils.CreateCharacterCardRewardOptions(context.Player);
                    CardRewardPrediction.PredictCards(context, 3, options);
                }
                break;
        }
    }

    private static void FastForwardRelicRewardsPickup(RunPredictionContext context, int count)
    {
        var relics = OutOfCombatPredictionUtils.PredictRelicRewards(context, count);
        foreach (var relic in relics)
        {
            FastForwardRelicPickup(context, relic);
        }
    }

    private static IReadOnlyList<IHoverTip> PredictSilkenTressRewardTips(RunPredictionContext context, SilkenTress relic)
    {
        var previewRelic = PredictionUtils.CreateRelic(relic.CanonicalInstance, context.Player);

        var options = OutOfCombatPredictionUtils.CreateMonsterCombatCardRewardOptions(context.Player);

        OutOfCombatPredictionUtils.FastForwardBeforeMonsterCardReward(context);

        var cards = CardRewardPrediction.PredictCards(
            context,
            3,
            options,
            extraResultModifiers: [previewRelic]);

        return [.. cards.ToPredictionHoverTips()];
    }

    private static IReadOnlyList<CardModel> PredictNewLeaf(Player player)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(player, player.RunState.Rng.Niche);
    }

    private static IReadOnlyList<CardModel> PredictPandorasBox(Player player)
    {
        var rng = player.RunState.Rng.Niche.Clone();
        return PileType.Deck.GetPile(player).Cards
            .Where(card => card.IsBasicStrikeOrDefend && card.IsRemovable)
            .Select(card => PredictionUtils.PredictTransformResult(card, rng, isInCombat: false))
            .ToList();
    }

    private static IReadOnlyList<CardModel> PredictSandCastle(Player player, int count)
    {
        return OutOfCombatPredictionUtils.PredictUpgradedDeckCards(player, count, card => card.IsUpgradable);
    }

    private static IReadOnlyList<CardModel> PredictLeafyPoultice(Player player)
    {
        var rng = player.PlayerRng.Transformations.Clone();
        var source = PileType.Deck.GetPile(player).Cards
            .Where(card => card.Rarity == CardRarity.Basic)
            .ToList();
        var cards = new List<CardModel>();

        var strike = source.FirstOrDefault(card => card.Tags.Contains(CardTag.Strike));
        if (strike != null)
        {
            cards.Add(PredictionUtils.PredictTransformResult(strike, rng, isInCombat: false));
        }

        var defend = source.FirstOrDefault(card => card.Tags.Contains(CardTag.Defend));
        if (defend != null)
        {
            cards.Add(PredictionUtils.PredictTransformResult(defend, rng, isInCombat: false));
        }

        return cards;
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictScrollBoxes(RunPredictionContext context)
    {
        var player = context.Player;
        var isDefect = player.Character is Defect;
        var cardPool = player.Character.CardPool;
        var commonOptions = CardCreationOptions
            .ForNonCombatWithUniformOdds([cardPool], card => card.Rarity == CardRarity.Common)
            .WithFlags(CardCreationFlags.NoRarityModification);
        commonOptions = HookMirrors.ModifyCardRewardCreationOptions(player.RunState, player, commonOptions);
        var uncommonOptions = CardCreationOptions
            .ForNonCombatWithUniformOdds([cardPool], card => card.Rarity == CardRarity.Uncommon)
            .WithFlags(CardCreationFlags.NoRarityModification);
        uncommonOptions = HookMirrors.ModifyCardRewardCreationOptions(player.RunState, player, uncommonOptions);

        var commonCards = commonOptions.GetPossibleCards(player).ToList();
        var uncommonCards = uncommonOptions.GetPossibleCards(player).ToList();
        var bundles = new List<IReadOnlyList<CardModel>>();
        var usedCardIds = new HashSet<ModelId>();

        for (var bundleIndex = 0; bundleIndex < 2; bundleIndex++)
        {
            if (isDefect && context.Rng.Rewards.NextInt(100) < 1)
            {
                var claw = ModelDb.Card<Claw>();
                bundles.Add([claw, claw, claw]);
                continue;
            }

            var bundle = new List<CardModel>();
            var availableCommon = commonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            for (var i = 0; i < 2; i++)
            {
                var common = context.Rng.Rewards.NextItem(availableCommon);
                if (common == null)
                {
                    break;
                }

                bundle.Add(common);
                usedCardIds.Add(common.Id);
                availableCommon.Remove(common);
            }

            var availableUncommon = uncommonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            var uncommon = context.Rng.Rewards.NextItem(availableUncommon);
            if (uncommon != null)
            {
                bundle.Add(uncommon);
                usedCardIds.Add(uncommon.Id);
            }

            bundles.Add(bundle);
        }

        return bundles;
    }

    private static IReadOnlyList<RelicModel> PredictToyBoxRelics(RunPredictionContext context, int count)
    {
        var relics = OutOfCombatPredictionUtils.PredictRelicRewards(context, count)
            .Select(relic => relic.ToMutable())
            .ToList();
        foreach (var relic in relics)
        {
            relic.IsWax = true;
        }

        return relics;
    }

    private static IReadOnlyList<CardModel> PredictBeautifulBracelet(
        RunPredictionContext context,
        BeautifulBracelet relic)
    {
        var swift = ModelDb.Enchantment<Swift>();

        return [..context.Deck.Cards
            .Where(card => swift.CanEnchant(card.Preview))
            .TakeRandom(relic.DynamicVars.Cards.IntValue, context.SharedRng.Niche)
            .Select(card => card.Enchant(swift.ToMutable(), relic.DynamicVars["Swift"].IntValue))
            .SelectPreviews()];
    }

    private static void WarnOnce(Type relicType, string message)
    {
        if (WarnedRelicTypes.Add(relicType))
        {
            Entry.Logger.Warn(message);
        }
    }

    private static bool IsSingleplayerUnfairPredictionAllowed(RelicModel? relicSource = null)
    {
        return ModData.Settings.Allows(PredictionFairness.UnfairInSingleplayer) ||
            (relicSource is not null && RewardPagePredictionContext.HasOtherPendingReward(relicSource));
    }

    private static bool IsAllModesUnfairPredictionAllowed()
    {
        return ModData.Settings.Allows(PredictionFairness.UnfairInAllModes);
    }
}
