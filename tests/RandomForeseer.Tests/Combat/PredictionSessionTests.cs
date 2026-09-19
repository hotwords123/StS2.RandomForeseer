using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies <see cref="CombatPredictionSession"/> request isolation, completion, failure propagation and disposal.
/// </summary>
/// <remarks>
/// Uses the Arrange-only <see cref="TestCombat"/> source fixture and its headless listener isolation, without replacing
/// simulator commands. These tests do not exercise UI feature gates, eager capture, output freezing or native resource
/// disposal. No fixture prediction is started; each explicit session owns a freshly assembled simulator.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class PredictionSessionTests : GameTestBase
{
    [Fact]
    public void SeparateRequestsHaveIndependentStateHistoryAndRng()
    {
        using var combat = new TestCombat();
        var source = combat.Source;
        var sourceCounter = source.RunState.Rng.Shuffle._counter;
        var first = new CombatPredictionSession(source);
        var second = new CombatPredictionSession(source);

        using (first)
        using (second)
        {
            var firstSimulator = first.Simulator;
            var secondSimulator = second.Simulator;
            firstSimulator.GainEnergy(combat.Player, 2);
            firstSimulator.History.RecordRisk(PredictionRiskReason.UnresolvedPlayerChoice);

            Assert.NotSame(firstSimulator, secondSimulator);
            Assert.Equal(2, firstSimulator.State.GetPlayerCombatState(combat.Player).Energy);
            Assert.True(firstSimulator.Snapshot().HasRisk);
            Assert.Equal(0, secondSimulator.State.GetPlayerCombatState(combat.Player).Energy);
            Assert.False(secondSimulator.Snapshot().HasRisk);
            Assert.Equal(firstSimulator.Rng.Shuffle.NextInt(), secondSimulator.Rng.Shuffle.NextInt());
        }

        Assert.Equal(0, combat.Player.PlayerCombatState!.Energy);
        Assert.Equal(sourceCounter, source.RunState.Rng.Shuffle._counter);
        Assert.Throws<ObjectDisposedException>(() => first.Simulator);
        Assert.Throws<ObjectDisposedException>(() => second.Simulator);
    }

    [Fact]
    public void EarlyNullReturnDisposesTheSessionWhenLeavingTheBlock()
    {
        using var combat = new TestCombat();
        var session = new CombatPredictionSession(combat.Source);

        Assert.Null(Predict());
        Assert.Throws<ObjectDisposedException>(() => session.Simulator);

        object? Predict()
        {
            using (session)
            {
                var simulator = session.Simulator;
                Assert.False(simulator.Snapshot().HasRisk);
                return null;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureBeforeOrAfterSimulationPropagatesAndDisposesTheSession(bool afterSimulation)
    {
        using var combat = new TestCombat();
        var session = new CombatPredictionSession(combat.Source);
        var failure = new InvalidOperationException("Prediction failed.");

        var actual = Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using (session)
            {
                var simulator = session.Simulator;
                if (afterSimulation)
                {
                    simulator.GainEnergy(combat.Player, 2);
                }

                // A failure while materializing the result uses the same using-block cleanup path.
                throw failure;
            }
        }));

        Assert.Same(failure, actual);
        Assert.Equal(0, combat.Player.PlayerCombatState!.Energy);
        Assert.Throws<ObjectDisposedException>(() => session.Simulator);
    }

    [Fact]
    public void GetterKeepsTheSameSimulatorAvailableUntilTheBlockEnds()
    {
        using var combat = new TestCombat();
        var session = new CombatPredictionSession(combat.Source);

        using (session)
        {
            var simulator = session.Simulator;
            simulator.GainEnergy(combat.Player, 2);
            Assert.Same(simulator, session.Simulator);
            Assert.Equal(2, session.Simulator.State.GetPlayerCombatState(combat.Player).Energy);
            simulator.GainEnergy(combat.Player, 1);
            Assert.Equal(3, session.Simulator.State.GetPlayerCombatState(combat.Player).Energy);
        }

        Assert.Throws<ObjectDisposedException>(() => session.Simulator);
    }

    [Fact]
    public void DisposingAnUnusedSessionIsIdempotentAndPreventsGetterAccess()
    {
        using var combat = new TestCombat();
        var session = new CombatPredictionSession(combat.Source);

        session.Dispose();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.Simulator);
    }

    [Fact]
    public void SessionCompletionPreservesModelsRetainedByLegacyResults()
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<StrikeIronclad>();
        var result = Predict();

        Assert.NotSame(sourceCard, result);
        Assert.Equal(0, result.EnergyCost.GetWithModifiers(CostModifiers.Local));
        Assert.Equal(1, sourceCard.EnergyCost.GetWithModifiers(CostModifiers.Local));

        CardModel Predict()
        {
            using var session = new CombatPredictionSession(combat.Source);
            var simulator = session.Simulator;
            var predicted = simulator.State.GetPlayerCombatState(combat.Player).FindCard(sourceCard)!;
            predicted.MutablePreview.EnergyCost.SetThisTurnOrUntilPlayed(0);
            return predicted.Preview;
        }
    }
}
