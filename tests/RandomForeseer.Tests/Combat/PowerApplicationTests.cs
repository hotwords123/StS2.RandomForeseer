using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies <see cref="CardOnPlayInferrer"/> and exact <see cref="CardOnPlayMirrors"/> handlers for
/// normal/upgraded power application, command ordering, <see cref="ArtifactPower"/> consumption
/// and <see cref="ViciousPower"/> draw requests.
/// </summary>
/// <remarks>
/// Power and block commands use their actual mirrors. Attack execution and drawing are replaced with
/// recorders, so assertions cover command order and state changes rather than attack/draw internals.
/// Unsupported inference paths declare their expected behavior and limitation at the corresponding skipped test.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class PowerApplicationTests() : GameTestBase(observePowerCommands: true)
{
    private static void Play(TestCombat combat, Type type, bool upgraded = false, bool inferred = false)
    {
        var card = combat.Card(type, pile: PileType.Play);
        var preview = card.MutablePreview;
        if (upgraded)
        {
            preview.UpgradeInternal();
            preview.FinalizeUpgradeInternal();
        }
        if (preview is MadScience madScience)
        {
            madScience.TinkerTimeType = CardType.Attack;
            madScience.TinkerTimeRider = TinkerTime.RiderEffect.Sapping;
        }
        var context = new CardOnPlayMirrorContext
        {
            Simulator = combat.Simulator,
            Card = card,
            CardPlay = TestCombat.Play(card, target: combat.Enemy)
        };
        if (inferred)
        {
            var action = CardOnPlayInferrer.Infer(type, AccessTools.Method(type, "OnPlay"));
            Assert.NotNull(action);
            action(preview, context);
        }
        else
        {
            Assert.True(CardOnPlayMirrors.CanMirror(preview));
            CardOnPlayMirrors.Invoke(combat.Simulator, card, context.CardPlay);
        }
    }

    [Fact]
    public void ArtifactConsumesWeakBeforeUpgradedVulnerableWithoutMutatingLiveModels()
    {
        var combat = new TestCombat();
        var artifact = TestCombat.Power<ArtifactPower>(combat.Enemy, 1, true);
        Play(combat, typeof(Putrefy), upgraded: true, inferred: true);
        Assert.Equal(0, combat.Amount(artifact));
        Assert.Equal(1, artifact.Amount);
        Assert.Contains(PowerChanges, change => change.Name == nameof(VulnerablePower) && change.Amount == 3);
        Assert.Null(combat.Enemy.GetPower<VulnerablePower>());
    }

    [Theory]
    [InlineData(typeof(Shockwave), false, "WeakPower:1,VulnerablePower:1,WeakPower:2,VulnerablePower:2")]
    [InlineData(typeof(MeteorShower), true, "Attack,WeakPower:1,WeakPower:2,VulnerablePower:1,VulnerablePower:2")]
    public void MultiTargetApplicationsKeepVanillaCommandOrder(Type type, bool inferred, string expected)
    {
        var combat = new TestCombat(2);
        Play(combat, type, inferred: inferred);
        Assert.Equal(expected.Split(','), Calls);
    }

    [Fact]
    public void ExposeBreaksShadowBlockAndRemovesArtifactBeforeApplyingVulnerable()
    {
        var combat = new TestCombat();
        var artifact = TestCombat.Power<ArtifactPower>(combat.Enemy, 2, true);
        combat.Enemy._block = 12;
        Play(combat, typeof(Expose));
        Assert.Equal(0, combat.Simulator.State.GetCreature(combat.Enemy).Block);
        Assert.Equal(12, combat.Enemy.Block);
        Assert.Equal(1, BlockBreaks);
        Assert.Equal(0, combat.Amount(artifact));
        Assert.Contains(PowerChanges, change => change.Name == nameof(VulnerablePower) && change.Amount == 2);
    }

    [Theory]
    [InlineData(0, false, true, 2)]
    [InlineData(4, false, false, 5)]
    [InlineData(0, true, false, 0)]
    public void DominateUsesResultingShadowVulnerableForStrength(int initial, bool artifact, bool upgraded, int strength)
    {
        var combat = new TestCombat();
        var power = initial > 0 ? TestCombat.Power<VulnerablePower>(combat.Enemy, initial, true) : null;
        if (artifact) TestCombat.Power<ArtifactPower>(combat.Enemy, 1, true);
        Play(combat, typeof(Dominate), upgraded: upgraded);
        if (strength == 0) Assert.DoesNotContain(PowerChanges, change => change.Name == nameof(StrengthPower));
        else Assert.Contains(PowerChanges, change => change.Name == nameof(StrengthPower) && change.Amount == strength);
        if (power is not null) Assert.Equal(initial, power.Amount);
    }

    [Fact]
    public void RepeatedMoltenFistDoublesShadowVulnerable()
    {
        var combat = new TestCombat();
        var power = TestCombat.Power<VulnerablePower>(combat.Enemy, 3, true);
        Play(combat, typeof(MoltenFist));
        Play(combat, typeof(MoltenFist));
        Assert.Equal(12, combat.Amount(power));
        Assert.Equal(3, power.Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MoltenFistSkipsAbsentVulnerableOrDeadTargets(bool killTarget)
    {
        var combat = new TestCombat(2);
        if (killTarget)
        {
            TestCombat.Power<VulnerablePower>(combat.Enemy, 3, true);
            AfterAttack = simulator => simulator.State.GetCreature(combat.Enemy).LoseHp(100, ValueProp.Unblockable);
        }
        Play(combat, typeof(MoltenFist));
        Assert.Equal(["Attack"], Calls);
    }

    [Fact]
    public void HighFiveSkipsAllEffectsWithoutOsty()
    {
        Play(new TestCombat(), typeof(HighFive));
        Assert.Empty(Calls);
    }

    [Fact]
    public void MadScienceSappingUsesCustomAmountsAfterItsAttack()
    {
        var combat = new TestCombat();
        TestCombat.Power<ArtifactPower>(combat.Enemy, 1, true);
        Play(combat, typeof(MadScience));
        Assert.Equal(["Attack", "WeakPower:1", "VulnerablePower:1"], Calls);
        Assert.Contains(PowerChanges, change => change.Name == nameof(VulnerablePower) && change.Amount == 2);
    }

    public static IEnumerable<object[]> InferredCards()
    {
        foreach (var (type, amount, attack) in new[]
        {
            (typeof(Comet), 3, true), (typeof(FallingStar), 1, true), (typeof(GammaBlast), 2, true),
            (typeof(KnowThyPlace), 1, false), (typeof(Putrefy), 2, false), (typeof(Uppercut), 1, true),
            (typeof(MeteorShower), 2, true)
        })
            foreach (var upgraded in new[] { false, true })
                yield return [type, upgraded, amount + (upgraded && (type == typeof(Putrefy) || type == typeof(Uppercut)) ? 1 : 0), attack];
    }

    [Theory]
    [MemberData(nameof(InferredCards))]
    public void InferredApplicationsUseCorrectOrderAndUpgradeAmounts(Type type, bool upgraded, int amount, bool attack)
    {
        Play(new TestCombat(), type, upgraded, inferred: true);
        Assert.Equal(attack ? ["Attack", "WeakPower:1", "VulnerablePower:1"] : ["WeakPower:1", "VulnerablePower:1"], Calls);
        Assert.Contains(PowerChanges, change => change.Name == nameof(WeakPower) && change.Amount == amount);
        Assert.Contains(PowerChanges, change => change.Name == nameof(VulnerablePower) && change.Amount == amount);
    }

    [Fact]
    public void NeutralizeInfersUpgradedWeakWithoutVulnerable()
    {
        Play(new TestCombat(), typeof(Neutralize), upgraded: true, inferred: true);
        Assert.Equal(["Attack", "WeakPower:1"], Calls);
        Assert.Contains(PowerChanges, change => change.Name == nameof(WeakPower) && change.Amount == 2);
    }

    [Fact]
    public void ConditionalWeakIsNotInferredAsUnconditional()
    {
        Play(new TestCombat(), typeof(GoForTheEyes), inferred: true);
        Assert.Equal(["Attack"], Calls);
    }

    [Theory]
    [InlineData(0, 1, false, 2)]
    [InlineData(2, 1, false, 0)]
    [InlineData(1, 1, false, 2)]
    [InlineData(0, 2, false, 4)]
    [InlineData(0, 1, true, 0)]
    public void ViciousDrawsOnlyForSuccessfulApplicationsWhileActive(int artifactAmount, int enemies,
        bool removed, int expectedDraws)
    {
        var combat = new TestCombat(enemies);
        var vicious = TestCombat.Power<ViciousPower>(combat.Player.Creature, 2, true);
        if (artifactAmount > 0) TestCombat.Power<ArtifactPower>(combat.Enemy, artifactAmount, true);
        if (removed) combat.Simulator.RemovePower(vicious);
        Play(combat, enemies > 1 ? typeof(MeteorShower) : typeof(Putrefy), inferred: true);
        Assert.Equal(expectedDraws, Drawn);
        Assert.Equal(2, vicious.Amount);
    }

    /// <summary>Verifies that <see cref="Malaise"/> at zero energy X does not consume <see cref="ArtifactPower"/>.</summary>
    /// <remarks>The inferrer does not resolve this card's X-dependent debuff amount. Enable when that path is supported.</remarks>
    [Fact(Skip = "Known limitation: Malaise inference does not resolve energy X.")]
    [Trait("Category", "KnownLimitation")]
    public void MalaiseAtZeroXShouldNotConsumeArtifact()
    {
        var combat = new TestCombat();
        var artifact = TestCombat.Power<ArtifactPower>(combat.Enemy, 1, true);
        Play(combat, typeof(Malaise), inferred: true);
        Assert.Equal(1, combat.Amount(artifact));
    }

    /// <summary>Verifies that both <see cref="Haze"/> debuffs pass through <see cref="ArtifactPower"/> handling.</summary>
    /// <remarks>The inferrer omits the Poison application. Enable when both applications are supported.</remarks>
    [Fact(Skip = "Known limitation: Haze inference omits Poison.")]
    [Trait("Category", "KnownLimitation")]
    public void HazeShouldApplyBothDebuffsThroughArtifact()
    {
        var combat = new TestCombat();
        var artifact = TestCombat.Power<ArtifactPower>(combat.Enemy, 2, true);
        Play(combat, typeof(Haze), inferred: true);
        Assert.Equal(0, combat.Amount(artifact));
    }
}
