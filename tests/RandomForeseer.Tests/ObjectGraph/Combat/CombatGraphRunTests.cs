using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Dread;
using MegaCrit.Sts2.Core.Models.Dread.Mocks;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using RandomForeseer.Tests.Infrastructure;
using RandomForeseer.InCombat.ObjectGraph;
using RandomForeseer.ObjectGraph;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph.Combat;

/// <summary>Verifies real run/player/combat, RNG, deck and relic identities copied by <see cref="PredictionGraphCopier"/>.</summary>
/// <remarks>
/// Uses RunState.CreateForTest with real constructors and isolated profile/catalog services. The room has not
/// entered gameplay, the act has not generated rooms, and the map is the constructor's placeholder. Events
/// are cleared; initialized AI callbacks, conditional owner reads and random branches are copied without
/// performing moves. Game-window capture and gameplay side effects remain outside this coverage.
/// RunModel lifecycle initialization follows RunManager; Dread coverage captures a MockDominion without applying its effects.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class CombatGraphRunTests() : GameTestBase(isolateGraphSourceServices: true)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunCombatPlayerAndRelicShareOneIsolatedGraph(bool relicFirst)
    {
        var (run, combat, player, relic) = CreateRunCombat();
        var policy = CombatGraphPolicy.Instance;
        var before = GraphSourceSnapshot.Capture(combat, policy);
        var expectedRunRng = new Rng(run.Rng.CombatTargets.ToSerializable());
        var expectedPlayerRng = new Rng(player.PlayerRng.Rewards.ToSerializable());
        var profileReads = SaveManagerReads;
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);
        if (relicFirst)
        {
            copier.Copy(relic);
        }

        var clone = copier.Copy(combat);
        var copiedRun = scope.GetClone(run);
        var copiedPlayer = scope.GetClone(player);
        var copiedRelic = scope.GetClone(relic);
        var boundaries = ObjectGraphAssert.Equivalent(combat, clone, scope, policy);
        Assert.Same(copiedRun, copier.Copy(run));
        Assert.Same(copiedRun, clone.RunState);
        Assert.Same(copiedRun, copiedPlayer.RunState);
        Assert.Same(copiedPlayer, Assert.Single(copiedRun.Players));
        Assert.Same(copiedPlayer.Creature, Assert.Single(clone.Allies));
        Assert.Same(clone, copiedPlayer.Creature.CombatState);
        Assert.Same(clone, Assert.IsType<CombatRoom>(copiedRun.CurrentRoom).CombatState);
        var copiedRunModel = Assert.IsType<MultiplayerScalingModel>(Assert.Single(copiedRun.RunModels));
        Assert.Same(copiedRunModel, Assert.Single(clone.RunModels));
        Assert.Same(copiedRun, copiedRunModel._runState);
        Assert.Same(clone, copiedRunModel._combatState);
        Assert.Same(copiedPlayer, copiedRelic.Owner);
        Assert.Same(copiedRelic, Assert.Single(copiedPlayer.Relics));
        Assert.Same(relic.CanonicalInstance, copiedRelic.CanonicalInstance);
        Assert.Same(copiedRelic, copiedRelic.DynamicVars.Heal._owner);
        Assert.DoesNotContain(boundaries, boundary => boundary.RuleId == "fixture.null-run-state");
        Assert.False(scope.TryGetClone(run.UnlockState._encountersSeen.First(), out _));
        var eventId = run._visitedEventIds.Single();
        Assert.True(copiedRun._visitedEventIds.TryGetValue(new ModelId(eventId.Category, eventId.Entry), out var copiedId));
        Assert.Same(eventId, copiedId);
        Assert.NotSame(run.Map, copiedRun.Map);
        Assert.Same(copiedPlayer.PlayerRng.Rewards, copiedPlayer.PlayerOdds.CardRarity._rng);
        Assert.Same(copiedRun.Rng.UnknownMapPoint, copiedRun.Odds.UnknownMapPoint._rng);
        Assert.Equal(profileReads, SaveManagerReads);

        foreach (var card in combat._allCards)
        {
            var copiedCard = scope.GetClone(card);
            Assert.Same(copiedPlayer, copiedCard.Owner);
            Assert.Same(scope.GetClone(card.DeckVersion!), copiedCard.DeckVersion);
            Assert.Contains(copiedRun._allCards, candidate => ReferenceEquals(candidate, copiedCard.DeckVersion));
            Assert.DoesNotContain(copiedRun._allCards, candidate => ReferenceEquals(candidate, copiedCard));
            Assert.DoesNotContain(clone._allCards, candidate => ReferenceEquals(candidate, copiedCard.DeckVersion));
        }

        before.AssertUnchanged();
        for (var index = 0; index < 16; index++)
        {
            Assert.Equal(expectedRunRng.NextUnsignedLong(), copiedRun.Rng.CombatTargets.NextUnsignedLong());
            Assert.Equal(expectedPlayerRng.NextUnsignedLong(), copiedPlayer.PlayerRng.Rewards.NextUnsignedLong());
        }

        copiedPlayer.PlayerCombatState!.Hand._cards.Clear();
        copiedPlayer.Deck._cards.RemoveAt(0);
        copiedRelic.DynamicVars.Heal.PreviewValue = 20;
        copiedRun.Odds.UnknownMapPoint.MonsterOdds = 0.7f;
        copiedRun._visitedEventIds.Clear();
        copiedRun.SharedRelicGrabBag._deques[RelicRarity.Common].Clear();
        copiedRun.Map.BossMapPoint.coord = new MapCoord(5, 6);
        before.AssertUnchanged();
    }

    [Fact]
    public void DominionAndAllRunModelsKeepCopiedRunAndCombatContexts()
    {
        var (run, combat, _, _) = CreateRunCombat(GameMode.Dread);
        var policy = CombatGraphPolicy.Instance;
        var before = GraphSourceSnapshot.Capture(combat, policy);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(combat);
        var copiedRun = scope.GetClone(run);
        var copiedDominion = scope.GetClone(run.Dominion!);

        ObjectGraphAssert.Equivalent(combat, clone, scope, policy);
        Assert.Same(copiedDominion, clone.Dominion);
        Assert.Same(copiedDominion, copiedRun.Dominion);
        Assert.Same(copiedRun, copiedDominion.RunState);
        Assert.Contains(copiedRun.RunModels, model => model is DreadRunModel);
        foreach (var runModel in run.RunModels)
        {
            var copiedModel = scope.GetClone(runModel);
            Assert.Contains(clone.RunModels, model => model == copiedModel);
            Assert.Same(copiedRun, copiedModel._runState);
            Assert.Same(clone, copiedModel._combatState);
        }

        clone.SetDominion(null);
        copiedRun.SetDominion(null);
        before.AssertUnchanged();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitializedEnemyCopiesAiCallbacksAndClearsEvents(bool monsterFirst)
    {
        var (_, combat, player, _) = CreateRunCombat();
        ModelDb.Inject(typeof(BigDummy));
        var monster = ModelDb.Monster<BigDummy>().ToMutable();
        var creature = combat.CreateCreature(monster, CombatSide.Enemy, "graph-b8");
        combat.AddCreature(creature);
        monster.SetUpForCombat();
        monster.RollMove(combat.Allies);
        var machine = monster.MoveStateMachine!;
        var move = monster.NextMove;
        Assert.Same(move, machine.States["NOTHING"]);
        Assert.Same(move, move.FollowUpState);
        Assert.Same(monster, move._onPerform.Target);

        // Install the real tracker subscriptions without entering CombatManager's live lifecycle.
        var tracker = CombatManager.Instance.StateTracker;
        tracker.Subscribe(creature);
        tracker.Subscribe(player.Creature);
        foreach (var card in combat._allCards)
        {
            tracker.Subscribe(card);
        }

        var policy = CombatGraphPolicy.Instance;
        var before = GraphSourceSnapshot.Capture(combat, policy);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);
        if (monsterFirst)
        {
            copier.Copy(monster);
        }

        var clone = copier.Copy(combat);
        var boundaries = ObjectGraphAssert.Equivalent(combat, clone, scope, policy);
        var copiedCreature = Assert.Single(clone.Enemies);
        var copiedMonster = copiedCreature.Monster!;
        Assert.NotSame(monster, copiedMonster);
        Assert.Same(scope.GetClone(monster), copiedMonster);
        Assert.Same(clone, copiedCreature.CombatState);
        Assert.Same(copiedCreature, copiedMonster.Creature);
        var copiedMachine = copiedMonster.MoveStateMachine!;
        var copiedMove = copiedMonster.NextMove;
        Assert.NotSame(machine, copiedMachine);
        Assert.NotSame(move, copiedMove);
        Assert.Same(copiedMachine, copier.Copy(machine));
        Assert.Same(copiedMove, copier.Copy(move));
        Assert.Same(copiedMove, copiedMachine.States["NOTHING"]);
        Assert.Same(copiedMove, copiedMove.FollowUpState);
        Assert.Same(copiedMove, Assert.Single(copiedMachine.StateLog));
        Assert.Same(copiedMonster, copiedMove._onPerform.Target);
        Assert.Contains(boundaries, boundary => boundary.Path.EndsWith(".Died") && boundary.RuleId == "field.event");
        Assert.NotSame(monster.Rng, copiedMonster.Rng);
        Assert.Same(scope.GetClone(monster.RunRng), copiedMonster.RunRng);
        copiedCreature._currentHp -= 10;
        copiedMonster.Rng.NextUnsignedLong();
        copiedMachine.StateLog.Clear();
        copiedMove.MustPerformOnceBeforeTransitioning = true;
        before.AssertUnchanged();
    }

    [Theory]
    [InlineData(typeof(FrogKnight), false)]
    [InlineData(typeof(FrogKnight), true)]
    [InlineData(typeof(Mawler), false)]
    [InlineData(typeof(Mawler), true)]
    public void InitializedBranchesUseCopiedOwnerAndPreserveRngSequence(Type monsterType, bool machineFirst)
    {
        var (_, combat, _, _) = CreateRunCombat();
        ModelDb.Inject(monsterType);
        var monster = ((MonsterModel)ModelDb.Get(monsterType)).ToMutable();
        var creature = combat.CreateCreature(monster, CombatSide.Enemy, "graph-ai-branches");
        combat.AddCreature(creature);
        monster.SetUpForCombat();
        monster.RollMove(combat.Allies);
        var machine = monster.MoveStateMachine!;
        var policy = CombatGraphPolicy.Instance;
        var before = GraphSourceSnapshot.Capture(combat, policy);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);
        if (machineFirst)
        {
            copier.Copy(machine);
        }

        var clone = copier.Copy(combat);
        ObjectGraphAssert.Equivalent(combat, clone, scope, policy);
        var copiedMonster = scope.GetClone(monster);
        var copiedMachine = copiedMonster.MoveStateMachine!;
        Assert.NotSame(machine, copiedMachine);
        Assert.Same(copiedMachine, copier.Copy(machine));
        Assert.Same(scope.GetClone(monster.NextMove), copiedMonster.NextMove);
        foreach (var move in copiedMachine.States.Values.OfType<MoveState>())
        {
            Assert.Same(copiedMonster, move._onPerform.Target);
        }

        if (monster is FrogKnight)
        {
            var branch = Assert.IsType<ConditionalBranchState>(copiedMachine.States["HALF_HEALTH"]);
            scope.GetClone(creature)._currentHp = 1;
            Assert.Equal("BEETLE_CHARGE", branch.GetNextState(copiedMonster.Creature, copiedMonster.Rng));
            Assert.Equal("TONGUE_LASH", machine.States["HALF_HEALTH"].GetNextState(creature, monster.Rng));
        }
        else
        {
            var expectedRng = new Rng(monster.RunRng.MonsterAi.ToSerializable());
            var branch = Assert.IsType<RandomBranchState>(copiedMachine.States["RAND"]);
            for (var index = 0; index < 24; index++)
            {
                Assert.Equal(machine.States["RAND"].GetNextState(creature, expectedRng),
                    branch.GetNextState(copiedMonster.Creature, copiedMonster.RunRng.MonsterAi));
            }
            Assert.Equal(expectedRng.NextUnsignedLong(), copiedMonster.RunRng.MonsterAi.NextUnsignedLong());
        }

        copiedMachine.StateLog.Clear();
        before.AssertUnchanged();
    }

    private static (RunState Run, CombatState Combat, Player Player, BurningBlood Relic) CreateRunCombat(
        GameMode gameMode = GameMode.Standard)
    {
        foreach (var type in new[] { typeof(Ironclad), typeof(StrikeIronclad), typeof(DefendIronclad),
            typeof(Overgrowth), typeof(MultiplayerScalingModel), typeof(BurningBlood), typeof(Anchor),
            typeof(BattlewornDummyEventV1Encounter) })
        {
            ModelDb.Inject(type);
        }

        var encounterId = ModelDb.Encounter<BattlewornDummyEventV1Encounter>().Id;
        var player = new Player(ModelDb.Character<Ironclad>(), 1, 57, 80, 3, 99, 3, 0,
            new RelicGrabBag(), new UnlockState(["fixture-epoch"], [encounterId], 2));
        // Supply a small explicit bag instead of generating the game's complete relic catalog.
        player.RelicGrabBag._deques.Add(RelicRarity.Common, [ModelDb.Relic<Anchor>()]);
        if (gameMode == GameMode.Dread)
        {
            ModelDb.Inject(typeof(DreadRunModel));
            ModelDb.Inject(typeof(MockDominion));
        }

        var run = RunState.CreateForTest(
            [player],
            [ModelDb.Act<Overgrowth>()],
            gameMode: gameMode,
            seed: "graph-b7");
        // RunState now creates RunModels; RunManager owns their run-lifecycle initialization.
        foreach (var runModel in run.RunModels)
        {
            runModel.OnRunCreated(run);
        }

        if (gameMode == GameMode.Dread)
        {
            run.SetDominion(ModelDb.Dominion<MockDominion>());
        }

        run._visitedEventIds.Add(new ModelId("EVENT", "FIXTURE_EVENT"));
        run.SharedRelicGrabBag._deques.Add(RelicRarity.Common, [ModelDb.Relic<Anchor>()]);
        var room = new CombatRoom(ModelDb.Encounter<BattlewornDummyEventV1Encounter>().ToMutable(), run);
        run._currentRooms.Add(room);
        var combat = room.CombatState;
        combat.AddPlayer(player);
        player.ResetCombatState();
        foreach (var runModel in run.RunModels)
        {
            runModel.OnCombatEntered(combat);
        }
        player.PlayerCombatState!._energy = 2;
        player.PlayerCombatState._stars = 7;
        player.Creature._block = 11;
        var power = ModelDb.Power<StrengthPower>().ToMutable();
        power.ApplyInternal(player.Creature, 4, silent: true);
        var relic = (BurningBlood)ModelDb.Relic<BurningBlood>().ToMutable();
        player.AddRelicInternal(relic, silent: true);
        relic.DynamicVars.Heal.PreviewValue = 8;
        Assert.False(CombatManager.Instance.IsInProgress);
        foreach (var pile in player.PlayerCombatState.AllPiles)
        {
            var canonical = pile.Type is PileType.Discard or PileType.Exhaust
                ? (CardModel)ModelDb.Card<DefendIronclad>() : ModelDb.Card<StrikeIronclad>();
            var deckCard = canonical.ToMutable();
            run.AddCard(deckCard, player);
            player.Deck.AddInternal(deckCard, silent: true);
            var card = canonical.ToMutable();
            combat.AddCard(card, player);
            card.DeckVersion = deckCard;
            pile.AddInternal(card, silent: true);
        }

        run.Rng.CombatTargets.NextUnsignedLong();
        player.PlayerRng.Rewards.NextUnsignedLong();
        return (run, combat, player, relic);
    }
}
