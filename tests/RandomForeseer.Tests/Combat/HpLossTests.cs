using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies <see cref="DieForYouPower"/> damage redirection and simulated liveness checks,
/// plus <see cref="IntangiblePower"/> and <see cref="BeatingRemnant"/> HP-loss caps through
/// <see cref="ModifyHpLostMirrors"/> across the simulated combat boundary.
/// </summary>
/// <remarks>Death states are arranged explicitly; complete death processing and combat teardown are outside this suite.</remarks>
[Collection(GameTestCollection.Name)]
public sealed class HpLossTests : GameTestBase
{
    [Fact]
    public void OstyRedirectsOnlyPoweredOwnerDamageWhileAliveInPrediction()
    {
        var combat = new TestCombat();
        var pet = new Creature(null!, 10, 10) { CombatState = combat.Player.Creature.CombatState };
        pet._petOwner = combat.Player;
        combat.Proxy.Listeners = [TestCombat.Power<DieForYouPower>(pet, 1)];
        Creature Target(Creature target, ValueProp props) => HookMirrors.ModifyUnblockedDamageTarget(
            combat.Simulator, target, 8, props, combat.Enemy);
        Assert.Same(pet, Target(combat.Player.Creature, ValueProp.Move));
        Assert.Same(combat.Player.Creature, Target(combat.Player.Creature, ValueProp.Unpowered));
        Assert.Same(combat.OtherPlayer.Creature, Target(combat.OtherPlayer.Creature, ValueProp.Move));
        combat.Simulator.State.GetCreature(pet).LoseHp(10, ValueProp.Unpowered);
        Assert.Same(combat.Player.Creature, Target(combat.Player.Creature, ValueProp.Move));
        Assert.False(HookMirrors.ShouldAllowHitting(combat.Simulator, pet));
        Assert.True(pet.IsAlive);
        Assert.Empty(combat.Simulator.Damage([combat.Player.Creature], 8, ValueProp.Move, pet, null, null));
    }

    [Fact]
    public void DieForYouChecksShadowLivenessOfAnyQueriedCreature()
    {
        var combat = new TestCombat();
        combat.Proxy.Listeners = [TestCombat.Power<DieForYouPower>(combat.Player.Creature, 1)];
        Assert.True(HookMirrors.ShouldAllowHitting(combat.Simulator, combat.OtherPlayer.Creature));
        // Keep the enemy alive so combat-ending guards do not bypass listeners.
        combat.Simulator.State.GetCreature(combat.OtherPlayer.Creature).LoseHp(100, ValueProp.Unpowered);
        Assert.False(HookMirrors.ShouldAllowHitting(combat.Simulator, combat.OtherPlayer.Creature));
        Assert.True(combat.OtherPlayer.Creature.IsAlive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HpCapsUseShadowCombatBoundaryAndPreserveOtherTargetsAndSmallLosses(bool remnant)
    {
        var combat = new TestCombat();
        AbstractModel listener = remnant ? TestCombat.Relic<BeatingRemnant>(combat.Player)
            : TestCombat.Power<IntangiblePower>(combat.Player.Creature, 1);
        decimal Query(Creature target, decimal amount) => ModifyHpLostMirrors.InvokeAfterOsty(listener, new()
        {
            Simulator = combat.Simulator,
            Target = target,
            Amount = amount,
            Props = ValueProp.Unpowered,
            Dealer = null,
            CardSource = null
        });
        Assert.Equal(remnant ? 20m : 1m, Query(combat.Player.Creature, 30));
        Assert.Equal(30m, Query(combat.OtherPlayer.Creature, 30));
        Assert.Equal(0m, Query(combat.Player.Creature, 0));
        Assert.Equal(0.5m, Query(combat.Player.Creature, 0.5m));
        combat.Simulator.LoseCombat();
        Assert.True(combat.Simulator.CheckWinCondition());
        Assert.Equal(30m, Query(combat.Player.Creature, 30));
    }
}
