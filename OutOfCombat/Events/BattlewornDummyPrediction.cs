using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class BattlewornDummyPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(BattlewornDummy battlewornDummy, EventOption option)
    {
        if (!RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes))
        {
            return [];
        }

        var player = battlewornDummy.Owner!;
        return option.TextKey switch
        {
            "BATTLEWORN_DUMMY.pages.INITIAL.options.SETTING_1" =>
                PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictUniformPotions(player, 1)),
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

        FastForwardBeforeBattlewornDummyRewards(player, nicheRng, deckState);

        // v0.107.0 moved the final Setting 2 upgrade shuffle to the event-local RNG.
        return deckState
            .Where(card => card.IsUpgradable)
            .ToList()
            .StableShuffle(eventRng)
            .Take(2)
            .Select(PredictionUtils.ToUpgradedCard)
            .ToList();
    }

    private static void FastForwardBeforeBattlewornDummyRewards(
        Player player,
        Rng nicheRng,
        IList<CardModel> currentPlayerDeckState)
    {
        // Battleworn Dummy Setting 2 rolls after the event combat ends, not when the option is chosen.
        // Known vanilla RNG consumers between option hover and BattlewornDummy.Resume:
        // 1. CombatState.CreateCreature -> Creature.SetUniqueMonsterHpValue consumes one roll even for
        //    fixed-HP monsters, because the single HP value is still selected with NextItem from Niche.
        // 2. CombatEndEffectPrediction handles predictable combat-end effects before the parent event resumes.
        nicheRng.NextInt(1);

        CombatEndEffectPrediction.FastForwardMonsterRoomCombatEndHooks(
            player,
            nicheRng: nicheRng,
            targetPlayerDeckState: currentPlayerDeckState);
    }
}
