using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies <see cref="MummifiedHand"/> selection through <see cref="AfterCardPlayedMirrors"/>:
/// candidate-pool priority, X-resource filtering, RNG sequence and original-model isolation.
/// </summary>
/// <remarks>Invokes the after-play mirror directly with a prepared hand and deterministic selection RNG.</remarks>
[Collection(GameTestCollection.Name)]
public sealed class MummifiedHandTests : GameTestBase
{
    private static CardModel Trigger(TestCombat combat)
    {
        var power = combat.Card<Inflame>(pile: PileType.Play);
        AfterCardPlayedMirrors.Invoke(TestCombat.Relic<MummifiedHand>(combat.Player), new()
        {
            Simulator = combat.Simulator,
            Card = power,
            CardPlay = TestCombat.Play(power)
        });
        var selection = Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardsSelectedEntry>());
        return Assert.Single(selection.Cards).Original;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SelectsFromTheFirstNonemptyFallbackPool(int pool)
    {
        var combat = new TestCombat();
        var x = combat.Card<Whirlwind>();
        combat.Card<Stardust>();
        combat.PlayerState.GainEnergy(5);
        combat.PlayerState.GainStars(7);
        PredictedCard expected;
        if (pool == 1)
        {
            expected = combat.Card<StrikeIronclad>();
            combat.Card<Shiv>().MutablePreview.EnergyCost.SetThisTurn(1);
        }
        else if (pool == 2)
        {
            expected = combat.Card<Shiv>();
            expected.MutablePreview.EnergyCost.SetThisTurn(1);
        }
        else if (pool == 3)
        {
            expected = combat.Card<StrikeIronclad>();
            expected.MutablePreview.SetToFreeThisTurn();
        }
        else
        {
            combat.PlayerState.Hand.Remove(combat.PlayerState.Hand.Cards.Last());
            expected = x;
        }
        Assert.Same(expected.Original, Trigger(combat));
        if (!expected.Preview.EnergyCost.CostsX)
            Assert.Equal(0, expected.GetEnergyCostWithModifiers(combat.Simulator));
    }

    [Theory]
    [InlineData(typeof(Whirlwind))]
    [InlineData(typeof(Stardust))]
    public void LastFallbackCanSelectAnXCard(Type type)
    {
        var combat = new TestCombat();
        var card = combat.Card(type);
        Assert.Same(card.Original, Trigger(combat));
    }

    [Fact]
    public void FixedEnergyCostOnStarXRemainsEligible()
    {
        var combat = new TestCombat();
        var card = combat.Card<Stardust>();
        card.MutablePreview.EnergyCost.SetThisTurn(1);
        combat.Card<Shiv>();
        Assert.Same(card.Original, Trigger(combat));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(29)]
    [InlineData(71)]
    public void SelectionPreservesCandidateOrderAndRngSequence(int seed)
    {
        var liveRng = NullRunState.Instance.Rng.CombatCardSelection;
        var liveCounter = liveRng.ToSerializable().counter;
        var combat = new TestCombat();
        var actualRng = new Rng((uint)seed);
        var expectedRng = new Rng((uint)seed);
        combat.Simulator.Rng.CombatCardSelection.LoadFromSerializable(actualRng.ToSerializable());
        actualRng = combat.Simulator.Rng.CombatCardSelection;
        combat.PlayerState.GainEnergy(5);
        combat.PlayerState.GainStars(7);
        combat.Card<Whirlwind>();
        combat.Card<Stardust>();
        var energy = combat.Card<Shiv>();
        energy.MutablePreview.EnergyCost.SetThisTurn(1);
        combat.Card<DefendIronclad>().MutablePreview.EnergyCost.SetThisTurn(0);
        var stars = combat.Card<Shiv>();
        stars.MutablePreview.SetStarCostThisTurn(1);

        var expected = expectedRng.NextItem(new[] { energy.Original, stars.Original });
        Assert.Same(expected, Trigger(combat));
        Assert.Equal(expectedRng.ToSerializable().counter, actualRng.ToSerializable().counter);
        Assert.Equal(expectedRng.NextInt(), actualRng.NextInt());
        Assert.Equal(0, energy.Original.EnergyCost.GetWithModifiers(CostModifiers.Local));
        Assert.Equal(-1, stars.Original.CurrentStarCost);
        Assert.Equal(liveCounter, liveRng.ToSerializable().counter);
    }

    [Fact]
    public void EmptyHandDoesNotSelectOrAdvanceRng()
    {
        var combat = new TestCombat();
        var card = combat.Card<Inflame>(pile: PileType.Play);
        var counter = combat.Simulator.Rng.CombatCardSelection.ToSerializable().counter;
        AfterCardPlayedMirrors.Invoke(TestCombat.Relic<MummifiedHand>(combat.Player), new()
        {
            Simulator = combat.Simulator,
            Card = card,
            CardPlay = TestCombat.Play(card)
        });
        Assert.Empty(combat.Simulator.History.OfType<CombatPredictionCardsSelectedEntry>());
        Assert.Equal(counter, combat.Simulator.Rng.CombatCardSelection.ToSerializable().counter);
    }
}
