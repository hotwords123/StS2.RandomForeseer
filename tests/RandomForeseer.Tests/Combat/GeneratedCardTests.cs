using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Verifies that <see cref="CombatPredictionSimulator.AddGeneratedCardsToCombat"/> and its single-card wrapper
/// honor the same combat boundary as the pile-add path they delegate to, so no generated-card history is recorded
/// for a card that is never inserted into a shadow pile.
/// </summary>
/// <remarks>
/// Combat-ending state is arranged explicitly through the shadow pending-loss boundary; full combat teardown,
/// generation hooks and projection are outside this suite.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class GeneratedCardTests : GameTestBase
{
    [Fact]
    public void GeneratedCardsEnterShadowPilesAndHistoryWhileCombatIsInProgress()
    {
        var combat = new TestCombat();
        var card = CreateGeneratedCard(combat, typeof(Shiv));

        var result = Assert.Single(
            combat.Simulator.AddGeneratedCardsToCombat([card], PileType.Hand, combat.Player));

        Assert.True(result.Success);
        Assert.Same(combat.PlayerState.Hand, card.GetPile(combat.Simulator.State));
        Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGeneratedEntry>());
        Assert.Single(combat.Simulator.History.OfType<CombatPredictionCardGenerationResolvedEntry>());
    }

    [Fact]
    public void CombatEndingSkipsGeneratedCardsInsteadOfRecordingHistory()
    {
        var combat = new TestCombat();
        combat.Simulator.LoseCombat();
        Assert.True(combat.Simulator.IsEnding);
        Assert.True(combat.Simulator.IsInProgress);

        var card = CreateGeneratedCard(combat, typeof(Shiv));

        Assert.Empty(combat.Simulator.AddGeneratedCardsToCombat([card], PileType.Hand, combat.Player));
        Assert.Null(card.GetPile(combat.Simulator.State));
        Assert.Empty(combat.Simulator.History.OfType<CombatPredictionCardGeneratedEntry>());
        Assert.Empty(combat.Simulator.History.OfType<CombatPredictionCardGenerationResolvedEntry>());
    }

    [Fact]
    public void SingleGeneratedCardReportsFailureWhenCombatIsEnding()
    {
        var combat = new TestCombat();
        combat.Simulator.LoseCombat();
        var card = CreateGeneratedCard(combat, typeof(Shiv));

        var result = combat.Simulator.AddGeneratedCardToCombat(card, PileType.Hand, combat.Player);

        Assert.False(result.Success);
        Assert.Same(card, result.CardAdded);
        Assert.Null(card.GetPile(combat.Simulator.State));
        Assert.Empty(combat.Simulator.History.OfType<CombatPredictionCardGeneratedEntry>());
    }

    [Fact]
    public void SingleGeneratedCardEntersShadowPilesWhileCombatIsInProgress()
    {
        var combat = new TestCombat();
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
