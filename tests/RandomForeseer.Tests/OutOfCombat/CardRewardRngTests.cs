using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using RandomForeseer.Common;
using RandomForeseer.OutOfCombat;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.OutOfCombat;

/// <summary>
/// Verifies that <see cref="CardRewardPrediction.CreateBaseRewards"/> uses a caller's RNG override for the
/// entire reward generation, including rarity, while preserving encounter-only rarity progression.
/// </summary>
/// <remarks>Uses a small real card pool in a null run; reward modifiers, UI and live card registration are excluded.</remarks>
[Collection(GameTestCollection.Name)]
public sealed class CardRewardRngTests : GameTestBase
{
    [Theory]
    [InlineData(CardCreationSource.Encounter)]
    [InlineData(CardCreationSource.Other)]
    public void RarityAndCardSelectionShareOverrideWithoutAdvancingRewards(CardCreationSource source)
    {
        ModelDb.Inject(typeof(Ironclad));
        ModelDb.Inject(typeof(MockCardPool));
        ModelDb.Inject(typeof(Anger));
        ModelDb.Inject(typeof(Inflame));
        ModelDb.Inject(typeof(Feed));
        var player = new Player(ModelDb.Character<Ironclad>(), 1, 80, 80, 3, 99, 3, 0,
            new RelicGrabBag(), new UnlockState([], [], 2));
        var pool = MockCardPool.Create(ModelDb.Card<Anger>(), ModelDb.Card<Inflame>(), ModelDb.Card<Feed>());
        var context = new RunPredictionContext(player);
        var rewardRng = context.Rng.Rewards;
        var overrideRng = context.Rng.Shops;
        var expectedRng = overrideRng.Clone();
        var untouchedRewards = rewardRng.Clone();
        var untouchedLiveShops = player.PlayerRng.Shops.Clone();
        var expectedOdds = new CardRarityOdds(context.CardRarityOdds.CurrentValue, expectedRng);
        var expectedRarity = source == CardCreationSource.Encounter
            ? expectedOdds.Roll(CardRarityOddsType.RegularEncounter)
            : expectedOdds.RollWithBaseOdds(CardRarityOddsType.RegularEncounter);
        var expectedCard = expectedRng.NextItem(pool.AllCards.Where(card => card.Rarity == expectedRarity))!;
        var options = new CardCreationOptions([pool], source, CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.NoUpgradeRoll)
            .WithRngOverride(overrideRng);

        var result = Assert.Single(CardRewardPrediction.CreateBaseRewards(
            player,
            1,
            options,
            rewardRng,
            context.CardRarityOdds));

        Assert.Equal(expectedCard.Id, result.Card.Id);
        Assert.Equal(expectedOdds.CurrentValue, context.CardRarityOdds.CurrentValue);
        Assert.Equal(expectedRng.NextUnsignedLong(), overrideRng.NextUnsignedLong());
        Assert.Equal(untouchedRewards.NextUnsignedLong(), rewardRng.NextUnsignedLong());
        Assert.Equal(untouchedLiveShops.NextUnsignedLong(), player.PlayerRng.Shops.NextUnsignedLong());
    }
}
