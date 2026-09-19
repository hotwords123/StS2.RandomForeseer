using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies <see cref="ModifyDamageMirrors"/> conditions for <see cref="LethalityPower"/>,
/// <see cref="PhantomBladesPower"/> and <see cref="OneForAllPower"/>, including live/simulated play history
/// and additive-before-multiplicative composition through <see cref="HookMirrors.ModifyDamage"/>.
/// </summary>
/// <remarks>Play history and pile transitions are arranged explicitly; these tests do not execute complete card plays.</remarks>
[Collection(GameTestCollection.Name)]
public sealed class DamageModifierTests : GameTestBase
{
    private static decimal Damage(TestCombat combat, AbstractModel listener, PredictedCard? card,
        CardPlay? play = null, bool additive = false, ValueProp props = ValueProp.Move, Creature? dealer = null)
    {
        var context = new ModifyDamageMirrorContext
        {
            Simulator = combat.Simulator,
            Target = combat.Enemy,
            Dealer = dealer ?? combat.Player.Creature,
            CardSource = card,
            CardPlay = play,
            Amount = 10,
            Props = props
        };
        return additive ? ModifyDamageMirrors.InvokeAdditive(listener, context)
            : ModifyDamageMirrors.InvokeMultiplicative(listener, context);
    }

    [Fact]
    public void LethalityUsesStartedHistoryAndShadowPlayPile()
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<StrikeIronclad>();
        var otherSource = combat.ArrangeCard<StrikeIronclad>(combat.OtherPlayer);
        var defendSource = combat.ArrangeCard<DefendIronclad>();
        var secondSource = combat.ArrangeCard<StrikeIronclad>();
        var power = combat.ArrangePower<LethalityPower>(combat.Player.Creature, 50);
        combat.ArrangeHistory(sourceCard, round: 0);
        combat.ArrangeHistory(otherSource);
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        var other = combat.Predicted(otherSource);
        Assert.Equal(1.5m, Damage(combat, power, card));
        combat.Start(other);
        combat.Start(combat.Predicted(defendSource));
        Assert.Equal(1.5m, Damage(combat, power, card));
        combat.PlayerState.Hand.Remove(card);
        combat.PlayerState.PlayPile.Add(card);
        combat.Start(card);
        Assert.Equal(1.5m, Damage(combat, power, card, TestCombat.Play(card)));
        Assert.Equal(1.5m, Damage(combat, power, card, TestCombat.Play(card)));
        card.MutablePreview._currentPlayIndex = 1;
        Assert.Equal(1m, Damage(combat, power, card, TestCombat.Play(card, index: 1)));
        var second = combat.Predicted(secondSource);
        combat.PlayerState.Hand.Remove(second);
        combat.PlayerState.PlayPile.Add(second);
        combat.Start(second);
        Assert.Equal(1m, Damage(combat, power, second, TestCombat.Play(second)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LethalityIncludesLiveCurrentTurnHistory(bool alsoSimulated)
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<StrikeIronclad>(pile: alsoSimulated ? PileType.Play : PileType.Hand);
        combat.ArrangeHistory(sourceCard);
        var power = combat.ArrangePower<LethalityPower>(combat.Player.Creature, 50);
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        if (alsoSimulated) combat.Start(card);
        Assert.Equal(1m, Damage(combat, power, card));
    }

    [Fact]
    public void PhantomBladesWaitsForMatchingShivToFinish()
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<Shiv>(pile: PileType.Play);
        var otherSource = combat.ArrangeCard<Shiv>(combat.OtherPlayer);
        var power = combat.ArrangePower<PhantomBladesPower>(combat.Player.Creature, 9);
        combat.ArrangeHistory(sourceCard, finished: true, round: 0);
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        combat.Start(card);
        Assert.Equal(9m, Damage(combat, power, card, additive: true));
        combat.Finish(combat.Predicted(otherSource));
        Assert.Equal(9m, Damage(combat, power, card, additive: true));
        combat.Finish(card);
        Assert.Equal(0m, Damage(combat, power, card, additive: true));
    }

