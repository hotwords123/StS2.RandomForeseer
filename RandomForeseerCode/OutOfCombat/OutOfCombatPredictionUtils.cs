using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer;

internal static class OutOfCombatPredictionUtils
{
    public static IReadOnlyList<CardModel> PredictCards(
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

    public static IReadOnlyList<CardModel> PredictCards(
        Player player,
        int count,
        CardCreationOptions options,
        Rng rewardRng,
        Rng nicheRng,
        Action? afterGenerated = null)
    {
        return CardRewardPrediction.PredictCards(player, count, options, rewardRng, nicheRng, afterGenerated);
    }

    public static CardCreationOptions CreateCharacterCardRewardOptions(Player player)
    {
        return new CardCreationOptions(
                [player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.IsCardReward);
    }

    public static IReadOnlyList<IReadOnlyList<CardModel>> PredictCardRewardBundles(
        Player player,
        int rewardCount,
        int optionCount,
        CardCreationOptions options)
    {
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);

        return Enumerable.Range(0, rewardCount)
            .Select(_ => (IReadOnlyList<CardModel>)PredictCards(player, optionCount, options, rewardRng, nicheRng))
            .ToList();
    }

    public static IReadOnlyList<CardModel> PredictUpgradedDeckCards(
        Player player,
        int count,
        Func<CardModel, bool> filter)
    {
        var rng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        return PileType.Deck.GetPile(player).Cards
            .Where(filter)
            .ToList()
            .StableShuffle(rng)
            .Take(count)
            .Select(PredictionUtils.ToUpgradedPreviewCard)
            .ToList();
    }

    public static IReadOnlyList<PotionModel> PredictPotions(Player player, int count, Rng rng)
    {
        return PotionFactory.CreateRandomPotionsOutOfCombat(player, count, PredictionUtils.CloneRng(rng))
            .Select(potion => potion.ToMutable())
            .ToList();
    }

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, int count)
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

    public static IReadOnlyList<RelicModel> PredictRelicRewards(Player player, IEnumerable<RelicRarity> rarities)
    {
        var grabBag = RelicGrabBag.FromSerializable(player.RelicGrabBag.ToSerializable());
        return rarities
            .Select(rarity => (grabBag.PullFromFront(rarity, player.RunState) ?? RelicFactory.FallbackRelic).ToMutable())
            .ToList();
    }
}
