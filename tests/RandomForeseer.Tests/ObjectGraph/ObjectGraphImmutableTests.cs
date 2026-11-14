using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Metadata;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects immutable sharing, polymorphic storage and explicit field overrides of <see cref="PredictionGraphCopier"/>.</summary>
/// <remarks>
/// Pure managed fixtures; readonly cycles may share, but mutable dependencies and resource ownership must
/// prevent sharing regardless of the analysis entry point or previously cached dependency results.
/// </remarks>
public sealed class ObjectGraphImmutableTests
{
    [Fact]
    public void ReadonlyCyclesShareFromEitherEntryPoint()
    {
        var source = new ReadonlyCycleRoot();
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, new GraphCopyPolicy());

        Assert.Same(source, copier.Copy(source));
        Assert.Same(source.Child, copier.Copy(source.Child));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutableCycleDependenciesRemainIsolatedRegardlessOfAnalysisOrder(bool childFirst)
    {
        // Distinct closed graphs give each order its own initially uncached types without resetting global state.
        if (childFirst)
        {
            CheckMutableCycle<ChildFirst>(childFirst);
        }
        else
        {
            CheckMutableCycle<RootFirst>(childFirst);
        }
    }

    private static void CheckMutableCycle<T>(bool childFirst)
    {
        var source = new MutableCycleRoot<T> { Amount = 3 };
        var entryType = childFirst ? typeof(MutableCycleChild<T>) : typeof(MutableCycleRoot<T>);
        Assert.False(GraphTypeTraits.IsImmutable(entryType));
        using var scope = new PredictionObjectScope();
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone);

        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        clone.Child.Parent.Amount = 9;

        Assert.Same(clone, clone.Child.Parent);
        Assert.Equal(3, source.Amount);
    }

    [Fact]
    public void ReadonlyDataNullableValuesAndImmutableBoxesAreShared()
    {
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var leaf = new Leaf("value");
        var source = new ImmutableHolder(leaf, new Value(7));
        object box = new Value(9);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        Assert.Same(source, copier.Copy(source));
        Assert.Same(box, copier.Copy(box));
        Assert.Equal(new Value(7), copier.Copy<Value?>(new Value(7)));
        Assert.Null(copier.Copy<Value?>(null));
        Assert.False(scope.TryGetClone(source, out _));
        Assert.False(scope.TryGetClone(box, out _));
    }

    [Fact]
    public void ReadonlyPolymorphicSlotsAndMutableBackingFieldsDoNotGrantSharing()
    {
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var derived = new Derived(3);
        var source = new PolymorphicHolder(derived);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        var baseInstance = new Base(1);
        Assert.Same(baseInstance, copier.Copy(baseInstance));
        var clone = copier.Copy(source);
        Assert.NotSame(source, clone);
        var copiedDerived = Assert.IsType<Derived>(clone.Item);
        Assert.NotSame(derived, copiedDerived);
        copiedDerived.Items.Add(5);
        Assert.Empty(derived.Items);

        Assert.False(GraphTypeTraits.IsImmutable(typeof(ResourceHolder)));
    }

    [Fact]
    public void ExplicitCloneAndResetOverrideDefaultImmutableSharing()
    {
        var source = new Leaf("retained");
        using var sharedScope = new PredictionObjectScope();
        Assert.Same(source, new PredictionGraphCopier(sharedScope, new GraphCopyPolicy()).Copy(source));
        using var resetScope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(resetScope, new GraphCopyPolicy(
            referenceRules: [new("test.clone-leaf", type => type == typeof(Leaf) ? GraphReferenceStrategy.Clone : null)],
            fieldRules: [new("test.reset-leaf", field => field.DeclaringType == typeof(Leaf) ? GraphFieldAction.Reset : null)])).Copy(source);
        Assert.NotSame(source, clone);
        Assert.Null(clone.Text);
        Assert.Equal("retained", source.Text);
    }

    [Fact]
    public void ExplicitResetCutsRejectedResourceEdgeWithoutChangingSource()
    {
        var source = new ResourceHolder();
        var originalTask = source.Completion;
        var field = typeof(ResourceHolder).GetField(nameof(ResourceHolder.Completion))!;
        var policy = new GraphCopyPolicy(
            referenceRules: [new("test.resource-holder", type => type == typeof(ResourceHolder) ? GraphReferenceStrategy.Clone : null)],
            fieldRules: [new("test.resource-edge", candidate => candidate == field ? GraphFieldAction.Reset : null)]);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        Assert.Null(clone.Completion);
        Assert.Same(originalTask, source.Completion);
    }

    private sealed class Leaf(string text)
    {
        public readonly string Text = text;
    }

    private readonly record struct Value(int Number);
    private sealed record ImmutableHolder(Leaf Leaf, Value? Value);
    private class Base(int number) { public readonly int Number = number; }
    private sealed class Derived(int number) : Base(number) { public readonly List<int> Items = []; }
    private sealed class PolymorphicHolder(Base item) { public readonly Base Item = item; }
    private sealed class ResourceHolder { public readonly Task Completion = Task.CompletedTask; }

    private sealed class ReadonlyCycleRoot
    {
        public readonly ReadonlyCycleChild Child;

        public ReadonlyCycleRoot() => Child = new ReadonlyCycleChild(this);
    }

    private sealed class ReadonlyCycleChild(ReadonlyCycleRoot parent)
    {
        public readonly ReadonlyCycleRoot Parent = parent;
    }

    private sealed class MutableCycleRoot<T>
    {
        // Visit the back-reference dependency before encountering the mutable field.
        public readonly MutableCycleChild<T> Child;
        public int Amount;

        public MutableCycleRoot() => Child = new MutableCycleChild<T>(this);
    }

    private sealed class MutableCycleChild<T>(MutableCycleRoot<T> parent)
    {
        public readonly MutableCycleRoot<T> Parent = parent;
    }

    private sealed class RootFirst;
    private sealed class ChildFirst;
}
