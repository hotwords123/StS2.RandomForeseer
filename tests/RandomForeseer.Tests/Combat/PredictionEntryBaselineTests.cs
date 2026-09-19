using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Combat;

/// <summary>
/// Records representative simulation paths used by the card, potion, end-turn and draw-pile entry points through
/// <see cref="CombatPredictionSimulator.ManualPlay"/>, <see cref="CombatPredictionSimulator.ManualUse"/>,
/// <see cref="CombatPredictionSimulator.SimulateEndPlayerTurn"/> and <see cref="CombatPredictionSimulator.Shuffle"/>.
/// </summary>
/// <remarks>
/// Uses the existing headless fixture, including its simplified listener enumeration and source-pile arrangement before explicit prediction startup.
/// Does not replace attack/draw commands, exercise Godot UI or feature gates, prove native listener order, or validate
/// the complete live object graph. Potion removal/hooks and final hand flush remain outside these baseline assertions.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class PredictionEntryBaselineTests : GameTestBase
{
    [Fact]
    public void ManualCardPlayResolvesHitsResourcesPilesAndHistoryWithoutChangingSourceValues()
    {
        using var combat = new TestCombat();
        var sourceCard = combat.ArrangeCard<SwordBoomerang>();
        combat.BeginPrediction();
        var card = combat.Predicted(sourceCard);
        combat.PlayerState.GainEnergy(3);
        var sourceRng = combat.Source.RunState.Rng.CombatTargets;
        var sourceCounter = sourceRng._counter;

        combat.Simulator.ManualPlay(card, null, out var frame);

        Assert.Equal(91, combat.Simulator.State.GetCreature(combat.Enemy).CurrentHp);
        Assert.Equal(100, combat.Enemy.CurrentHp);
        Assert.Equal(2, combat.PlayerState.Energy);
        Assert.Equal(0, combat.Player.PlayerCombatState!.Energy);
        Assert.Same(card, Assert.Single(combat.PlayerState.DiscardPile.Cards));
        Assert.Empty(combat.PlayerState.Hand.Cards);
        Assert.Empty(combat.PlayerState.PlayPile.Cards);
        Assert.Equal(3, combat.Simulator.History.Count<CombatPredictionDamageReceivedEntry>());
        Assert.Equal(1, combat.Simulator.History.Count<CombatPredictionCardPlayStartedEntry>());
        Assert.Equal(1, combat.Simulator.History.Count<CombatPredictionCardPlayFinishedEntry>());
        Assert.Same(card.Original, frame.Source);
        Assert.Null(card.Original.CurrentTarget);
        Assert.Equal(sourceCounter, sourceRng._counter);
        Assert.Empty(CombatManager.Instance.History.Entries);
        Assert.False(combat.Simulator.Snapshot().HasRisk);
    }

    [Fact]
    public void ManualPotionUseDrawsInOrderAndResolvesHistoryWithoutChangingLivePiles()
    {
        using var combat = new TestCombat();
        var sourceCards = Enumerable.Range(0, 4).Select(_ => combat.ArrangeCard<StrikeIronclad>(pile: PileType.Draw)).ToArray();
        ModelDb.Inject(typeof(SwiftPotion));
        var potion = (SwiftPotion)ModelDb.Potion<SwiftPotion>().ToMutable();
        potion.Owner = combat.Player;

        combat.BeginPrediction();
        var cards = sourceCards.Select(combat.Predicted).ToArray();
        Assert.True(combat.Simulator.ManualUse(potion, null, out var frame));

        Assert.Equal(cards.Take(3), combat.PlayerState.Hand.Cards);
        Assert.Same(cards[3], Assert.Single(combat.PlayerState.DrawPile.Cards));
        Assert.Equal(3, combat.Simulator.History.Count<CombatPredictionCardDrawnEntry>());
        Assert.Equal(3, combat.Simulator.History.Count<CombatPredictionCardDrawResolvedEntry>());
        Assert.Same(potion, frame.Source);
        Assert.Empty(combat.Player.PlayerCombatState!.Hand.Cards);
        Assert.Equal(sourceCards, combat.Player.PlayerCombatState.DrawPile.Cards);
        Assert.Empty(CombatManager.Instance.History.Entries);
        Assert.False(combat.Simulator.Snapshot().HasRisk);
    }

    [Fact]
    public void EndTurnResolvesHandDamageForEachPlayerWithoutChangingLiveHp()
    {
        using var combat = new TestCombat();
        var sourceFirst = combat.ArrangeCard<Burn>();
        var sourceSecond = combat.ArrangeCard<Burn>(combat.OtherPlayer);
        combat.BeginPrediction();
        var first = combat.Predicted(sourceFirst);
        var second = combat.Predicted(sourceSecond);

        combat.Simulator.SimulateEndPlayerTurn();

        Assert.Equal(98, combat.Simulator.State.GetCreature(combat.Player.Creature).CurrentHp);
        Assert.Equal(98, combat.Simulator.State.GetCreature(combat.OtherPlayer.Creature).CurrentHp);
        Assert.Equal(100, combat.Player.Creature.CurrentHp);
        Assert.Equal(100, combat.OtherPlayer.Creature.CurrentHp);
        Assert.Same(first, Assert.Single(combat.PlayerState.DiscardPile.Cards));
        Assert.Same(second, Assert.Single(combat.Simulator.State.GetPlayerCombatState(combat.OtherPlayer).DiscardPile.Cards));
        Assert.Equal(2, combat.Simulator.History.Count<CombatPredictionDamageReceivedEntry>());
        Assert.Empty(CombatManager.Instance.History.Entries);
        Assert.False(combat.Simulator.Snapshot().HasRisk);
    }

    [Fact]
    public void DrawPilePreviewShufflesDiscardDeterministicallyWithoutAdvancingSourceRng()
    {
        using var first = new TestCombat();
        using var second = new TestCombat();
        var firstSources = Arrange(first);
        var secondSources = Arrange(second);
        first.BeginPrediction();
        second.BeginPrediction();
        var firstCards = firstSources.Select(first.Predicted).ToArray();
        var secondCards = secondSources.Select(second.Predicted).ToArray();
        var sourceRng = first.Simulator.State.CombatState.RunState.Rng.Shuffle;
        var sourceCounter = sourceRng._counter;
        var beforeSample = sourceRng.Clone().NextInt();

        first.PlayerState.DrawPile.Clear();
        first.Simulator.Shuffle(first.Player);
        second.PlayerState.DrawPile.Clear();
        second.Simulator.Shuffle(second.Player);

        var order = first.PlayerState.DrawPile.Cards.Select(card => Array.IndexOf(firstCards, card)).ToArray();
        Assert.Equal(Enumerable.Range(0, 4), order.Order());
        Assert.Equal(order, second.PlayerState.DrawPile.Cards.Select(card => Array.IndexOf(secondCards, card)));
        Assert.Empty(first.PlayerState.DiscardPile.Cards);
        Assert.IsType<Burn>(Assert.Single(first.Player.PlayerCombatState!.DrawPile.Cards));
        Assert.Equal(firstSources, first.Player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(sourceCounter, sourceRng._counter);
        Assert.Equal(beforeSample, sourceRng.Clone().NextInt());
        Assert.True(first.Simulator.Rng.Shuffle._counter > sourceCounter);
        Assert.False(first.Simulator.Snapshot().HasRisk);

        static CardModel[] Arrange(TestCombat combat)
        {
            combat.ArrangeCard<Burn>(pile: PileType.Draw);
            return Enumerable.Range(0, 4).Select(_ => combat.ArrangeCard<StrikeIronclad>(pile: PileType.Discard)).ToArray();
        }
    }
}
