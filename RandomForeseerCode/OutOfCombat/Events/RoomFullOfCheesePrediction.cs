using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class RoomFullOfCheesePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(RoomFullOfCheese roomFullOfCheese, EventOption option)
    {
        if (option.TextKey != "ROOM_FULL_OF_CHEESE.pages.INITIAL.options.GORGE")
        {
            return [];
        }

        var player = roomFullOfCheese.Owner!;
        var options = CardCreationOptions
            .ForNonCombatWithUniformOdds([player.Character.CardPool], card => card.Rarity == CardRarity.Common)
            .WithFlags(CardCreationFlags.NoRarityModification);
        return PredictionHoverTips.Cards(CardRewardPrediction.PredictCards(player, 8, options));
    }
}
