using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class TheFutureOfPotionsPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<TheFutureOfPotions>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(TheFutureOfPotions future, EventOption option)
    {
        if (option.TextKey != "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.POTION")
        {
            return [];
        }

        var player = future.Owner!;
        var index = future.CurrentOptions.ToList().IndexOf(option);
        var potion = player.Potions.ElementAtOrDefault(index);
        var cardTypes = future._cardTypes;
        if (potion == null || cardTypes == null || !cardTypes.TryGetValue(potion, out var cardType))
        {
            return [];
        }

        var targetRarity = potion.Rarity switch
        {
            PotionRarity.Rare or PotionRarity.Event => CardRarity.Rare,
            PotionRarity.Uncommon => CardRarity.Uncommon,
            _ => CardRarity.Common
        };
        var options = CardCreationOptions
            .ForNonCombatWithUniformOdds([player.Character.CardPool], card => card.Rarity == targetRarity && card.Type == cardType)
            .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications);
        var cards = OutOfCombatPredictionUtils.PredictCards(
            player,
            3,
            options,
            PredictionUtils.CloneRng(player.PlayerRng.Rewards),
            PredictionUtils.CloneRng(player.RunState.Rng.Niche),
            afterGenerated: null)
            .Select(PredictionUtils.ToUpgradedCard)
            .ToList();

        return PredictionHoverTips.Cards(cards);
    }
}
