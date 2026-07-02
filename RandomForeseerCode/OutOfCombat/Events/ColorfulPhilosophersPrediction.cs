using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class ColorfulPhilosophersPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(ColorfulPhilosophers colorfulPhilosophers, EventOption option)
    {
        const string prefix = "COLORFUL_PHILOSOPHERS.pages.INITIAL.options.";
        if (!option.TextKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return [];
        }

        var player = colorfulPhilosophers.Owner!;
        var pool = player.UnlockState.CharacterCardPools
            .FirstOrDefault(pool => option.TextKey.EndsWith(pool.EnergyColorName.ToUpperInvariant(), StringComparison.Ordinal));
        if (pool == null)
        {
            return [];
        }

        var context = new RunPredictionContext(player);
        var bundles = new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare }
            .Select(rarity => CardRewardPrediction.PredictCards(
                context,
                colorfulPhilosophers.DynamicVars.Cards.IntValue,
                new CardCreationOptions([pool], CardCreationSource.Other, CardRarityOddsType.Uniform, card => card.Rarity == rarity)
                    .WithFlags(CardCreationFlags.NoRarityModification |
                        CardCreationFlags.NoCardPoolModifications |
                        CardCreationFlags.IsCardReward)))
            .ToList();

        return PredictionHoverTips.CardBundles(bundles);
    }
}
