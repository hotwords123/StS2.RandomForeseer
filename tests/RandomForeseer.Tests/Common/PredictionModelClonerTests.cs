using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Common;

/// <summary>Checks canonical identity and mutable-model isolation in <see cref="PredictionCloner.CloneModel{T}"/>.</summary>
/// <remarks>Uses game fixtures for card attachments, runtime state and event subscribers.</remarks>
[Collection(GameTestCollection.Name)]
public sealed class PredictionModelClonerTests : GameTestBase
{
    [Fact]
    public void MutableCardCopyPreservesRuntimeStateWithoutChangingSource()
    {
        using var combat = new TestCombat();
        var source = combat.ArrangeCard<StrikeIronclad>();
        ModelDb.Inject(typeof(Sharp));
        ModelDb.Inject(typeof(Bound));
        var enchantment = ModelDb.Enchantment<Sharp>().ToMutable();
        enchantment._card = source;
        source.Enchantment = enchantment;
        var affliction = ModelDb.Affliction<Bound>().ToMutable();
        affliction._card = source;
        source.Affliction = affliction;
        source.CurrentTarget = combat.Player.Creature;
        source.DynamicVars.Damage.PreviewValue = 27;
        var sourceCost = source.EnergyCost;
        var executions = 0;
        source.ExecutionFinished += _ => executions++;

        var copy = PredictionCloner.CloneModel(source);
        copy.DynamicVars.Damage.PreviewValue = 5;
        copy.InvokeExecutionFinished();

        Assert.NotSame(source, copy);
        Assert.Same(source.CurrentTarget, copy.CurrentTarget);
        Assert.Same(source, source.Enchantment.Card);
        Assert.Same(source, source.Affliction.Card);
        Assert.Same(copy, copy.Enchantment!.Card);
        Assert.Same(copy, copy.Affliction!.Card);
        Assert.NotSame(sourceCost, copy.EnergyCost);
        Assert.Equal(27, source.DynamicVars.Damage.PreviewValue);
        Assert.Equal(0, executions);
    }

    [Fact]
    public void CanonicalModelReturnsItself()
    {
        ModelDb.Inject(typeof(StrikeIronclad));
        var canonical = ModelDb.Card<StrikeIronclad>();

        Assert.Same(canonical, PredictionCloner.CloneModel(canonical));
    }

    [Fact]
    public void UnreviewedCloneOverrideDoesNotRetainSourceSubscribers()
    {
        ModelDb.Inject(typeof(CloneFallbackAffliction));
        var source = (CloneFallbackAffliction)ModelDb.Affliction<CloneFallbackAffliction>().ToMutable();
        var calls = 0;
        source.ExtraChanged += () => calls++;

        var copy = PredictionCloner.CloneModel(source);
        copy.Raise();
        source.Raise();

        Assert.NotSame(source, copy);
        Assert.Equal(1, calls);
    }
}

public sealed class CloneFallbackAffliction : AfflictionModel
{
    public event Action? ExtraChanged;

    public void Raise() => ExtraChanged?.Invoke();

    protected override void AfterCloned()
    {
        base.AfterCloned();
        ExtraChanged = null;
    }
}
