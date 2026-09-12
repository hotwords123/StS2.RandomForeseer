using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies <see cref="CombatPredictedCardExtensions"/> cost queries, resource ownership and X values,
/// together with <see cref="CombatPredictionSimulator.CanPlay"/> and manual/automatic resource payment.
/// </summary>
/// <remarks>Exercises local and simulated global modifiers while checking that live resources remain unchanged.</remarks>
[Collection(GameTestCollection.Name)]
public sealed class CardCostTests : GameTestBase
{
    // SpendResources is private; keep its one unavoidable reflection boundary typed and centralized.
    private delegate ResourceInfo SpendResources(CombatPredictionSimulator simulator, PredictedCard card,
        bool isAutoPlay, bool skipXCapture);
    private static readonly SpendResources Spend = AccessTools.Method(typeof(CombatPredictionSimulator), "SpendResources")
        .CreateDelegate<SpendResources>();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnergyXUsesShadowEnergyForPaymentButNotCostQuery(bool autoPlay)
    {
        var combat = new TestCombat();
        var card = combat.Card<Whirlwind>();
        combat.PlayerState.GainEnergy(5);
        Assert.Equal(0, card.GetEnergyCostWithModifiers(combat.Simulator));
        Assert.Equal(5, card.GetEnergyAmountToSpend(combat.Simulator));

        var resources = Spend(combat.Simulator, card, autoPlay, false);
        Assert.Equal(5, resources.EnergyValue);
        Assert.Equal(autoPlay ? 0 : 5, resources.EnergySpent);
        Assert.Equal(autoPlay ? 5 : 0, combat.PlayerState.Energy);
        Assert.Equal(5, card.Preview.EnergyCost.CapturedXValue);
        Assert.Equal(0, combat.Player.PlayerCombatState!.Energy);
        Assert.Equal(0, card.GetEnergyCostWithModifiers(combat.Simulator));
        Assert.Equal(combat.PlayerState.Energy, card.GetEnergyAmountToSpend(combat.Simulator));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StarXQueriesAndCapturesShadowStars(bool autoPlay)
    {
        var combat = new TestCombat();
        var card = combat.Card<Stardust>();
        combat.PlayerState.GainStars(7);
        Assert.Equal(7, card.GetStarCostWithModifiers(combat.Simulator));
        var resources = Spend(combat.Simulator, card, autoPlay, false);
        Assert.Equal(7, resources.StarValue);
        Assert.Equal(autoPlay ? 0 : 7, resources.StarsSpent);
        Assert.Equal(autoPlay ? 7 : 0, combat.PlayerState.Stars);
        Assert.Equal(0, combat.Player.PlayerCombatState!.Stars);
    }

    [Fact]
    public void FixedCostsApplyLocalAndShadowGlobalModifiers()
    {
        var combat = new TestCombat();
        var card = combat.Card<StrikeIronclad>();
        card.MutablePreview.EnergyCost.SetThisTurn(2);
        Assert.Equal(2, card.GetEnergyCostWithModifiers(combat.Simulator));
        Assert.Equal(2, card.GetEnergyAmountToSpend(combat.Simulator));
        combat.Proxy.Listeners = [TestCombat.Power<FreeAttackPower>(combat.Player.Creature, 1)];
        Assert.Equal(0, card.GetEnergyCostWithModifiers(combat.Simulator));
    }

    [Fact]
    public void ResourcesAreResolvedForTheCardOwner()
    {
        var combat = new TestCombat();
        combat.PlayerState.GainEnergy(5);
        combat.PlayerState.GainStars(7);
        Assert.Equal(0, combat.Card<Whirlwind>(combat.OtherPlayer).GetEnergyAmountToSpend(combat.Simulator));
        Assert.Equal(0, combat.Card<Stardust>(combat.OtherPlayer).GetStarCostWithModifiers(combat.Simulator));
    }

    [Theory]
    [InlineData(typeof(Whirlwind), null, null, false)]
    [InlineData(typeof(Stardust), null, null, false)]
    [InlineData(typeof(StrikeIronclad), null, null, true)]
    [InlineData(typeof(Shiv), null, null, false)]
    [InlineData(typeof(Whirlwind), null, 2, true)]
    [InlineData(typeof(Stardust), 2, null, true)]
    [InlineData(typeof(StrikeIronclad), 0, 0, false)]
    [InlineData(typeof(Shiv), null, 2, true)]
    public void ResourcePredicateExcludesEachXResourceIndependently(Type type, int? energy, int? stars, bool expected)
    {
        var combat = new TestCombat();
        combat.PlayerState.GainEnergy(4);
        combat.PlayerState.GainStars(6);
        var card = combat.Card(type);
        if (energy is { } e) card.MutablePreview.EnergyCost.SetThisTurn(e);
        if (stars is { } s) card.MutablePreview.SetStarCostThisTurn(s);
        Assert.Equal(expected, card.CostsEnergyOrStars(combat.Simulator));
        // No global listeners in this case: vanilla is an independent predicate oracle.
        Assert.Equal(card.Preview.CostsEnergyOrStars(true), card.CostsEnergyOrStars(combat.Simulator));
        if (stars is > 0 && !card.Preview.HasStarCostX)
            Assert.Equal(stars.Value, card.GetStarCostWithModifiers(combat.Simulator));
    }

    [Fact]
    public void CanPlayUsesSimulatedResourcesAndAcceptsZeroResourceXCards()
    {
        var combat = new TestCombat();
        Assert.True(combat.Simulator.CanPlay(combat.Card<Whirlwind>()));
        Assert.True(combat.Simulator.CanPlay(combat.Card<Stardust>()));
        var card = combat.Card<StrikeIronclad>();
        Assert.False(combat.Simulator.CanPlay(card));
        combat.PlayerState.GainEnergy(1);
        Assert.True(combat.Simulator.CanPlay(card));
        card.MutablePreview.SetStarCostThisTurn(2);
        Assert.False(combat.Simulator.CanPlay(card));
        combat.PlayerState.GainStars(2);
        Assert.True(combat.Simulator.CanPlay(card));
    }
}
