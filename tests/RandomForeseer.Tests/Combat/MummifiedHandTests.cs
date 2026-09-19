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
    private static (CardModel Card, MummifiedHand Relic) ArrangeTrigger(TestCombat combat) =>
        (combat.ArrangeCard<Inflame>(pile: PileType.Play), combat.ArrangeRelic<MummifiedHand>(combat.Player));

    private static CardModel Trigger(TestCombat combat, (CardModel Card, MummifiedHand Relic) trigger)
    {
        var power = combat.Predicted(trigger.Card);
        AfterCardPlayedMirrors.Invoke(trigger.Relic, new()
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
        using var combat = new TestCombat();
        var x = combat.ArrangeCard<Whirlwind>();
        if (pool != 4) combat.ArrangeCard<Stardust>();
        var expected = pool == 1 || pool == 3 ? combat.ArrangeCard<StrikeIronclad>()
            : pool == 2 ? combat.ArrangeCard<Shiv>() : x;
        var additional = pool == 1 ? combat.ArrangeCard<Shiv>() : null;
        var trigger = ArrangeTrigger(combat);
        combat.BeginPrediction();
        combat.PlayerState.GainEnergy(5);
        combat.PlayerState.GainStars(7);
        if (additional is not null) combat.Predicted(additional).MutablePreview.EnergyCost.SetThisTurn(1);
        if (pool == 2) combat.Predicted(expected).MutablePreview.EnergyCost.SetThisTurn(1);
        if (pool == 3) combat.Predicted(expected).MutablePreview.SetToFreeThisTurn();
        Assert.Same(expected, Trigger(combat, trigger));
        var predicted = combat.Predicted(expected);
        if (!predicted.Preview.EnergyCost.CostsX)
            Assert.Equal(0, predicted.GetEnergyCostWithModifiers(combat.Simulator));
    }

    [Theory]
    [InlineData(typeof(Whirlwind))]
    [InlineData(typeof(Stardust))]
    public void LastFallbackCanSelectAnXCard(Type type)
    {
        using var combat = new TestCombat();
        var card = combat.ArrangeCard(type);
        var trigger = ArrangeTrigger(combat);
        combat.BeginPrediction();
        Assert.Same(card, Trigger(combat, trigger));
    }

    [Fact]
    public void FixedEnergyCostOnStarXRemainsEligible()
    {
        using var combat = new TestCombat();
        var card = combat.ArrangeCard<Stardust>();
        combat.ArrangeCard<Shiv>();
        var trigger = ArrangeTrigger(combat);
        combat.BeginPrediction();
        combat.Predicted(card).MutablePreview.EnergyCost.SetThisTurn(1);
        Assert.Same(card, Trigger(combat, trigger));
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
        using var combat = new TestCombat();
        var actualRng = new Rng((uint)seed);
        var expectedRng = new Rng((uint)seed);
        combat.ArrangeCard<Whirlwind>();
        combat.ArrangeCard<Stardust>();
        var energySource = combat.ArrangeCard<Shiv>();
        var defendSource = combat.ArrangeCard<DefendIronclad>();
        var starsSource = combat.ArrangeCard<Shiv>();
        var trigger = ArrangeTrigger(combat);
        combat.BeginPrediction();
        combat.Simulator.Rng.CombatCardSelection.LoadFromSerializable(actualRng.ToSerializable());
        actualRng = combat.Simulator.Rng.CombatCardSelection;
        combat.PlayerState.GainEnergy(5);
        combat.PlayerState.GainStars(7);
        var energy = combat.Predicted(energySource);
        energy.MutablePreview.EnergyCost.SetThisTurn(1);
        combat.Predicted(defendSource).MutablePreview.EnergyCost.SetThisTurn(0);
        var stars = combat.Predicted(starsSource);
        stars.MutablePreview.SetStarCostThisTurn(1);

        var expected = expectedRng.NextItem(new[] { energy.Original, stars.Original });
        Assert.Same(expected, Trigger(combat, trigger));
        Assert.Equal(expectedRng.ToSerializable().counter, actualRng.ToSerializable().counter);
        Assert.Equal(expectedRng.NextInt(), actualRng.NextInt());
        Assert.Equal(0, energy.Original.EnergyCost.GetWithModifiers(CostModifiers.Local));
        Assert.Equal(-1, stars.Original.CurrentStarCost);
        Assert.Equal(liveCounter, liveRng.ToSerializable().counter);
    }

    [Fact]
    public void EmptyHandDoesNotSelectOrAdvanceRng()
    {
        using var combat = new TestCombat();
        var trigger = ArrangeTrigger(combat);
        combat.BeginPrediction();
        var card = combat.Predicted(trigger.Card);
        var counter = combat.Simulator.Rng.CombatCardSelection.ToSerializable().counter;
        AfterCardPlayedMirrors.Invoke(trigger.Relic, new()
        {
            Simulator = combat.Simulator,
            Card = card,
            CardPlay = TestCombat.Play(card)
        });
        Assert.Empty(combat.Simulator.History.OfType<CombatPredictionCardsSelectedEntry>());
        Assert.Equal(counter, combat.Simulator.Rng.CombatCardSelection.ToSerializable().counter);
    }
}
