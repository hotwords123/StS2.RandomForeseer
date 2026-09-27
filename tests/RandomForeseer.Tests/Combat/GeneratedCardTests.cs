using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies that <see cref="CombatPredictionSimulator.AddGeneratedCardsToCombat"/> preserves vanilla generation
/// history, snapshots successfully added cards, and records failed pile additions as absent snapshots.
/// </summary>
/// <remarks>
/// Combat-ending state is arranged explicitly through the shadow pending-loss boundary; full combat teardown,
/// generation-hook listeners and projection are outside this suite.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class GeneratedCardTests : GameTestBase
{
    [Fact]
    public void GeneratedCardsEnterShadowPilesAndHistoryWhileCombatIsInProgress()
    {
        using var combat = new TestCombat();
        combat.BeginPrediction();
        var card = CreateGeneratedCard(combat, typeof(Shiv));

        var result = Assert.Single(
            combat.Simulator.AddGeneratedCardsToCombat([card], PileType.Hand, combat.Player));

        Assert.True(result.Success);
        Assert.Same(combat.PlayerState.Hand, card.GetPile(combat.Simulator.State));
        Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGeneratedEntry>());
        var resolved = Assert.Single(
            combat.Simulator.History.OfType<CombatPredictionCardGenerationResolvedEntry>());
        Assert.NotNull(resolved.PreviewCard);
        Assert.NotSame(card.Preview, resolved.PreviewCard);
    }

    [Fact]
    public void CombatEndingPreservesGenerationHistoryWithFailedPileAdd()
    {
        using var combat = new TestCombat();
        combat.BeginPrediction();
        combat.Simulator.LoseCombat();
        Assert.True(combat.Simulator.IsEnding);
        Assert.True(combat.Simulator.IsInProgress);

        var card = CreateGeneratedCard(combat, typeof(Shiv));

        var result = Assert.Single(
            combat.Simulator.AddGeneratedCardsToCombat([card], PileType.Hand, combat.Player));

        Assert.False(result.Success);
        Assert.Null(card.GetPile(combat.Simulator.State));
        Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGeneratedEntry>());
        Assert.Null(Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGenerationResolvedEntry>())
            .PreviewCard);
    }

    [Fact]
    public void SingleGeneratedCardReportsFailureWhenCombatIsEnding()
    {
        using var combat = new TestCombat();
        combat.BeginPrediction();
        combat.Simulator.LoseCombat();
        var card = CreateGeneratedCard(combat, typeof(Shiv));

        var result = combat.Simulator.AddGeneratedCardToCombat(card, PileType.Hand, combat.Player);

        Assert.False(result.Success);
        Assert.Same(card, result.CardAdded);
        Assert.Null(card.GetPile(combat.Simulator.State));
        Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGeneratedEntry>());
        Assert.Null(Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGenerationResolvedEntry>())
            .PreviewCard);
    }

    [Fact]
    public void SingleGeneratedCardEntersShadowPilesWhileCombatIsInProgress()
    {
        using var combat = new TestCombat();
        combat.BeginPrediction();
        var card = CreateGeneratedCard(combat, typeof(Shiv));

        var result = combat.Simulator.AddGeneratedCardToCombat(card, PileType.Hand, combat.Player);

        Assert.True(result.Success);
        Assert.Same(card, result.CardAdded);
        Assert.Same(combat.PlayerState.Hand, card.GetPile(combat.Simulator.State));
    }

    // Generated cards must not already live in a combat pile, so they are created outside the fixture's pile helpers.
    private static PredictedCard CreateGeneratedCard(TestCombat combat, Type cardType)
    {
        ModelDb.Inject(cardType);
        return PredictedCard.Create(
            ModelDb.GetById<CardModel>(ModelDb.GetId(cardType)),
            combat.Player);
    }
}
