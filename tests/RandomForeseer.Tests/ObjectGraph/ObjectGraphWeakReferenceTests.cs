using System.Runtime.CompilerServices;
using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects lookup, graph identity and source isolation for weak edges copied by <see cref="PredictionGraphCopier"/>.</summary>
/// <remarks>Managed fixtures keep live keys and targets strongly reachable for deterministic assertions.</remarks>
public sealed class ObjectGraphWeakReferenceTests
{
    [Fact]
    public void WeakTableUsesCopiedKeyIdentitiesAndPreservesBackReferences()
    {
        var firstKey = new Node { Number = 3 };
        var secondKey = new Node { Number = 3 };
        var table = new ConditionalWeakTable<Node, object?>();
        object[] source = [table, firstKey, secondKey];
        table.Add(firstKey, source);
        table.Add(secondKey, null);
        using var scope = new PredictionObjectScope();
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone);

        var copy = new PredictionGraphCopier(scope, policy).Copy(source);
        var copiedTable = (ConditionalWeakTable<Node, object?>)copy[0];
        var copiedFirstKey = (Node)copy[1];
        var copiedSecondKey = (Node)copy[2];

        Assert.NotSame(firstKey, copiedFirstKey);
        Assert.False(copiedTable.TryGetValue(firstKey, out _));
        Assert.True(copiedTable.TryGetValue(copiedFirstKey, out var value));
        Assert.Same(copy, value);
        Assert.True(copiedTable.TryGetValue(copiedSecondKey, out var nullValue));
        Assert.Null(nullValue);

        Assert.True(copiedTable.Remove(copiedFirstKey));
        Assert.True(table.TryGetValue(firstKey, out var originalValue));
        Assert.Same(source, originalValue);
    }

    [Fact]
    public void WeakReferencesFollowTheSameCopyAsAStrongReference()
    {
        var target = new Node();
        var untyped = new WeakReference(target);
        var typed = new WeakReference<Node>(target);
        object[] source = [untyped, typed, target];
        using var scope = new PredictionObjectScope();
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone);

        var copy = new PredictionGraphCopier(scope, policy).Copy(source);
        var copiedUntyped = (WeakReference)copy[0];
        var copiedTyped = (WeakReference<Node>)copy[1];

        Assert.NotSame(target, copy[2]);
        Assert.Same(copy[2], copiedUntyped.Target);
        Assert.True(copiedTyped.TryGetTarget(out var copiedTarget));
        Assert.Same(copy[2], copiedTarget);

        copiedUntyped.Target = null;
        copiedTyped.SetTarget(null!);
        Assert.Same(target, untyped.Target);
        Assert.True(typed.TryGetTarget(out var originalTarget));
        Assert.Same(target, originalTarget);
    }

    [Fact]
    public void EmptyWeakReferencesRemainEmptyAndCanBeRetargetedIndependently()
    {
        var untyped = new WeakReference(null);
        var typed = new WeakReference<Node>(null!);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, new GraphCopyPolicy());

        var copiedUntyped = copier.Copy(untyped);
        var copiedTyped = copier.Copy(typed);

        Assert.Null(copiedUntyped.Target);
        Assert.False(copiedTyped.TryGetTarget(out _));

        var target = new Node();
        copiedUntyped.Target = target;
        copiedTyped.SetTarget(target);
        Assert.Same(target, copiedUntyped.Target);
        Assert.True(copiedTyped.TryGetTarget(out var copiedTarget));
        Assert.Same(target, copiedTarget);
        Assert.Null(untyped.Target);
        Assert.False(typed.TryGetTarget(out _));
    }

    private sealed record Node
    {
        public int Number { get; set; }
    }
}
