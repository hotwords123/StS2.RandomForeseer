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
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

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
            return relic switch
            {
                // Neow
                ArcaneScroll when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictRareCharacterCards(player, 1)),
                HeftyTablet => PredictionHoverTips.Cards(PredictRareCharacterCards(player, 3)),
                Kaleidoscope => PredictionHoverTips.CardBundles(PredictKaleidoscopeBundles(player)),
                LargeCapsule when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Relics(OutOfCombatPredictionUtils.PredictRelicRewards(player, relic.DynamicVars["Relics"].IntValue)),
                LeadPaperweight => PredictionHoverTips.Cards(PredictColorlessCards(player, 2)),
                LeafyPoultice when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictLeafyPoultice(player)),
                LostCoffer => PredictLostCofferTips(player),
                MassiveScroll => PredictionHoverTips.Cards(PredictMultiplayerCards(player, 3)),
                NeowsBones => PredictNeowsBonesTips(player, relic),
                NewLeaf when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictNewLeaf(player)),
                PhialHolster when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Potions(PotionFactory.CreateRandomPotionsOutOfCombat(
                        player,
                        relic.DynamicVars["Potions"].IntValue,
                        PredictionUtils.CloneRng(player.RunState.Rng.CombatPotionGeneration))),
                ScrollBoxes => PredictionHoverTips.CardBundles(PredictScrollBoxes(player), isVanillaCardBundle: true),
                SilkenTress silkenTress when IsAllModesUnfairPredictionAllowed() =>
                    PredictSilkenTressRewardTips(player, silkenTress),
                SmallCapsule when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Relics(OutOfCombatPredictionUtils.PredictRelicRewards(player, 1)),

                // Darv
                Astrolabe when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.CardBundles(PredictAstrolabeBundles(player), isTransform: true),
                CallingBell => PredictionHoverTips.Relics(OutOfCombatPredictionUtils.PredictRelicRewards(player, [
                    RelicRarity.Common,
                    RelicRarity.Uncommon,
                    RelicRarity.Rare
                ])),
                PandorasBox when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictPandorasBox(player)),

                // Orobas
                AlchemicalCoffer when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Potions(PotionFactory.CreateRandomPotionsOutOfCombat(
                        player,
                        relic.DynamicVars["PotionSlots"].IntValue,
                        PredictionUtils.CloneRng(player.RunState.Rng.CombatPotionGeneration))),
                GlassEye => PredictionHoverTips.CardBundles(PredictGlassEyeBundles(player)),
                SandCastle when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictSandCastle(player, relic.DynamicVars.Cards.IntValue)),
                SeaGlass seaGlass => PredictionHoverTips.CardBundles(PredictSeaGlassBundles(player, seaGlass)),

                // Tezcatara
                ToyBox => PredictionHoverTips.Relics(PredictToyBoxRelics(player, relic.DynamicVars["Relics"].IntValue)),

                // Vakuu
                SereTalon when IsSingleplayerUnfairPredictionAllowed() =>
                    PredictionHoverTips.Cards(PredictSereTalon(player, relic)),

                // Non-Ancient relics
                Cauldron => PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictPotionRewards(
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

    private static IReadOnlyList<CardModel> PredictRareCharacterCards(Player player, int count)
    {
        var options = new CardCreationOptions(
                [player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.Uniform,
                card => card.Rarity == CardRarity.Rare)
            .WithFlags(CardCreationFlags.NoUpgradeRoll);

        return PredictCards(player, count, options);
    }

    private static IReadOnlyList<CardModel> PredictColorlessCards(Player player, int count)
    {
        var options = new CardCreationOptions(
            [ModelDb.CardPool<ColorlessCardPool>()],
            CardCreationSource.Other,
            CardRarityOddsType.RegularEncounter);

        return PredictCards(player, count, options);
    }

    private static IReadOnlyList<CardModel> PredictMultiplayerCards(Player player, int count)
    {
        var customCardPool =
            ModelDb.CardPool<ColorlessCardPool>()
                .GetUnlockedCards(player.RunState.UnlockState, player.RunState.CardMultiplayerConstraint)
                .Concat(player.Character.CardPool.GetUnlockedCards(player.RunState.UnlockState, player.RunState.CardMultiplayerConstraint))
                .Where(card => card.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly);
        var options = new CardCreationOptions(customCardPool, CardCreationSource.Other, CardRarityOddsType.RegularEncounter);

        return PredictCards(player, count, options);
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictKaleidoscopeBundles(Player player)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var bundles = new List<IReadOnlyList<CardModel>>();

        for (var i = 0; i < 2; i++)
        {
            var bundle = new List<CardModel>();
            var pools = player.UnlockState.CharacterCardPools
                .Where(pool => pool != player.Character.CardPool)
                .ToList()
                .StableShuffle(nicheRng)
                .Take(3);

            foreach (var pool in pools)
            {
                var options = new CardCreationOptions(
                        [pool],
                        CardCreationSource.Other,
                        CardRarityOddsType.RegularEncounter)
                    .WithFlags(CardCreationFlags.NoCardPoolModifications);
                bundle.AddRange(PredictCards(player, 1, options, rewardRng, nicheRng));
            }

            bundles.Add(bundle);
        }

        return bundles;
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictAstrolabeBundles(Player player)
    {
        var bundles = new List<IReadOnlyList<CardModel>>();

        for (var slot = 0; slot < 3; slot++)
        {
            bundles.Add(OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(
                player,
                player.RunState.Rng.Niche,
                PredictionUtils.UpgradeCardInPlace,
                rngCounterOffset: slot));
        }

        return bundles;
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictGlassEyeBundles(Player player)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
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
                    .ForNonCombatWithUniformOdds([player.Character.CardPool], card => card.Rarity == rarity)
                    .WithFlags(CardCreationFlags.NoRarityModification);
                return PredictCards(player, 3, options, rewardRng, nicheRng);
            })
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictSeaGlassBundles(Player player, SeaGlass seaGlass)
    {
        if (seaGlass.CharacterId == null)
        {
            return [];
        }

        var character = ModelDb.GetById<CharacterModel>(seaGlass.CharacterId);
        var cardCount = seaGlass.DynamicVars.Cards.IntValue / 3;
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);

        return new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare }
            .Select(rarity =>
            {
                var options = CardCreationOptions
                    .ForNonCombatWithUniformOdds([character.CardPool], card => card.Rarity == rarity)
                    .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications);
                return PredictCards(player, cardCount, options, rewardRng, nicheRng);
            })
            .ToList();
    }

    private static IReadOnlyList<IHoverTip> PredictLostCofferTips(Player player)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var options = OutOfCombatPredictionUtils.CreateCharacterCardRewardOptions(player);
        var tips = PredictionHoverTips.Cards(PredictCards(player, 3, options, rewardRng, nicheRng)).ToList();

        var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, rewardRng);
        tips.AddRange(PredictionHoverTips.Potions([potion]));
        return tips;
    }

    private static IReadOnlyList<IHoverTip> PredictNeowsBonesTips(Player player, RelicModel relic)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var validRelics = ModelDb.Event<Neow>().AllPossibleOptions
            .Where(option => option.Relic != null && option.Relic.IsAllowedAtNeow(player) && option.Relic is not NeowsBones)
            .Select(option => option.Relic!)
            .ToList();
        rewardRng.Shuffle(validRelics);

        var tips = PredictionHoverTips.Relics(validRelics.Take(relic.DynamicVars["Relics"].IntValue)).ToList();
        if (IsSingleplayerUnfairPredictionAllowed())
        {
            tips.AddRange(PredictionHoverTips.Cards(PredictNeowsBonesCurses(player, relic.DynamicVars["Curses"].IntValue)));
        }

        return tips;
    }

    private static IReadOnlyList<CardModel> PredictNeowsBonesCurses(Player player, int count)
    {
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var availableCurses = ModelDb.CardPool<CurseCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(card => card.CanBeGeneratedByModifiers)
            .OrderBy(card => card.Id)
            .ToList();
        var curses = new List<CardModel>();

        for (var i = 0; i < count; i++)
        {
            var card = nicheRng.NextItem(availableCurses);
            if (card == null)
            {
                break;
            }

            availableCurses.Remove(card);
            curses.Add(card);
        }

        return curses;
    }

    private static IReadOnlyList<IHoverTip> PredictSilkenTressRewardTips(Player player, SilkenTress relic)
    {
        var previewRelic = PredictionUtils.CreateRelic(relic.CanonicalInstance, player);

        var options = CardCreationOptions
            .ForRoom(player, RoomType.Monster)
            .WithFlags(CardCreationFlags.IsCardReward);
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);

        OutOfCombatPredictionUtils.FastForwardBeforeFirstMonsterCardReward(player, rewardRng);

        var cards = OutOfCombatPredictionUtils.PredictCards(
            player,
            3,
            options,
            rewardRng,
            nicheRng,
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
            .Select(card => OutOfCombatPredictionUtils.PredictTransformResult(card, rng))
            .ToList();
    }

    private static IReadOnlyList<CardModel> PredictSandCastle(Player player, int count)
    {
        return OutOfCombatPredictionUtils.PredictUpgradedDeckCards(player, count, card => card.IsUpgradable);
    }

    private static IReadOnlyList<CardModel> PredictSereTalon(Player player, RelicModel relic)
    {
        return PredictNeowsBonesCurses(player, relic.DynamicVars["Curses"].IntValue);
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
            cards.Add(OutOfCombatPredictionUtils.PredictTransformResult(strike, rng));
        }

        var defend = source.FirstOrDefault(card => card.Tags.Contains(CardTag.Defend));
        if (defend != null)
        {
            cards.Add(OutOfCombatPredictionUtils.PredictTransformResult(defend, rng));
        }

        return cards;
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictScrollBoxes(Player player)
    {
        var rewards = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var isDefect = player.Character is Defect;
        var cardPool = GetScrollBoxesCardPool(player.Character);
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
            if (isDefect && rewards.NextInt(100) < 1)
            {
                var claw = ModelDb.Card<Claw>();
                bundles.Add([claw, claw, claw]);
                continue;
            }

            var bundle = new List<CardModel>();
            var availableCommon = commonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            for (var i = 0; i < 2; i++)
            {
                var common = rewards.NextItem(availableCommon);
                if (common == null)
                {
                    break;
                }

                bundle.Add(common);
                usedCardIds.Add(common.Id);
                availableCommon.Remove(common);
            }

            var availableUncommon = uncommonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            var uncommon = rewards.NextItem(availableUncommon);
            if (uncommon != null)
            {
                bundle.Add(uncommon);
                usedCardIds.Add(uncommon.Id);
            }

            bundles.Add(bundle);
        }

        return bundles;
    }

    private static CardPoolModel GetScrollBoxesCardPool(CharacterModel character)
    {
        if (TestMode.IsOn && character is Deprived)
        {
            return ModelDb.Character<Ironclad>().CardPool;
        }

        return character.CardPool;
    }

    private static IReadOnlyList<RelicModel> PredictToyBoxRelics(Player player, int count)
    {
        var relics = OutOfCombatPredictionUtils.PredictRelicRewards(player, count)
            .Select(relic => relic.ToMutable())
            .ToList();
        foreach (var relic in relics)
        {
            relic.IsWax = true;
        }

        return relics;
    }

    private static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int count,
        CardCreationOptions options)
    {
        return OutOfCombatPredictionUtils.PredictCards(player, count, options);
    }

    private static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int count,
        CardCreationOptions options,
        Rng rewardRng,
        Rng nicheRng)
    {
        return OutOfCombatPredictionUtils.PredictCards(player, count, options, rewardRng, nicheRng);
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
