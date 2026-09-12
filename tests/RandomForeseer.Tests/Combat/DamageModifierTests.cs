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
        var combat = new TestCombat();
        var card = combat.Card<StrikeIronclad>();
        var power = TestCombat.Power<LethalityPower>(combat.Player.Creature, 50);
        Assert.Equal(1.5m, Damage(combat, power, card));
        combat.LiveHistory(card, round: 0);
        var other = combat.Card<StrikeIronclad>(combat.OtherPlayer);
        combat.Start(other);
        combat.LiveHistory(other);
        combat.Start(combat.Card<DefendIronclad>());
        Assert.Equal(1.5m, Damage(combat, power, card));
        combat.PlayerState.Hand.Remove(card);
        combat.PlayerState.PlayPile.Add(card);
        combat.Start(card);
        Assert.Null(card.Preview.Pile);
        Assert.Equal(1.5m, Damage(combat, power, card, TestCombat.Play(card)));
        Assert.Equal(1.5m, Damage(combat, power, card, TestCombat.Play(card)));
        card.MutablePreview._currentPlayIndex = 1;
        Assert.Equal(1m, Damage(combat, power, card, TestCombat.Play(card, index: 1)));
        var second = combat.Card<StrikeIronclad>(pile: PileType.Play);
        combat.Start(second);
        Assert.Equal(1m, Damage(combat, power, second, TestCombat.Play(second)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LethalityIncludesLiveCurrentTurnHistory(bool alsoSimulated)
    {
        var combat = new TestCombat();
        var card = combat.Card<StrikeIronclad>(pile: alsoSimulated ? PileType.Play : PileType.Hand);
        combat.LiveHistory(card);
        if (alsoSimulated) combat.Start(card);
        Assert.Equal(1m, Damage(combat, TestCombat.Power<LethalityPower>(combat.Player.Creature, 50), card));
    }

    [Fact]
    public void PhantomBladesWaitsForMatchingShivToFinish()
    {
        var combat = new TestCombat();
        var card = combat.Card<Shiv>(pile: PileType.Play);
        var power = TestCombat.Power<PhantomBladesPower>(combat.Player.Creature, 9);
        combat.Start(card);
        Assert.Equal(9m, Damage(combat, power, card, additive: true));
        combat.Finish(combat.Card<Shiv>(combat.OtherPlayer));
        combat.LiveHistory(card, finished: true, round: 0);
        Assert.Equal(9m, Damage(combat, power, card, additive: true));
        combat.Finish(card);
        Assert.Equal(0m, Damage(combat, power, card, additive: true));
    }

    [Fact]
    public void PhantomBladesIncludesLiveFinishedHistory()
    {
        var combat = new TestCombat();
        var card = combat.Card<Shiv>();
        combat.LiveHistory(card, finished: true);
        Assert.Equal(0m, Damage(combat, TestCombat.Power<PhantomBladesPower>(combat.Player.Creature, 9), card,
            additive: true));
    }

    [Fact]
    public void OneForAllPreviewUsesConsumedShadowCostModifiers()
    {
        var combat = new TestCombat();
        var card = combat.Card<StrikeIronclad>();
        var power = TestCombat.Power<OneForAllPower>(combat.Player.Creature, 5);
        var free = TestCombat.Power<FreeAttackPower>(combat.Player.Creature, 1);
        combat.Proxy.Listeners = [free];
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
        var combat = new TestCombat();
        var card = combat.Card(cardType);
        var power = TestCombat.Power<OneForAllPower>(combat.Player.Creature, 5);
        Assert.Equal(expected, Damage(combat, power, card, TestCombat.Play(card, energySpent: spent), additive: true));
    }

    [Theory]
    [InlineData("Lethality")]
    [InlineData("PhantomBlades")]
    [InlineData("OneForAll")]
    public void DamageBonusesIgnoreUnpoweredMissingAndOtherOwnerSources(string kind)
    {
        var combat = new TestCombat();
        var card = combat.Card<Shiv>();
        PowerModel power = kind switch
        {
            "Lethality" => TestCombat.Power<LethalityPower>(combat.Player.Creature, 50),
            "PhantomBlades" => TestCombat.Power<PhantomBladesPower>(combat.Player.Creature, 9),
            _ => TestCombat.Power<OneForAllPower>(combat.Player.Creature, 5)
        };
        var additive = power is not LethalityPower;
        var neutral = additive ? 0m : 1m;
        Assert.Equal(neutral, Damage(combat, power, card, additive: additive, props: ValueProp.Unpowered));
        Assert.Equal(neutral, Damage(combat, power, null, additive: additive));
        Assert.Equal(neutral, Damage(combat, power, combat.Card<Shiv>(combat.OtherPlayer), additive: additive,
            dealer: combat.OtherPlayer.Creature));
        if (power is PhantomBladesPower)
            Assert.Equal(0m, Damage(combat, power, combat.Card<StrikeIronclad>(), additive: true));
    }

    [Fact]
    public void CombinedDamageAppliesAdditionBeforeMultiplicationAndConsumesFirstPlayBonuses()
    {
        var combat = new TestCombat();
        var card = combat.Card<Shiv>();
        combat.Proxy.Listeners = [TestCombat.Power<OneForAllPower>(combat.Player.Creature, 5),
            TestCombat.Power<PhantomBladesPower>(combat.Player.Creature, 9),
            TestCombat.Power<LethalityPower>(combat.Player.Creature, 50)];
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
