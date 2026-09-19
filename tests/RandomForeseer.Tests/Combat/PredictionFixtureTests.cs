using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies the <see cref="TestCombat"/> Arrange/BeginPrediction protocol and ownership of
/// <see cref="CombatPredictionSession"/>, including source-pile capture and prediction-only generation.
/// </summary>
/// <remarks>
/// Uses the standard headless listener isolation. Guards cover fixture helpers and proxy setters, not arbitrary
/// writes through raw game references; these tests do not claim eager capture or complete graph isolation.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class PredictionFixtureTests : GameTestBase
{
    [Fact]
    public void PredictionRequiresExplicitBeginAndRejectsFurtherSourceArrangement()
    {
        using var combat = new TestCombat();
        var source = combat.ArrangeCard<StrikeIronclad>();
        Assert.Throws<InvalidOperationException>(() => combat.Simulator);
        Assert.Throws<InvalidOperationException>(() => combat.PlayerState);
        Assert.Throws<InvalidOperationException>(() => combat.Predicted(source));
        combat.BeginPrediction();

        Assert.Throws<InvalidOperationException>(combat.BeginPrediction);
        Assert.Throws<InvalidOperationException>(() => combat.ArrangeCard<Shiv>());
        Assert.Throws<InvalidOperationException>(() => combat.ArrangePower<ArtifactPower>(combat.Enemy, 1, true));
        Assert.Throws<InvalidOperationException>(() => combat.ArrangeRelic<MummifiedHand>(combat.Player));
        Assert.Throws<InvalidOperationException>(() => combat.ArrangeHistory(source));
        Assert.Throws<InvalidOperationException>(() => combat.Proxy.Listeners = []);
        Assert.Throws<InvalidOperationException>(() => combat.Proxy.Allies = []);
        Assert.Throws<InvalidOperationException>(() => combat.Proxy.Enemies = []);
        Assert.Same(source, Assert.Single(combat.Player.PlayerCombatState!.Hand.Cards));
        Assert.Empty(combat.Enemy.Powers);
        Assert.Empty(CombatManager.Instance.History.Entries);
    }

    [Fact]
    public void BeginUsesArrangedSourceValuesAndPreservesPileOrderAndIdentity()
    {
        using var combat = new TestCombat();
        var hand = combat.ArrangeCard<StrikeIronclad>();
        hand.EnergyCost.SetThisTurn(2);
        var draw = combat.ArrangeCard<Shiv>(pile: PileType.Draw);
        var secondDraw = combat.ArrangeCard<DefendIronclad>(pile: PileType.Draw);
        var other = combat.ArrangeCard<Burn>(combat.OtherPlayer, PileType.Discard);
        var artifact = combat.ArrangePower<ArtifactPower>(combat.Enemy, 2, true);
        combat.Player.PlayerCombatState!._energy = 3;
        combat.Player.PlayerCombatState._stars = 4;
        combat.Enemy._block = 12;
        combat.ArrangeHistory(hand);
        combat.BeginPrediction();

        Assert.Equal(3, combat.PlayerState.Energy);
        Assert.Equal(4, combat.PlayerState.Stars);
        Assert.Equal(12, combat.Simulator.State.GetCreature(combat.Enemy).Block);
        Assert.Equal(2, combat.Amount(artifact));
        Assert.Equal(2, combat.Predicted(hand).GetEnergyCostWithModifiers(combat.Simulator));
        Assert.Same(combat.Predicted(hand), Assert.Single(combat.PlayerState.Hand.Cards));
        Assert.Equal([draw, secondDraw], combat.PlayerState.DrawPile.Cards.Select(card => card.Original));
        Assert.Same(combat.Predicted(other), Assert.Single(
            combat.Simulator.State.GetPlayerCombatState(combat.OtherPlayer).DiscardPile.Cards));
        Assert.Same(hand, Assert.Single(CombatManager.Instance.History.CardPlaysStarted).CardPlay.Card);

        combat.Simulator.Draw(combat.Player, 1);
        Assert.Equal([hand, draw], combat.PlayerState.Hand.Cards.Select(card => card.Original));
        Assert.Equal([draw, secondDraw], combat.Player.PlayerCombatState.DrawPile.Cards);
        Assert.Same(hand, Assert.Single(combat.Player.PlayerCombatState.Hand.Cards));
    }

    [Fact]
    public void CardsCreatedDuringPredictionUseGenerationCommandsWithoutChangingSourcePiles()
    {
        using var combat = new TestCombat();
        ModelDb.Inject(typeof(Shiv));
        combat.BeginPrediction();
        var generated = Assert.Single(combat.Simulator.CreateAndAddGeneratedCardsToCombat<Shiv>(
            combat.Player, PileType.Hand, 1, combat.Player));

        Assert.True(generated.Success);
        Assert.Same(generated.CardAdded, Assert.Single(combat.PlayerState.Hand.Cards));
        Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGeneratedEntry>());
        Assert.Empty(combat.Player.PlayerCombatState!.Hand.Cards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposalClosesBothUnusedAndStartedFixtures(bool begin)
    {
        var combat = new TestCombat();
        if (begin) combat.BeginPrediction();
        combat.Dispose();
        combat.Dispose();

        Assert.Throws<ObjectDisposedException>(combat.BeginPrediction);
        Assert.Throws<ObjectDisposedException>(() => combat.Simulator);
        Assert.Throws<ObjectDisposedException>(() => combat.ArrangeCard<Shiv>());
    }

    [Fact]
    public void FailureLeavesUsingScopeAndClosesTheFixture()
    {
        var combat = new TestCombat();
        var failure = new InvalidOperationException("Fixture simulation failed.");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using (combat)
            {
                combat.BeginPrediction();
                combat.Simulator.GainEnergy(combat.Player, 2);
                throw failure;
            }
        })));
        Assert.Throws<ObjectDisposedException>(() => combat.Simulator);
        Assert.Equal(0, combat.Player.PlayerCombatState!.Energy);
    }
}
