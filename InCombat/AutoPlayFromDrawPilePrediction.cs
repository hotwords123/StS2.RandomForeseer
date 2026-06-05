using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class AutoPlayFromDrawPilePrediction
{
    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction) ||
            !card.IsMutable ||
            card.Pile?.Type != PileType.Hand ||
            card.Owner.Creature.CombatState == null)
        {
            return [];
        }

        var count = card switch
        {
            Havoc => 1,
            Cascade cascade => PredictCascadeCount(cascade),
            _ => 0
        };

        return GetHoverTips(card.Owner, count);
    }

    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(Player player, PotionModel potion)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction) ||
            !potion.IsMutable ||
            player.Creature.CombatState == null)
        {
            return [];
        }

        var count = potion switch
        {
            DistilledChaos => potion.DynamicVars.Repeat.IntValue,
            _ => 0
        };

        return GetHoverTips(player, count);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(Player player, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var cards = DrawPilePredictionUtils.PredictTopCardsAfterNecessaryShuffles(player, count);
        return PredictionHoverTips.Cards(cards);
    }

    private static int PredictCascadeCount(Cascade cascade)
    {
        if (cascade.Owner.Creature.CombatState is not { } combatState)
        {
            return 0;
        }

        var capturedXValue = cascade.Owner.PlayerCombatState?.Energy ?? 0;
        var count = Hook.ModifyXValue(combatState, cascade, capturedXValue);
        return cascade.IsUpgraded
            ? count + 1
            : count;
    }
}
