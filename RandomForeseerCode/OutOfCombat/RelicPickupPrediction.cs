using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class RelicPickupPrediction
{
    private static readonly HashSet<Type> WarnedRelicTypes = [];

    public static IReadOnlyList<IHoverTip> GetHoverTips(Player player, RelicModel relic)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableRelicPickupPrediction))
        {
            return [];
        }

        try
        {
            var context = new RunPredictionContext(player);

            return relic switch
            {
                // Neow
                ArcaneScroll when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictRareCharacterCards(context, 1)),
                HeftyTablet => PredictionHoverTips.Cards(PredictRareCharacterCards(context, 3)),
                Kaleidoscope =>
                    PredictionHoverTips.CardBundles(PredictKaleidoscopeBundles(context)),
                LargeCapsule when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Relics(OutOfCombatPredictionUtils.PredictRelicRewards(
                        context,
                        relic.DynamicVars["Relics"].IntValue)),
                LeadPaperweight => PredictionHoverTips.Cards(PredictColorlessCards(context, 2)),
                LeafyPoultice when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictLeafyPoultice(player)),
                LostCoffer => PredictLostCofferTips(context),
                MassiveScroll => PredictionHoverTips.Cards(PredictMultiplayerCards(context, 3)),
                NeowsBones => PredictNeowsBonesTips(context, relic),
                NewLeaf when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictNewLeaf(player)),
                PhialHolster when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Potions(PotionFactory.CreateRandomPotionsOutOfCombat(
                        player,
                        relic.DynamicVars["Potions"].IntValue,
                        PredictionUtils.CloneRng(player.RunState.Rng.CombatPotionGeneration))),
                ScrollBoxes =>
                    PredictionHoverTips.CardBundles(PredictScrollBoxes(context), isVanillaCardBundle: true),
                SilkenTress silkenTress when IsAllModesUnfairPredictionAllowed() =>
                    RewardPagePredictionContext.HasOtherPendingRelicReward(silkenTress)
                        ? [PredictionHoverTips.Text("silken_tress_reward_offset")]
                        : PredictSilkenTressRewardTips(context, silkenTress),
                SmallCapsule =>
                    PredictionHoverTips.Relics(OutOfCombatPredictionUtils.PredictRelicRewards(context, 1)),

                // Darv
                Astrolabe when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.CardBundles(PredictAstrolabeBundles(player), isTransform: true),
                CallingBell => PredictionHoverTips.Relics(OutOfCombatPredictionUtils.PredictRelicRewards(
                    player,
                    [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare])),
                PandorasBox when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictPandorasBox(player)),

                // Orobas
                AlchemicalCoffer when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Potions(PotionFactory.CreateRandomPotionsOutOfCombat(
                        player,
                        relic.DynamicVars["PotionSlots"].IntValue,
                        PredictionUtils.CloneRng(player.RunState.Rng.CombatPotionGeneration))),
                GlassEye => PredictionHoverTips.CardBundles(PredictGlassEyeBundles(context)),
                SandCastle when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictSandCastle(player, relic.DynamicVars.Cards.IntValue)),
                SeaGlass seaGlass => PredictionHoverTips.CardBundles(PredictSeaGlassBundles(context, seaGlass)),

                // Tezcatara
                ToyBox =>
                    PredictionHoverTips.Relics(PredictToyBoxRelics(context, relic.DynamicVars["Relics"].IntValue)),

                // Vakuu
                SereTalon when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictSereTalon(context, relic)),

                // Non-Ancient relics
                Cauldron => PredictionHoverTips.Potions(PredictionUtils.PredictPotionRewards(
                    player,
                    relic.DynamicVars["Potions"].IntValue,
                    PredictionUtils.CloneRng(player.PlayerRng.Rewards))),
                FragrantMushroom => PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.IsUpgradable)),
                Orrery => PredictionHoverTips.CardBundles(OutOfCombatPredictionUtils.PredictCardRewardBundles(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    3,
                    OutOfCombatPredictionUtils.CreateCharacterCardRewardOptions(player))),
                WarPaint => PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.Type == CardType.Skill && card.IsUpgradable)),
                Whetstone => PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                    player,
                    relic.DynamicVars.Cards.IntValue,
                    card => card.Type == CardType.Attack && card.IsUpgradable)),
                _ => []
            };
        }
        catch (Exception ex)
        {
            WarnOnce(relic.GetType(), $"Could not predict relic pickup effect for {relic.Id}: {ex}");
            return [];
        }
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
        var customCardPool = PredictionUtils.GetUnlockedColorlessCards(context.Player)
            .Concat(PredictionUtils.GetUnlockedCharacterCards(context.Player))
            .Where(card => card.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly);
        var options = new CardCreationOptions(customCardPool, CardCreationSource.Other, CardRarityOddsType.RegularEncounter);

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
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResultBundles(
            player,
            player.RunState.Rng.Niche,
            3,
            upgradeResults: true);
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
        var tips = PredictionHoverTips.Cards(CardRewardPrediction.PredictCards(context, 3, options));

        var potion = PotionFactory.CreateRandomPotionOutOfCombat(context.Player, context.Rng.Rewards);
        tips = tips.Concat(PredictionHoverTips.Potions([potion])).ToList();
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
        var tips = PredictionHoverTips.Relics(predictedRelics).ToList();

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
                tips.AddRange(PredictionHoverTips.Cards(firstCurses));
            }
            else
            {
                tips.Add(PredictionHoverTips.Text("neows_bones_pickup_order", description =>
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
        var availableCurses = PredictionUtils.GetUnlockedCards(context.Player, ModelDb.CardPool<CurseCardPool>())
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

    private static void FastForwardRelicPickup(RunPredictionContext context, RelicModel relic)
    {
        // Mirrors only immediate pickup RNG that can occur before NeowsBones adds curses.
        // TODO: Streamline this with generic relic pickup prediction logic using RunPredictionContext
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

        var options = CardCreationOptions
            .ForRoom(context.Player, RoomType.Monster)
            .WithFlags(CardCreationFlags.IsCardReward);

        OutOfCombatPredictionUtils.FastForwardBeforeMonsterCardReward(context);

        var cards = CardRewardPrediction.PredictCards(
            context,
            3,
            options,
            extraResultModifiers: [previewRelic]);

        return PredictionHoverTips.Cards(cards);
    }

    private static IReadOnlyList<CardModel> PredictNewLeaf(Player player)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(player, player.RunState.Rng.Niche);
    }

    private static IReadOnlyList<CardModel> PredictPandorasBox(Player player)
    {
        var rng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        return PileType.Deck.GetPile(player).Cards
            .Where(card => card.IsBasicStrikeOrDefend && card.IsRemovable)
            .Select(card => PredictionUtils.PredictTransformResult(card, rng, isInCombat: false))
            .ToList();
    }

    private static IReadOnlyList<CardModel> PredictSandCastle(Player player, int count)
    {
        return OutOfCombatPredictionUtils.PredictUpgradedDeckCards(player, count, card => card.IsUpgradable);
    }

    private static IReadOnlyList<CardModel> PredictSereTalon(RunPredictionContext context, RelicModel relic)
    {
        return PredictCurses(context, relic.DynamicVars["Curses"].IntValue);
    }

    private static IReadOnlyList<CardModel> PredictLeafyPoultice(Player player)
    {
        var rng = PredictionUtils.CloneRng(player.PlayerRng.Transformations);
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
        commonOptions = Hook.ModifyCardRewardCreationOptions(player.RunState, player, commonOptions);
        var uncommonOptions = CardCreationOptions
            .ForNonCombatWithUniformOdds([cardPool], card => card.Rarity == CardRarity.Uncommon)
            .WithFlags(CardCreationFlags.NoRarityModification);
        uncommonOptions = Hook.ModifyCardRewardCreationOptions(player.RunState, player, uncommonOptions);

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

    private static void WarnOnce(Type relicType, string message)
    {
        if (WarnedRelicTypes.Add(relicType))
        {
            Entry.Logger.Warn(message);
        }
    }

    private static bool IsSingleplayerUnfairPredictionAllowed()
    {
        return RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInSingleplayer);
    }

    private static bool IsAllModesUnfairPredictionAllowed()
    {
        return RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes);
    }
}
