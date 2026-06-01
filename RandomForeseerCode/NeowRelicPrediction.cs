using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
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
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace RandomForeseer;

internal static class NeowRelicPrediction
{
    public static IEventOptionPredictionProvider Provider { get; } = new NeowRelicPredictionProvider();

    private static readonly HashSet<Type> WarnedRelicTypes = [];

    public static IReadOnlyList<IHoverTip> GetHoverTips(Neow neow, RelicModel relic)
    {
        if (!RandomForeseerSettings.EnableNeowRelicPrediction)
        {
            return [];
        }

        var player = neow.Owner;
        if (player == null)
        {
            return [];
        }

        try
        {
            return relic switch
            {
                ArcaneScroll => PredictionHoverTips.Cards(PredictRareCharacterCards(player, 1)),
                HeftyTablet => PredictionHoverTips.Cards(PredictRareCharacterCards(player, 3)),
                LeadPaperweight => PredictionHoverTips.Cards(PredictColorlessCards(player, 2)),
                MassiveScroll => PredictionHoverTips.Cards(PredictMultiplayerCards(player, 3)),
                Kaleidoscope => PredictionHoverTips.Cards(PredictKaleidoscopeCards(player)),
                LostCoffer => PredictLostCofferTips(player),
                PhialHolster => PredictionHoverTips.Potions(PotionFactory.CreateRandomPotionsOutOfCombat(
                        player,
                        relic.DynamicVars["Potions"].IntValue,
                        PredictionUtils.CloneRng(player.RunState.Rng.CombatPotionGeneration))
                    .Select(potion => potion.ToMutable())),
                ScrollBoxes => PredictionHoverTips.Cards(PredictScrollBoxes(player).SelectMany(bundle => bundle)),
                SmallCapsule => PredictionHoverTips.Relics(PredictRelicRewards(player, 1)),
                LargeCapsule => PredictionHoverTips.Relics(PredictRelicRewards(player, relic.DynamicVars["Relics"].IntValue)),
                NeowsBones => PredictNeowsBonesTips(player, relic),
                NewLeaf => PredictionHoverTips.Cards(PredictNewLeaf(player)),
                LeafyPoultice => PredictionHoverTips.Cards(PredictLeafyPoultice(player)),
                _ => []
            };
        }
        catch (Exception ex)
        {
            WarnOnce(relic.GetType(), $"Could not predict Neow relic result for {relic.Id}: {ex}");
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

    private static IReadOnlyList<CardModel> PredictKaleidoscopeCards(Player player)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var cards = new List<CardModel>();

        for (var i = 0; i < 2; i++)
        {
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
                cards.AddRange(PredictCards(player, 1, options, rewardRng, nicheRng));
            }
        }

        return cards;
    }

    private static IReadOnlyList<IHoverTip> PredictLostCofferTips(Player player)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var options = new CardCreationOptions(
                [player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.IsCardReward);
        var tips = PredictionHoverTips.Cards(PredictCards(player, 3, options, rewardRng, nicheRng)).ToList();

        var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, rewardRng).ToMutable();
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
        tips.AddRange(PredictionHoverTips.Cards(PredictNeowsBonesCurses(player, relic.DynamicVars["Curses"].IntValue)));
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
            curses.Add(player.RunState.CreateCard(card, player));
        }

        return curses;
    }

    private static IReadOnlyList<CardModel> PredictNewLeaf(Player player)
    {
        var candidates = PileType.Deck.GetPile(player).Cards
            .Where(card => card.Type != CardType.Quest && card.IsTransformable)
            .ToList();

        return candidates
            .Select(candidate => CardFactory.CreateRandomCardForTransform(
                candidate,
                isInCombat: false,
                PredictionUtils.CloneRng(player.RunState.Rng.Niche)))
            .ToList();
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
            cards.Add(CardFactory.CreateRandomCardForTransform(strike, isInCombat: false, rng));
        }

        var defend = source.FirstOrDefault(card => card.Tags.Contains(CardTag.Defend));
        if (defend != null)
        {
            cards.Add(CardFactory.CreateRandomCardForTransform(defend, isInCombat: false, rng));
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
                bundles.Add([
                    player.RunState.CreateCard(claw, player),
                    player.RunState.CreateCard(claw, player),
                    player.RunState.CreateCard(claw, player)
                ]);
                continue;
            }

            var bundle = new List<CardModel>();
            var availableCommon = commonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            for (var i = 0; i < 2; i++)
            {
                var card = rewards.NextItem(availableCommon);
                if (card == null)
                {
                    break;
                }

                bundle.Add(player.RunState.CreateCard(card, player));
                usedCardIds.Add(card.Id);
                availableCommon.Remove(card);
            }

            var availableUncommon = uncommonCards.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            var uncommon = rewards.NextItem(availableUncommon);
            if (uncommon != null)
            {
                bundle.Add(player.RunState.CreateCard(uncommon, player));
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

    private static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count)
    {
        var rng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        var relics = new List<RelicModel>();

        for (var i = 0; i < count; i++)
        {
            var rarity = RelicFactory.RollRarity(rng);
            relics.Add((grabBag.PullFromFront(rarity, player.RunState) ?? RelicFactory.FallbackRelic).ToMutable());
        }

        return relics;
    }

    private static IReadOnlyList<CardModel> PredictCards(
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

    private static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int count,
        CardCreationOptions options,
        Rng rewardRng,
        Rng nicheRng)
    {
        return CardRewardPrediction.PredictCards(player, count, options, rewardRng, nicheRng);
    }

    private static void WarnOnce(Type relicType, string message)
    {
        if (WarnedRelicTypes.Add(relicType))
        {
            Entry.Logger.Warn(message);
        }
    }

    private sealed class NeowRelicPredictionProvider : IEventOptionPredictionProvider
    {
        public IReadOnlyList<IHoverTip> GetHoverTips(EventModel eventModel, EventOption option)
        {
            if (eventModel is not Neow neow || option.Relic == null)
            {
                return [];
            }

            return NeowRelicPrediction.GetHoverTips(neow, option.Relic);
        }
    }
}
