using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects identity, per-instance policies and failure isolation in <see cref="PredictionGraphCopier"/>.</summary>
/// <remarks>
/// Pure managed fixtures exercise the common framework without game models or global services, including
/// independent captures and failure recovery across scopes sharing one stable policy, plus diagnostic path
/// isolation between successive roots copied by the same <see cref="PredictionGraphCopier"/>.
/// </remarks>
public sealed class ObjectGraphCopyFrameworkTests
{
    [Fact]
    public void FieldCopyPreservesIdentityCyclesReadonlyFieldsAndStructReferences()
    {
        var first = new Node { Amount = 4 };
        var second = new Node { Amount = 4 };
        first.Next = second;
        second.Next = first;
        first.Pair = (second, second);
        var constructions = Node.Constructions;
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy());

        var clone = copier.Copy(first);
        var other = copier.Copy(second);

        Assert.Equal(constructions, Node.Constructions);
        Assert.NotSame(clone, other);
        Assert.Same(other, clone.Next);
        Assert.Same(clone, other.Next);
        Assert.Same(other, clone.Pair.Item1);
        Assert.Same(other, clone.Pair.Item2);
        Assert.Same(clone, clone.Self);
        Assert.Same(first, scope.GetSource(clone));
        Assert.Same(second, scope.GetSource(other));
        other.Amount = 9;
        Assert.Equal(4, second.Amount);
    }

    [Fact]
    public void TypeAndFieldPoliciesCanDistinguishInstancesAndEdges()
    {
        var definition = new Node { Shared = true };
        var mutable = new Node { Amount = 3 };
        var root = new Node { Next = mutable, SharedEdge = mutable, ResetEdge = mutable, Pair = (definition, mutable), ResetValue = 7 };
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy());

        Assert.Same(definition, copier.Copy(definition));
        var clone = copier.Copy(root);

        ObjectGraphAssert.Equivalent(root, clone, scope, CreatePolicy());

        Assert.NotSame(mutable, clone.Next);
        Assert.Same(mutable, clone.SharedEdge);
        Assert.Null(clone.ResetEdge);
        Assert.Equal(0, clone.ResetValue);
        Assert.Same(definition, clone.Pair.Item1);
        Assert.Same(clone.Next, clone.Pair.Item2);
        Assert.False(scope.TryGetClone(definition, out _));
        Assert.False(scope.TryGetSource(definition, out _));
        // Sharing the same Type must not poison later mutable-instance dispatch.
        Assert.Same(mutable, scope.GetSource(clone.Next!));
    }

    [Fact]
    public void ArrayHandlerRegistersBeforeFollowingElementsAndPreservesShape()
    {
        var array = Array.CreateInstance(typeof(object), [2, 1], [-1, 3]);
        var node = new Node();
        array.SetValue(array, -1, 3);
        array.SetValue(node, 0, 3);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy());

        var before = GraphSourceSnapshot.Capture(array, CreatePolicy());
        var clone = copier.Copy(array);
        ObjectGraphAssert.Equivalent(array, clone, scope, CreatePolicy());
        before.AssertUnchanged();

        Assert.Equal(-1, clone.GetLowerBound(0));
        Assert.Equal(3, clone.GetLowerBound(1));
        Assert.Same(clone, clone.GetValue(-1, 3));
        Assert.Same(copier.Copy(node), clone.GetValue(0, 3));
        Assert.Same(array, scope.GetSource(clone));
    }

    [Fact]
    public void RejectedTypeInvalidatesTheWholeScopeWithoutTouchingSource()
    {
        var source = new Node { Unknown = Task.CompletedTask };
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy());
        var previous = new Node();
        var retained = copier.Copy(previous);

        var error = Assert.Throws<NotSupportedException>(() => copier.Copy(source));

        Assert.Contains(nameof(Node.Unknown), error.Message);
        Assert.Throws<ObjectDisposedException>(() => scope.GetClone(source));
        Assert.Throws<ObjectDisposedException>(() => scope.GetSource(retained));
        Assert.Throws<ObjectDisposedException>(() => copier.Copy("shared leaf"));
        Assert.NotNull(source.Unknown);
        Assert.Same(retained, retained.Self);
    }

    [Fact]
    public void StablePolicyKeepsLaterCapturesIndependentAndReevaluatesInstanceRules()
    {
        var policy = CreatePolicy();
        var definition = new Node { Shared = true };
        var child = new Node { Amount = 3 };
        var source = new Node { Next = child, Pair = (definition, child) };
        child.Next = source;
        Node firstClone;
        using (var firstScope = new PredictionObjectScope())
        {
            firstClone = new PredictionGraphCopier(firstScope, policy).Copy(source);
            Assert.Same(definition, firstClone.Pair.Item1);
            Assert.Same(firstClone.Next, firstClone.Pair.Item2);
        }

        // Change source state between capture windows; a later scope must read it afresh.
        child.Amount = 7;
        definition.Shared = false;
        source.Unknown = new Node[] { source, child };
        using var secondScope = new PredictionObjectScope();
        var secondClone = new PredictionGraphCopier(secondScope, policy).Copy(source);
        var copiedArray = Assert.IsType<Node[]>(secondClone.Unknown);

        Assert.NotSame(firstClone, secondClone);
        Assert.NotSame(firstClone.Next, secondClone.Next);
        Assert.Equal(3, firstClone.Next!.Amount);
        Assert.Equal(7, secondClone.Next!.Amount);
        Assert.Same(secondClone, secondClone.Next.Next);
        Assert.Same(secondClone, secondClone.Self);
        Assert.Same(secondClone, copiedArray[0]);
        Assert.Same(secondClone.Next, copiedArray[1]);
        Assert.Same(secondScope.GetClone(definition), secondClone.Pair.Item1);
        Assert.Same(secondClone.Next, secondClone.Pair.Item2);
        Assert.Same(source, secondScope.GetSource(secondClone));
        secondClone.Next.Amount = 9;
        Assert.Equal(7, child.Amount);
        Assert.Equal(3, firstClone.Next.Amount);
    }

    [Fact]
    public void FailedCaptureLeavesLaterScopesAndDiagnosticPathsIndependent()
    {
        var policy = CreatePolicy();
        var source = new Node { Unknown = Task.CompletedTask };
        using (var failedScope = new PredictionObjectScope())
        {
            var copier = new PredictionGraphCopier(failedScope, policy);
            copier.Copy(new Node { Next = new Node { Pair = (new Node(), new Node()) } }, "$previous");
            var error = Assert.Throws<NotSupportedException>(() => copier.Copy(source, "$failed"));
            Assert.StartsWith("$failed.Unknown: ", error.Message);
            Assert.Throws<ObjectDisposedException>(() => copier.Copy(source));
        }

        using (var retryScope = new PredictionObjectScope())
        {
            var copier = new PredictionGraphCopier(retryScope, policy);
            var error = Assert.Throws<NotSupportedException>(() => copier.Copy(source, "$retry"));
            Assert.Contains("$retry.Unknown", error.Message);
            Assert.DoesNotContain("$failed", error.Message);
        }

        source.Unknown = null;
        source.Next = source;
        using var successfulScope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(successfulScope, policy).Copy(source);

        Assert.NotSame(source, clone);
        Assert.Same(clone, clone.Next);
        Assert.Same(clone, clone.Self);
        Assert.Same(source, successfulScope.GetSource(clone));
    }

    [Fact]
    public void RulesSkipNonMatchesAndStopAtFirstDecisionIncludingRejection()
    {
        GraphReferenceRule unrelated = new("unrelated", type => type == typeof(string) ? GraphReferenceStrategy.Reject : null);
        GraphReferenceRule clone = new("copy-node", type => type == typeof(Node) ? GraphReferenceStrategy.Clone : null);
        GraphReferenceRule reject = new("blocked-node", type => type == typeof(Node) ? GraphReferenceStrategy.Reject : null);
        var source = new Node();
        using var copiedScope = new PredictionObjectScope();
        var policy = new GraphCopyPolicy(referenceRules: [unrelated, clone, reject],
            valueRules: [new("pair", type => type == typeof((Node, Node)) ? GraphValueAction.Fields : null)]);

        var copied = new PredictionGraphCopier(copiedScope, policy).Copy(source);
        Assert.NotSame(source, copied);
        Assert.Same(copied, copied.Self);

        using var rejectedScope = new PredictionObjectScope();
        var rejectedPolicy = new GraphCopyPolicy(referenceRules: [unrelated, reject, clone]);
        var error = Assert.Throws<NotSupportedException>(() =>
            new PredictionGraphCopier(rejectedScope, rejectedPolicy).Copy(source));
        Assert.Contains("blocked-node", error.Message);
    }

    private static GraphCopyPolicy CreatePolicy() => new(
        referenceRules: [new("test.node", type => type == typeof(Node)
            ? GraphReferenceStrategy.Choose(selection => selection.GetSource<Node>().Shared ? GraphReferenceAction.Share : GraphReferenceAction.Clone)
            : null)],
        valueRules: [new("test.pair", type => type == typeof(ValueTuple<Node?, Node?>) ? GraphValueAction.Fields : null)],
        fieldRules: [new("test.edges", field => field.Name switch
        {
            nameof(Node.SharedEdge) => GraphFieldAction.Keep,
            nameof(Node.ResetEdge) or nameof(Node.ResetValue) => GraphFieldAction.Reset,
            _ => null
        })]);

    private abstract class NodeBase
    {
        private readonly NodeBase _self;
        protected NodeBase() => _self = this;
        public NodeBase Self => _self;
    }

    private sealed class Node : NodeBase
    {
        public static int Constructions;
        public int Amount;
        public bool Shared;
        public Node? Next;
        public Node? SharedEdge;
        public Node? ResetEdge;
        public int ResetValue;
        public object? Unknown;
        public (Node?, Node?) Pair;
        public Node() => Constructions++;
        public override bool Equals(object? obj) => obj is Node other && Amount == other.Amount;
        public override int GetHashCode() => Amount;
    }
}
