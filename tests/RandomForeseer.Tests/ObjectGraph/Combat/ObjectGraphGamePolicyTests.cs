using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using RandomForeseer.Tests.Infrastructure;
using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Metadata;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph.Combat;

/// <summary>Checks card runtime state, calculated callbacks and event boundaries in <see cref="GraphCopyPolicy"/>.</summary>
/// <remarks>
/// This is a policy-framework test, not full combat capture: the constructor's NullRunState is shared as a test
/// boundary; cards have no Player owner and the monster has no initialized move state machine.
/// The normal scenario explicitly registers the combat model rule alongside common rules and the fixture NullRunState boundary.
/// Event subscribers are uniformly omitted under the current visual-only assumption; callbacks are never executed.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class ObjectGraphGamePolicyTests : GameTestBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CardRuntimeStateAndCanonicalCachesRemainIsolated(bool canonicalFirst)
    {
        var (combat, _, card) = ArrangeGraph();
        var canonical = card.CanonicalInstance;
        var originalVariables = canonical._dynamicVars;
        var originalCost = canonical._energyCost;
        using var scope = new PredictionObjectScope();
        var policy = CombatGraphFixturePolicy.Create();
        var before = GraphSourceSnapshot.Capture(combat, policy);
        var copier = new PredictionGraphCopier(scope, policy);
        if (canonicalFirst)
        {
            Assert.Same(canonical, copier.Copy(canonical));
        }
        var combatCopy = copier.Copy(combat);
        var cardCopy = copier.Copy(card);

        ObjectGraphAssert.Equivalent(combat, combatCopy, scope, policy);
        before.AssertUnchanged();
        Assert.NotSame(card.EnergyCost._localModifiers[0], cardCopy.EnergyCost._localModifiers[0]);
        Assert.Equal(card.EnergyCost._localModifiers[0].Amount, cardCopy.EnergyCost._localModifiers[0].Amount);
        Assert.True(cardCopy._keywords!.TryGetValue(CardKeyword.Retain, out _));
        Assert.True(cardCopy._tags!.TryGetValue(CardTag.Strike, out _));
        Assert.Same(canonical, copier.Copy(canonical));
        Assert.Same(originalVariables, canonical._dynamicVars);
        Assert.Same(originalCost, canonical._energyCost);
        Assert.False(scope.TryGetClone(canonical, out _));
        Assert.Same(canonical, cardCopy.CanonicalInstance);
        Assert.Same(cardCopy, cardCopy.EnergyCost._card);
        Assert.Same(cardCopy, cardCopy.DynamicVars.Damage._owner);
        Assert.True(cardCopy.IsMutable);
        Assert.Equal(3, cardCopy.CurrentPlayIndex);
        Assert.Equal(0, cardCopy.EnergyCost._base);
        Assert.Equal(27, cardCopy.DynamicVars.Damage.PreviewValue);

        cardCopy.EnergyCost._base = 8;
        cardCopy._keywords!.Remove(CardKeyword.Retain);
        cardCopy._tags!.Add(CardTag.Defend);
        cardCopy.DynamicVars.Damage.PreviewValue = 99;
        before.AssertUnchanged();
        card._currentPlayIndex = 6;
        Assert.Equal(3, cardCopy.CurrentPlayIndex);
    }

    [Fact]
    public void CalculatedCardVariablesKeepTheirCallbackAndRemapTheirOwner()
    {
        ModelDb.Inject(typeof(BodySlam));
        var source = ModelDb.Card<BodySlam>().ToMutable();
        var policy = CombatGraphFixturePolicy.Create();
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();

        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        var originalVar = source.DynamicVars.Values.OfType<CalculatedVar>().Single();
        var copiedVar = clone.DynamicVars.Values.OfType<CalculatedVar>().Single();
        Assert.NotNull(originalVar._multiplierCalc);
        Assert.Same(originalVar._multiplierCalc, copiedVar._multiplierCalc);
        Assert.Same(clone, copiedVar._owner);
        Assert.NotSame(originalVar, copiedVar);
        before.AssertUnchanged();
    }

    [Fact]
    public void EventsAreClearedWithoutInvokingOrChangingSourceSubscribers()
    {
        var (combat, creature, card) = ArrangeGraph();
        var calls = 0;
        combat.CreaturesChanged += _ => calls++;
        creature.Died += _ => calls++;
        card.Played += () => calls++;
        card.Played += () => calls += 10;
        var policy = CombatGraphFixturePolicy.Create();
        var before = GraphSourceSnapshot.Capture(combat, policy);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(combat);

        ObjectGraphAssert.Equivalent(combat, clone, scope, policy);
        Assert.Null(ReadEvent<Action<ICombatState>>(clone, nameof(CombatState.CreaturesChanged)));
        Assert.Null(ReadEvent<Action<Creature>>(scope.GetClone(creature), nameof(Creature.Died)));
        Assert.Null(ReadEvent<Action>(scope.GetClone(card), nameof(CardModel.Played)));
        Assert.Equal(2, ReadEvent<Action>(card, nameof(CardModel.Played))!.GetInvocationList().Length);
        Assert.Equal(0, calls);
        before.AssertUnchanged();
    }

    private static (CombatState Combat, Creature Creature, CardModel Card) ArrangeGraph()
    {
        ModelDb.Inject(typeof(StrikeIronclad));
        ModelDb.Inject(typeof(BigDummy));
        var combat = new CombatState();
        var monster = (BigDummy)ModelDb.Monster<BigDummy>().ToMutable();
        var creature = new Creature(monster, CombatSide.Enemy, "policy-test") { CombatState = combat, CombatId = 1 };
        creature._currentHp = 53;
        combat._enemies.Add(creature);
        var card = ModelDb.Card<StrikeIronclad>().ToMutable();
        card.CurrentTarget = creature;
        card.CurrentPlayIndex = 3;
        card.EnergyCost._base = 0;
        card.DynamicVars.Damage.PreviewValue = 27;
        card._keywords = [CardKeyword.Retain];
        card._tags = [CardTag.Strike];
        card.EnergyCost._localModifiers.Add(new LocalCostModifier(2, LocalCostType.Absolute, LocalCostModifierExpiration.WhenPlayed, false));
        combat._allCards.Add(card);
        return (combat, creature, card);
    }

    private static T? ReadEvent<T>(object instance, string name) where T : Delegate =>
        (T?)GraphTypeLayout.For(instance.GetType()).Fields.Single(field => field.Name == name).GetValue(instance);

}
