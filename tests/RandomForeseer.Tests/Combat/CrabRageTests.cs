using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Death;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies <see cref="AfterDeathMirrors"/> grants <see cref="CrabRagePower"/> block once per prediction,
/// and subsequent <see cref="CombatPredictionSimulator.Damage"/> consumes it without changing live models.
/// </summary>
/// <remarks>
/// Death notifications are dispatched explicitly through <see cref="HookMirrors.AfterDeath"/>;
/// full creature removal, death prevention and encounter animation lifecycles are outside this fixture.
/// Strength application and power removal remain unsupported and must retain prediction risk.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class CrabRageTests : GameTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllyDeathGrantsBlockForFollowingDamageOnlyOnce(bool wasRemovalPrevented)
    {
        using var combat = new TestCombat(enemyCount: 3);
        var power = combat.ArrangePower<CrabRagePower>(combat.Enemy, 1, addToLiveCollection: true);
        combat.BeginPrediction();
        var simulator = combat.Simulator;
        var owner = simulator.State.GetCreature(combat.Enemy);

        // Vanilla CrabRagePower does not test wasRemovalPrevented.
        HookMirrors.AfterDeath(simulator, combat.Proxy.Enemies[1], wasRemovalPrevented);

        Assert.Equal(99, owner.Block);
        Assert.True(simulator.Snapshot().HasRisk);
        var hit = Assert.Single(simulator.Damage(combat.Enemy, 120m, ValueProp.Move, combat.Player.Creature));
        Assert.Equal(99, hit.BlockedDamage);
        Assert.Equal(21, hit.UnblockedDamage);
        Assert.Equal(79, owner.CurrentHp);

        HookMirrors.AfterDeath(simulator, combat.Proxy.Enemies[2], wasRemovalPrevented: false);
        Assert.Equal(0, owner.Block);
        Assert.Equal(0, combat.Enemy.Block);
        Assert.Equal(100, combat.Enemy.CurrentHp);
        Assert.Equal(1, power.Amount);
        Assert.Same(power, Assert.Single(combat.Enemy.Powers));

        using var freshSession = new CombatPredictionSession(combat.Source);
        var freshPrediction = freshSession.Simulator;
        HookMirrors.AfterDeath(freshPrediction, combat.Proxy.Enemies[1], wasRemovalPrevented: false);
        Assert.Equal(99, freshPrediction.State.GetCreature(combat.Enemy).Block);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnOrOpposingDeathDoesNotConsumeTheTrigger(bool ownDeath)
    {
        using var combat = new TestCombat(enemyCount: 2);
        var power = combat.ArrangePower<CrabRagePower>(combat.Enemy, 1, addToLiveCollection: true);
        // Read the configured BlockVar instead of hardcoding the amount in the mirror.
        power.DynamicVars.Block.BaseValue = 37m;

        combat.BeginPrediction();
        HookMirrors.AfterDeath(combat.Simulator, ownDeath ? combat.Enemy : combat.Player.Creature,
            wasRemovalPrevented: false);
        Assert.Equal(0, combat.Simulator.State.GetCreature(combat.Enemy).Block);
        Assert.False(combat.Simulator.Snapshot().HasRisk);

        HookMirrors.AfterDeath(combat.Simulator, combat.Proxy.Enemies[1], wasRemovalPrevented: false);
        Assert.Equal(37, combat.Simulator.State.GetCreature(combat.Enemy).Block);
    }
}
