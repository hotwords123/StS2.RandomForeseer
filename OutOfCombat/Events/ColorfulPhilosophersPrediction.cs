using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class ColorfulPhilosophersPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<ColorfulPhilosophers>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(ColorfulPhilosophers colorfulPhilosophers, EventOption option)
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

        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var bundles = new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare }
            .Select(rarity => OutOfCombatPredictionUtils.PredictCards(
                player,
                colorfulPhilosophers.DynamicVars.Cards.IntValue,
                new CardCreationOptions([pool], CardCreationSource.Other, CardRarityOddsType.Uniform, card => card.Rarity == rarity)
                    .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications),
                rewardRng,
                nicheRng))
            .ToList();

        return PredictionHoverTips.CardBundles(bundles);
    }
}
