using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

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
        var context = new RunPredictionContext(player);

        // Battleworn Dummy Setting 2 rolls after the event combat ends, not when the option is chosen.
        // Known vanilla RNG consumers between option hover and BattlewornDummy.Resume:
        // 1. CombatState.CreateCreature -> Creature.SetUniqueMonsterHpValue consumes one roll even for
        //    fixed-HP monsters, because the single HP value is still selected with NextItem from Niche.
        // 2. CombatEndEffectPrediction handles predictable combat-end effects before the parent event resumes.
        context.SharedRng.Niche.NextInt(1);

        CombatEndEffectPrediction.FastForwardMonsterRoomCombatEndHooks(context);

        // v0.107.0 moved the final Setting 2 upgrade shuffle to the event-local RNG.
        // TODO: streamline this with OutOfCombatPredictionUtils.PredictUpgradedDeckCards
        return context.Deck.Cards
            .Where(card => card.Preview.IsUpgradable)
            .ToList()
            .StableShuffle(eventRng)
            .Take(2)
            .Select(card => card.Upgrade().Preview)
            .ToList();
    }
}
