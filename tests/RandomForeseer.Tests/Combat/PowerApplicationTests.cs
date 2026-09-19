using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
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
/// All input cards and powers are arranged before BeginPrediction; repeated mirror calls reuse the arranged card.
/// Unsupported inference paths declare their expected behavior and limitation at the corresponding skipped test.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class PowerApplicationTests() : GameTestBase(observePowerCommands: true)
{
    private static CardModel ArrangePlay(TestCombat combat, Type type, bool upgraded = false)
    {
        var source = combat.ArrangeCard(type, pile: PileType.Play);
        if (upgraded)
        {
            source.UpgradeInternal();
            source.FinalizeUpgradeInternal();
        }
        if (source is MadScience madScience)
        {
            madScience.TinkerTimeType = CardType.Attack;
            madScience.TinkerTimeRider = TinkerTime.RiderEffect.Sapping;
        }
        return source;
    }

    private static void Play(TestCombat combat, CardModel source, bool inferred = false)
    {
        var card = combat.Predicted(source);
        var preview = card.MutablePreview;
        var type = source.GetType();
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
        using var combat = new TestCombat();
        var artifact = combat.ArrangePower<ArtifactPower>(combat.Enemy, 1, true);
        var sourceCard = ArrangePlay(combat, typeof(Putrefy), upgraded: true);
        combat.BeginPrediction();
        Play(combat, sourceCard, inferred: true);
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
        using var combat = new TestCombat(2);
        var sourceCard = ArrangePlay(combat, type);
        combat.BeginPrediction();
        Play(combat, sourceCard, inferred: inferred);
        Assert.Equal(expected.Split(','), Calls);
    }

    [Fact]
    public void ExposeBreaksShadowBlockAndRemovesArtifactBeforeApplyingVulnerable()
    {
        using var combat = new TestCombat();
        var artifact = combat.ArrangePower<ArtifactPower>(combat.Enemy, 2, true);
        combat.Enemy._block = 12;
        var sourceCard = ArrangePlay(combat, typeof(Expose));
        combat.BeginPrediction();
        Play(combat, sourceCard);
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
        using var combat = new TestCombat();
        var power = initial > 0 ? combat.ArrangePower<VulnerablePower>(combat.Enemy, initial, true) : null;
        if (artifact) combat.ArrangePower<ArtifactPower>(combat.Enemy, 1, true);
        var sourceCard = ArrangePlay(combat, typeof(Dominate), upgraded: upgraded);
        combat.BeginPrediction();
        Play(combat, sourceCard);
        if (strength == 0) Assert.DoesNotContain(PowerChanges, change => change.Name == nameof(StrengthPower));
        else Assert.Contains(PowerChanges, change => change.Name == nameof(StrengthPower) && change.Amount == strength);
        if (power is not null) Assert.Equal(initial, power.Amount);
    }

    [Fact]
    public void RepeatedMoltenFistDoublesShadowVulnerable()
    {
        using var combat = new TestCombat();
        var power = combat.ArrangePower<VulnerablePower>(combat.Enemy, 3, true);
        var sourceCard = ArrangePlay(combat, typeof(MoltenFist));
        combat.BeginPrediction();
        Play(combat, sourceCard);
        Play(combat, sourceCard);
        Assert.Equal(12, combat.Amount(power));
        Assert.Equal(3, power.Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MoltenFistSkipsAbsentVulnerableOrDeadTargets(bool killTarget)
    {
        using var combat = new TestCombat(2);
        if (killTarget)
        {
            combat.ArrangePower<VulnerablePower>(combat.Enemy, 3, true);
            AfterAttack = simulator => simulator.State.GetCreature(combat.Enemy).LoseHp(100, ValueProp.Unblockable);
        }
        var sourceCard = ArrangePlay(combat, typeof(MoltenFist));
        combat.BeginPrediction();
        Play(combat, sourceCard);
        Assert.Equal(["Attack"], Calls);
    }

    [Fact]
    public void HighFiveSkipsAllEffectsWithoutOsty()
    {
        using var combat = new TestCombat();
        var sourceCard = ArrangePlay(combat, typeof(HighFive));
        combat.BeginPrediction();
        Play(combat, sourceCard);
        Assert.Empty(Calls);
    }

    [Fact]
    public void MadScienceSappingUsesCustomAmountsAfterItsAttack()
    {
        using var combat = new TestCombat();
        combat.ArrangePower<ArtifactPower>(combat.Enemy, 1, true);
        var sourceCard = ArrangePlay(combat, typeof(MadScience));
        combat.BeginPrediction();
        Play(combat, sourceCard);
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
        using var combat = new TestCombat();
        var sourceCard = ArrangePlay(combat, type, upgraded);
        combat.BeginPrediction();
        Play(combat, sourceCard, inferred: true);
        Assert.Equal(attack ? ["Attack", "WeakPower:1", "VulnerablePower:1"] : ["WeakPower:1", "VulnerablePower:1"], Calls);
        Assert.Contains(PowerChanges, change => change.Name == nameof(WeakPower) && change.Amount == amount);
        Assert.Contains(PowerChanges, change => change.Name == nameof(VulnerablePower) && change.Amount == amount);
    }

    [Fact]
    public void NeutralizeInfersUpgradedWeakWithoutVulnerable()
    {
        using var combat = new TestCombat();
        var sourceCard = ArrangePlay(combat, typeof(Neutralize), upgraded: true);
        combat.BeginPrediction();
        Play(combat, sourceCard, inferred: true);
        Assert.Equal(["Attack", "WeakPower:1"], Calls);
        Assert.Contains(PowerChanges, change => change.Name == nameof(WeakPower) && change.Amount == 2);
    }

    [Fact]
    public void ConditionalWeakIsNotInferredAsUnconditional()
    {
        using var combat = new TestCombat();
        var sourceCard = ArrangePlay(combat, typeof(GoForTheEyes));
        combat.BeginPrediction();
        Play(combat, sourceCard, inferred: true);
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
        using var combat = new TestCombat(enemies);
        var vicious = combat.ArrangePower<ViciousPower>(combat.Player.Creature, 2, true);
        if (artifactAmount > 0) combat.ArrangePower<ArtifactPower>(combat.Enemy, artifactAmount, true);
        var sourceCard = ArrangePlay(combat, enemies > 1 ? typeof(MeteorShower) : typeof(Putrefy));
        combat.BeginPrediction();
        if (removed) combat.Simulator.RemovePower(vicious);
        Play(combat, sourceCard, inferred: true);
        Assert.Equal(expectedDraws, Drawn);
        Assert.Equal(2, vicious.Amount);
    }

    /// <summary>Verifies that <see cref="Malaise"/> at zero energy X does not consume <see cref="ArtifactPower"/>.</summary>
    /// <remarks>The inferrer does not resolve this card's X-dependent debuff amount. Enable when that path is supported.</remarks>
    [Fact(Skip = "Known limitation: Malaise inference does not resolve energy X.")]
    [Trait("Category", "KnownLimitation")]
    public void MalaiseAtZeroXShouldNotConsumeArtifact()
    {
        using var combat = new TestCombat();
        var artifact = combat.ArrangePower<ArtifactPower>(combat.Enemy, 1, true);
        var sourceCard = ArrangePlay(combat, typeof(Malaise));
        combat.BeginPrediction();
        Play(combat, sourceCard, inferred: true);
        Assert.Equal(1, combat.Amount(artifact));
    }

    /// <summary>Verifies that both <see cref="Haze"/> debuffs pass through <see cref="ArtifactPower"/> handling.</summary>
    /// <remarks>The inferrer omits the Poison application. Enable when both applications are supported.</remarks>
    [Fact(Skip = "Known limitation: Haze inference omits Poison.")]
    [Trait("Category", "KnownLimitation")]
    public void HazeShouldApplyBothDebuffsThroughArtifact()
    {
        using var combat = new TestCombat();
        var artifact = combat.ArrangePower<ArtifactPower>(combat.Enemy, 2, true);
        var sourceCard = ArrangePlay(combat, typeof(Haze));
        combat.BeginPrediction();
        Play(combat, sourceCard, inferred: true);
        Assert.Equal(0, combat.Amount(artifact));
    }
}
