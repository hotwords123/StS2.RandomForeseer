using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class BattlewornDummyPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<BattlewornDummy>(
            GetHoverTips,
            PredictionFairness.UnfairInAllModes);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(BattlewornDummy battlewornDummy, EventOption option)
    {
        var player = battlewornDummy.Owner!;
        return option.TextKey switch
        {
            "BATTLEWORN_DUMMY.pages.INITIAL.options.SETTING_1" =>
                PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictUniformPotions(
                    player,
                    1,
                    player.PlayerRng.Rewards)),
            "BATTLEWORN_DUMMY.pages.INITIAL.options.SETTING_2" =>
                PredictionHoverTips.Cards(PredictSetting2(battlewornDummy)),
            "BATTLEWORN_DUMMY.pages.INITIAL.options.SETTING_3" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, 1)),
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictSetting2(BattlewornDummy battlewornDummy)
    {
        var player = battlewornDummy.Owner!;
        var eventRng = PredictionUtils.CloneRng(battlewornDummy.Rng);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var deckState = PileType.Deck.GetPile(player).Cards
            .Select(card => (CardModel)card.MutableClone())
            .ToList();

        FastForwardBeforeBattlewornDummyRewards(player, nicheRng, eventRng, deckState);

        return deckState
            .Where(card => card.IsUpgradable)
            .ToList()
            .StableShuffle(eventRng)
            .Take(2)
            .Select(PredictionUtils.ToUpgradedPreviewCard)
            .ToList();
    }

    private static void FastForwardBeforeBattlewornDummyRewards(
        Player player,
        Rng nicheRng,
        Rng eventRng,
        IReadOnlyList<CardModel> currentPlayerDeckState)
    {
        // Battleworn Dummy Setting 2 rolls after the event combat ends, not when the option is chosen.
        // Known vanilla RNG consumers between option hover and BattlewornDummy.Resume:
        // 1. CombatState.CreateCreature -> Creature.SetUniqueMonsterHpValue consumes one roll even for
        //    fixed-HP monsters, because the single HP value is still selected with NextItem from Niche.
        // 2. Fishing Rod's AfterCombatEnd runs before the parent event resumes. If its counter triggers,
        //    it consumes one Niche roll and may upgrade a card before Battleworn Dummy chooses Setting 2.
        // 3. v0.107.0 moved the final Setting 2 upgrade shuffle to the event-local RNG.
        nicheRng.NextInt(1);

        foreach (var runPlayer in player.RunState.Players)
        {
            FastForwardFishingRodAfterCombatEnd(runPlayer, nicheRng, runPlayer == player ? currentPlayerDeckState : null);
        }

        var slotIndex = player.RunState.GetPlayerSlotIndex(player);
        for (var i = 0; i < slotIndex; i++)
        {
            PileType.Deck.GetPile(player.RunState.Players[i]).Cards
                .Where(card => card?.IsUpgradable ?? false)
                .ToList()
                .StableShuffle(eventRng);
        }
    }

    private static void FastForwardFishingRodAfterCombatEnd(
        Player player,
        Rng rng,
        IReadOnlyList<CardModel>? mutableDeckState)
    {
        if (!ShouldFishingRodTriggerAfterThisCombat(player))
        {
            return;
        }

        var candidates = (mutableDeckState ?? PileType.Deck.GetPile(player).Cards)
            .Where(card => card.IsUpgradable)
            .ToList();
        var card = rng.NextItem(candidates);
        if (card != null && mutableDeckState != null)
        {
            PredictionUtils.UpgradePreviewCardInPlace(card);
        }
    }

    private static bool ShouldFishingRodTriggerAfterThisCombat(Player player)
    {
        return player.Relics
            .OfType<FishingRod>()
            .Any(relic => (relic.CombatsSeen + 1) % relic.DynamicVars["Combats"].IntValue == 0);
    }
}