    [Fact]
    public void PhantomBladesIncludesLiveFinishedHistory()
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<Shiv>();
        combat.ArrangeHistory(sourceCard, finished: true);
        var power = combat.ArrangePower<PhantomBladesPower>(combat.Player.Creature, 9);
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        Assert.Equal(0m, Damage(combat, power, card,
            additive: true));
    }

    [Fact]
    public void OneForAllPreviewUsesConsumedShadowCostModifiers()
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<StrikeIronclad>();
        var power = combat.ArrangePower<OneForAllPower>(combat.Player.Creature, 5);
        var free = combat.ArrangePower<FreeAttackPower>(combat.Player.Creature, 1);
        combat.Proxy.Listeners = [free];
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        Assert.Equal(5m, Damage(combat, power, card, additive: true));
        combat.Simulator.StateStore.GetPowerAmount(free).Consume();
        Assert.Equal(0m, Damage(combat, power, card, additive: true));
        Assert.Equal(1, free.Amount);
    }

    [Theory]
    [InlineData(typeof(StrikeIronclad), 0, 5)]
    [InlineData(typeof(StrikeIronclad), 1, 0)]
    [InlineData(typeof(Whirlwind), 0, 0)]
    public void OneForAllExecutionUsesSpentEnergyAndExcludesX(Type cardType, int spent, int expected)
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard(cardType);
        var power = combat.ArrangePower<OneForAllPower>(combat.Player.Creature, 5);
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        Assert.Equal(expected, Damage(combat, power, card, TestCombat.Play(card, energySpent: spent), additive: true));
    }

    [Theory]
    [InlineData("Lethality")]
    [InlineData("PhantomBlades")]
    [InlineData("OneForAll")]
    public void DamageBonusesIgnoreUnpoweredMissingAndOtherOwnerSources(string kind)
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<Shiv>();
        PowerModel power = kind switch
        {
            "Lethality" => combat.ArrangePower<LethalityPower>(combat.Player.Creature, 50),
            "PhantomBlades" => combat.ArrangePower<PhantomBladesPower>(combat.Player.Creature, 9),
            _ => combat.ArrangePower<OneForAllPower>(combat.Player.Creature, 5)
        };
        var otherSource = combat.ArrangeCard<Shiv>(combat.OtherPlayer);
        var strikeSource = combat.ArrangeCard<StrikeIronclad>();
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        var additive = power is not LethalityPower;
        var neutral = additive ? 0m : 1m;
        Assert.Equal(neutral, Damage(combat, power, card, additive: additive, props: ValueProp.Unpowered));
        Assert.Equal(neutral, Damage(combat, power, null, additive: additive));
        Assert.Equal(neutral, Damage(combat, power, combat.Predicted(otherSource), additive: additive,
            dealer: combat.OtherPlayer.Creature));
        if (power is PhantomBladesPower)
            Assert.Equal(0m, Damage(combat, power, combat.Predicted(strikeSource), additive: true));
    }

    [Fact]
    public void CombinedDamageAppliesAdditionBeforeMultiplicationAndConsumesFirstPlayBonuses()
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<Shiv>();
        combat.Proxy.Listeners = [combat.ArrangePower<OneForAllPower>(combat.Player.Creature, 5),
            combat.ArrangePower<PhantomBladesPower>(combat.Player.Creature, 9),
            combat.ArrangePower<LethalityPower>(combat.Player.Creature, 50)];
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        decimal Query() => HookMirrors.ModifyDamage(combat.Simulator, combat.Enemy, combat.Player.Creature,
            10, ValueProp.Move, card, null);
        Assert.Equal(36m, Query());
        combat.Start(card);
        combat.Finish(card);
        Assert.Equal(15m, Query());
        Assert.Empty(CombatManager.Instance.History.CardPlaysStarted);
        Assert.Empty(CombatManager.Instance.History.CardPlaysFinished);
    }
}
