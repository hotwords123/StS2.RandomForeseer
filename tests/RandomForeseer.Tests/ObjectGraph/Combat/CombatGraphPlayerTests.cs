using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using RandomForeseer.Tests.Infrastructure;
using RandomForeseer.ObjectGraph;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph.Combat;

/// <summary>Verifies player resources, powers and pile identities through <see cref="CombatGraphFixturePolicy"/>.</summary>
/// <remarks>
/// Uses real Player/PlayerCombatState constructors with isolated profile/catalog services and shared NullRunState.
/// Events are uniformly reset in the clone; source subscriptions remain intact.
/// This fixture has no enemy, relic, potion, pet or orb. Real RunState and initialized enemy capture are
/// covered by CombatGraphRunTests; game-window capture remains outside these offline tests.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class CombatGraphPlayerTests() : GameTestBase(isolateGraphSourceServices: true)
{
    [Fact]
    public void PlayerResourcesPowersAndBackReferencesAreIndependent()
    {
        var (combat, player, power) = CreatePlayerCombat();
        var policy = CombatGraphFixturePolicy.Create();
        var before = GraphSourceSnapshot.Capture(combat, policy);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);
        var profileReads = SaveManagerReads;
        var clone = copier.Copy(combat);
        var copiedPlayer = scope.GetClone(player);
        var copiedState = copiedPlayer.PlayerCombatState!;
        var copiedPower = scope.GetClone(power);

        ObjectGraphAssert.Equivalent(combat, clone, scope, policy);
        Assert.Same(copiedPlayer, copier.Copy(player));
        Assert.Same(copiedPlayer.Creature, Assert.Single(clone.Allies));
        Assert.Same(copiedPlayer, copiedPlayer.Creature.Player);
        Assert.Same(clone, copiedPlayer.Creature.CombatState);
        Assert.Same(copiedPlayer, copiedState._player);
        Assert.Same(copiedPlayer, copiedState.OrbQueue._owner);
        Assert.Same(player.Character, copiedPlayer.Character);
        Assert.Same(combat.RunState, copiedPlayer.RunState);
        Assert.Same(copiedPower, Assert.Single(copiedPlayer.Creature.Powers));
        Assert.Same(copiedPlayer.Creature, copiedPower.Owner);
        Assert.Same(copiedPlayer.Creature, copiedPower.Applier);
        Assert.Equal(57, copiedPlayer.Creature.CurrentHp);
        Assert.Equal(11, copiedPlayer.Creature.Block);
        Assert.Equal(2, copiedState.Energy);
        Assert.Equal(7, copiedState.Stars);
        Assert.Equal(4, copiedPower.Amount);
        Assert.Equal(profileReads, SaveManagerReads);
        before.AssertUnchanged();

        // Mutate raw copied state: gameplay setters may access static history or invoke callbacks.
        copiedState._energy = 0;
        copiedState._stars = 1;
        copiedPlayer.Creature._currentHp = 23;
        copiedPlayer.Creature._block = 0;
        copiedPower._amount = 9;
        copiedPlayer._gold = 200;
        copiedPlayer.ExtraFields.DamageDealt = 30;
        copiedPlayer.PlayerRng.Rewards.NextUnsignedLong();
        before.AssertUnchanged();
    }

    [Fact]
    public void AllPilesOwnersAndDeckVersionsPreserveIdentity()
    {
        var (combat, player, _) = CreatePlayerCombat();
        ModelDb.Inject(typeof(StrikeIronclad));
        ModelDb.Inject(typeof(DefendIronclad));
        var state = player.PlayerCombatState!;
        Assert.False(CombatManager.Instance.IsInProgress);
        foreach (var pile in state.AllPiles)
        {
            AddCard(pile, ModelDb.Card<StrikeIronclad>());
            AddCard(pile, ModelDb.Card<DefendIronclad>());
        }

        var policy = CombatGraphFixturePolicy.Create();
        var before = GraphSourceSnapshot.Capture(combat, policy);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);
        var firstCard = state.Hand.Cards[0];
        var firstCopy = copier.Copy(firstCard);
        var clone = copier.Copy(combat);
        var copiedPlayer = scope.GetClone(player);
        var copiedState = copiedPlayer.PlayerCombatState!;

        ObjectGraphAssert.Equivalent(combat, clone, scope, policy);
        Assert.Same(firstCopy, copiedState.Hand.Cards[0]);
        Assert.Equal(10, clone._allCards.Count);
        Assert.Equal(10, copiedPlayer.Deck.Cards.Count);
        // CombatState.AddCard checks CardModel.CombatState, which initializes Player's run-pile cache.
        Assert.Same(copiedPlayer.Deck, Assert.Single(copiedPlayer._runPiles!));
        Assert.Equal(new[] { copiedState.Hand, copiedState.DrawPile, copiedState.DiscardPile,
            copiedState.ExhaustPile, copiedState.PlayPile }, copiedState._piles);
        foreach (var pile in state.AllPiles)
        {
            var copiedPile = scope.GetClone(pile);
            Assert.Same(copiedPile, CardPile.Get(pile.Type, copiedPlayer));
            Assert.Equal<CardModel>(pile.Cards.Select(card => scope.GetClone(card)), copiedPile.Cards,
                ReferenceEqualityComparer.Instance);
            foreach (var card in pile.Cards)
            {
                var copiedCard = scope.GetClone(card);
                Assert.Same(copiedPlayer, copiedCard.Owner);
                // AbstractModel.CompareTo compares IDs; these assertions require individual object identities.
                Assert.Contains(clone._allCards, candidate => ReferenceEquals(candidate, copiedCard));
                Assert.Same(scope.GetClone(card.DeckVersion!), copiedCard.DeckVersion);
                Assert.NotSame(copiedCard, copiedCard.DeckVersion);
                Assert.Same(copiedPlayer, copiedCard.DeckVersion!.Owner);
                Assert.Contains(copiedPlayer.Deck.Cards, candidate => ReferenceEquals(candidate, copiedCard.DeckVersion));
                Assert.DoesNotContain(clone._allCards, candidate => ReferenceEquals(candidate, copiedCard.DeckVersion));
            }
        }

        before.AssertUnchanged();
        // CardPile's mutation APIs notify the global tracker even when silent; edit copied lists directly.
        copiedState.Hand._cards.Remove(firstCopy);
        copiedState.DiscardPile._cards.Add(firstCopy);
        copiedState.DrawPile._cards.Reverse();
        copiedState.ExhaustPile._cards.Clear();
        copiedState.PlayPile._cards.Clear();
        copiedPlayer.Deck._cards.Remove(firstCopy.DeckVersion!);
        firstCopy.EnergyCost._base = 0;
        before.AssertUnchanged();

        void AddCard(CardPile pile, CardModel canonical)
        {
            var deckCard = canonical.ToMutable();
            deckCard.Owner = player;
            player.Deck.AddInternal(deckCard, silent: true);
            var card = canonical.ToMutable();
            combat.AddCard(card, player);
            card.DeckVersion = deckCard;
            pile.AddInternal(card, silent: true);
        }
    }

    private static (CombatState Combat, Player Player, StrengthPower Power) CreatePlayerCombat()
    {
        ModelDb.Inject(typeof(Ironclad));
        var player = new Player(ModelDb.Character<Ironclad>(), 1, 57, 80, 3, 99, 3, 0,
            new RelicGrabBag(), new UnlockState([], [], 2));
        var combat = new CombatState();
        combat.AddPlayer(player);
        player.ResetCombatState();
        player.PlayerCombatState!._energy = 2;
        player.PlayerCombatState._stars = 7;
        player.Creature._block = 11;
        player.PlayerRng.Rewards.NextUnsignedLong();
        var power = (StrengthPower)ModelDb.Power<StrengthPower>().ToMutable();
        power.ApplyInternal(player.Creature, 4, silent: true);
        power.Applier = player.Creature;
        return (combat, player, power);
    }

}
