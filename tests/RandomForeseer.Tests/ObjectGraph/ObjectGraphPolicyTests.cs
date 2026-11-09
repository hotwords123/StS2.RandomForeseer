using Godot;
using Xunit.Sdk;
using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Common;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects common boundaries in <see cref="GraphCopyPolicy"/> and graph verification helpers.</summary>
/// <remarks>Pure managed fixtures, including Godot value types; native resources are inspected only as metadata.</remarks>
public sealed class ObjectGraphPolicyTests
{
    [Fact]
    public void ExplicitResetBoundariesPreserveSourceAndReportRuleIds()
    {
        var source = new BoundaryOwner { ResetValue = 5 };
        source.Subscribe(() => { });
        var display = typeof(BoundaryOwner).GetField("Display", ReflectionFlags.NonPublicInstance)!;
        var reset = typeof(BoundaryOwner).GetField(nameof(BoundaryOwner.ResetValue))!;
        var policy = new GraphCopyPolicy(
            referenceRules: [new("test.owner", type => type == typeof(BoundaryOwner) ? GraphReferenceStrategy.Clone : null)],
            fieldRules:
            [
                new("test.display", field => field == display ? GraphFieldAction.Reset : null),
                new("test.counter", field => field == reset ? GraphFieldAction.Reset : null)
            ]);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();

        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var boundaries = ObjectGraphAssert.Equivalent(source, clone, scope, policy);

        before.AssertUnchanged();
        Assert.Null(display.GetValue(clone));
        Assert.NotNull(display.GetValue(source));
        Assert.Equal(0, clone.ResetValue);
        Assert.Contains(boundaries, b => b.RuleId == "test.display" && b.Path.EndsWith(".Display"));
    }

    [Theory]
    [InlineData(typeof(Resource))]
    [InlineData(typeof(Task<int>))]
    [InlineData(typeof(ValueTask<int>))]
    [InlineData(typeof(CancellationToken))]
    [InlineData(typeof(CancellationTokenSource))]
    [InlineData(typeof(MemoryStream))]
    [InlineData(typeof(IntPtr))]
    [InlineData(typeof(WeakReference<Node>))]
    public void OpaqueBoundariesNeverFallThroughToOrdinaryFields(Type type)
    {
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        Assert.Equal(GraphReferenceAction.Reject, policy.BindReferenceRule(type).Strategy.StaticAction);
        if (type.IsValueType)
        {
            Assert.Equal(GraphValueAction.Reject, policy.BindValueRule(type).Strategy);
        }
    }

    [Fact]
    public void GodotVectorValuesCopyWithoutNativeResourceBoundaries()
    {
        Vector2[] source = [new(3, 4)];
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields));

        Assert.Equal(source[0], copier.Copy(source[0]));
        var clone = copier.Copy(source);
        Assert.NotSame(source, clone);
        Assert.Equal(source[0], clone[0]);
        clone[0].X = 9;
        Assert.Equal(3f, source[0].X);
    }

    [Fact]
    public void DelegateResourceTargetIsRejectedWithItsFieldPath()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new CallbackOwner { Callback = cancellation.Cancel };
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        using var scope = new PredictionObjectScope();
        var error = Assert.Throws<NotSupportedException>(() => new PredictionGraphCopier(scope, policy).Copy(source));
        Assert.StartsWith("$root.Callback.Target: ", error.Message);
        Assert.False(cancellation.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => scope.GetClone(source));
    }

    [Fact]
    public void CheckerDetectsLeakedSourceAndBrokenAliases()
    {
        var child = new Node();
        var source = new Node { Next = child, Items = [child, child] };
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var childCopy = clone.Next;
        clone.Next = child;

        var leak = Assert.ThrowsAny<XunitException>(() => ObjectGraphAssert.Equivalent(source, clone, scope, policy));
        Assert.Contains(".Next", leak.Message);
        Assert.Contains("source reference leaked", leak.Message);

        clone.Next = childCopy;
        clone.Items[1] = new Node();
        var alias = Assert.ThrowsAny<XunitException>(() => ObjectGraphAssert.Equivalent(source, clone, scope, policy));
        Assert.Contains("[1]", alias.Message);
        Assert.Contains("alias mismatch", alias.Message);
    }

    [Fact]
    public void SourceSnapshotDetectsChangedInlineValueAndArraySlot()
    {
        var source = new Node { Data = new Payload(null, 3), Items = [new Node()] };
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        source.Data = new Payload(null, 4);
        Assert.Contains("Amount", Assert.ThrowsAny<XunitException>(before.AssertUnchanged).Message);

        source.Data = new Payload(null, 3);
        source.Items[0] = new Node();
        Assert.Contains("[0]", Assert.ThrowsAny<XunitException>(before.AssertUnchanged).Message);
    }

    [Fact]
    public void SourceSnapshotRefreshesCollectionSlotsOnEveryCheck()
    {
        var node = new Node { Amount = 3 };
        var original = new Payload(node, 7);
        List<Payload?> values = [original];
        var source = new Dictionary<string, List<Payload?>> { ["values"] = values };
        var before = GraphSourceSnapshot.Capture(source, new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields));
        before.AssertUnchanged();

        // Cached enumeration must still observe fresh inline values and referenced objects on later checks.
        values[0] = new Payload(node, 8);
        Assert.Contains("Amount", Assert.ThrowsAny<XunitException>(before.AssertUnchanged).Message);
        values[0] = original;
        before.AssertUnchanged();

        node.Amount = 4;
        Assert.Contains("Amount", Assert.ThrowsAny<XunitException>(before.AssertUnchanged).Message);
        node.Amount = 3;
        values[0] = null;
        Assert.Contains("nullable state changed", Assert.ThrowsAny<XunitException>(before.AssertUnchanged).Message);
        values[0] = original;
        before.AssertUnchanged();

        values.Add(null);
        Assert.Contains("collection count changed", Assert.ThrowsAny<XunitException>(before.AssertUnchanged).Message);
        values.RemoveAt(1);
        before.AssertUnchanged();

        source.Remove("values");
        source.Add("other", values);
        Assert.Contains(".Key", Assert.ThrowsAny<XunitException>(before.AssertUnchanged).Message);
    }

    private sealed class Node
    {
        public int Amount;
        public Node? Next;
        public Payload? Data;
        public object[] Items = [];
    }

    private readonly struct Payload(Node? target, int amount)
    {
        public readonly Node? Target = target;
        public readonly int Amount = amount;
    }

    private sealed class CallbackOwner { public Action? Callback; }

    private sealed class BoundaryOwner
    {
        public int ResetValue;
        private event Action? Display;
        public bool HasDisplay => Display != null;
        public void Subscribe(Action callback) => Display += callback;
    }
}
