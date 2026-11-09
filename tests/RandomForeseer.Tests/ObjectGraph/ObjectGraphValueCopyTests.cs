using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects value/reference boundaries and failure isolation of <see cref="PredictionGraphCopier"/>.</summary>
/// <remarks>
/// Uses real managed values and arrays without game models or global services. Nullable rejection diagnostics
/// preserve the underlying Value segment through <see cref="PredictionGraphCopier"/> before unwinding.
/// Explicit underlying Reject/Fields rules must reach nullable roots and fields even when their data is immutable;
/// empty values bypass underlying copying, and field Keep/Reset still cut the edge.
/// Field-free value copies retain box identities; emitted readonly writes must preserve boxed back-references
/// and must never write into the caller's inline value storage.
/// </remarks>
public sealed class ObjectGraphValueCopyTests
{
    [Fact]
    public void NestedReadonlyNullableValuesAndStructArraysRemapTheirReferences()
    {
        var node = new Node { Amount = 11 };
        var state = new State { Target = node, Count = 3 };
        var input = new Envelope(state);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy());

        var clone = copier.Copy(input);
        State?[] values = [state, null, state];
        var arrayCopy = copier.Copy(values);
        var nodeCopy = scope.GetClone(node);

        Assert.Same(nodeCopy, clone.Value!.Value.Target);
        Assert.Same(nodeCopy, arrayCopy[0]!.Value.Target);
        Assert.Same(nodeCopy, arrayCopy[2]!.Value.Target);
        Assert.Null(arrayCopy[1]);
        Assert.Null(copier.Copy<State?>(null));
        Assert.Equal(3, clone.Value.Value.Count);
        nodeCopy.Amount = 20;
        Assert.Equal(11, node.Amount);
        Assert.Same(node, input.Value!.Value.Target);
    }

    [Fact]
    public void RealBoxesPreserveAliasesAndIndependentEqualIdentities()
    {
        var node = new Node();
        object box = new State { Target = node, Count = 5 };
        object otherBox = new State { Target = node, Count = 5 };
        object number = 42;
        object otherNumber = 42;
        object[] source = [box, box, otherBox, number, number, otherNumber];
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy());

        var clone = copier.Copy(source);

        Assert.Same(clone[0], clone[1]);
        Assert.NotSame(box, clone[0]);
        Assert.NotSame(clone[0], clone[2]);
        Assert.Same(scope.GetClone(node), ((State)clone[0]).Target);
        Assert.Same(((State)clone[0]).Target, ((State)clone[2]).Target);
        Assert.Same(box, scope.GetSource(clone[0]));
        Assert.Same(clone[3], clone[4]);
        Assert.Same(number, clone[3]);
        Assert.NotSame(clone[3], clone[5]);
        Assert.Equal(42, clone[3]);
        Assert.False(scope.TryGetClone(number, out _));
        Assert.Same(node, ((State)box).Target);
    }

    [Fact]
    public void ArrayDispatchUsesExactElementTypeAndCopiesStructElements()
    {
        var node = new Node();
        object[] covariant = new Node[] { node };
        State[,] matrix = { { new() { Target = node } }, { new() { Target = node } } };
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy());

        var covariantCopy = copier.Copy(covariant);
        var matrixCopy = copier.Copy(matrix);
        var emptyCopy = copier.Copy(new State[0, 2]);

        Assert.IsType<Node[]>(covariantCopy);
        Assert.Same(scope.GetClone(node), covariantCopy[0]);
        Assert.Equal(2, matrixCopy.GetLength(0));
        Assert.Equal(1, matrixCopy.GetLength(1));
        Assert.Same(covariantCopy[0], matrixCopy[0, 0].Target);
        Assert.Same(covariantCopy[0], matrixCopy[1, 0].Target);
        Assert.Equal(2, emptyCopy.Rank);
        Assert.Equal(2, emptyCopy.GetLength(1));
        Assert.Empty(emptyCopy);
    }

    [Fact]
    public void BoundPlansDoNotLeakBetweenDifferentPolicies()
    {
        var source = new State { Target = new Node() };
        using var firstScope = new PredictionObjectScope();
        using var secondScope = new PredictionObjectScope();
        var first = new PredictionGraphCopier(firstScope, CreatePolicy());
        var second = new PredictionGraphCopier(secondScope, CreatePolicy(keepTargets: true));

        var copied = first.Copy(source);
        var kept = second.Copy(source);

        Assert.NotSame(source.Target, copied.Target);
        Assert.Same(source.Target, kept.Target);
        Assert.False(secondScope.TryGetClone(source.Target!, out _));
    }

    [Fact]
    public void FieldRulesPreserveValuesResetFieldsAndCopyRuntimeTypes()
    {
        var source = new PlannedOwner();
        var policy = CreatePlanPolicy();
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        var clone = copier.Copy(source);

        Assert.Equal(source.Scalar, clone.Scalar);
        Assert.Equal(source.Pair, clone.Pair);
        Assert.Same(source.Text, clone.Text);
        Assert.Same(source.Ignored, clone.Ignored);
        Assert.Equal(0, clone.Reset);
        Assert.NotSame(source.Polymorphic, clone.Polymorphic);
        var derived = Assert.IsType<DerivedLeaf>(clone.Polymorphic);
        Assert.Equal(5, derived.Amount);
        derived.Amount = 9;
        Assert.Equal(5, ((DerivedLeaf)source.Polymorphic).Amount);
        Assert.Equal(6, source.Reset);
    }

    [Fact]
    public void NullableChildRejectionPreservesTheUnderlyingValuePath()
    {
        var source = new Envelope(new State { Target = new Node() });
        var policy = new GraphCopyPolicy(valueRules:
            [new("test.values", type => type == typeof(State) || type == typeof(Envelope) ? GraphValueAction.Fields : null)]);
        using var scope = new PredictionObjectScope();

        var error = Assert.Throws<NotSupportedException>(() => new PredictionGraphCopier(scope, policy).Copy(source, "$capture"));

        Assert.StartsWith("$capture.Value.Value.Target: ", error.Message);
        Assert.Throws<ObjectDisposedException>(() => scope.GetClone(source.Value!.Value.Target!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullableStorageAppliesUnderlyingRejectionOnlyWhenPresent(bool field)
    {
        var policy = new GraphCopyPolicy(
            referenceFallback: GraphReferenceStrategy.Clone,
            valueRules: [new("test.reject-int", type => type == typeof(int) ? GraphValueAction.Reject : null)]);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        if (field)
        {
            Assert.Null(copier.Copy(new NullableOwner<int>(), "$empty").Value);
        }
        else
        {
            Assert.Null(copier.Copy<int?>(null, "$empty"));
        }

        var error = Assert.Throws<NotSupportedException>(() =>
        {
            if (field)
            {
                copier.Copy(new NullableOwner<int> { Value = 7 }, "$capture");
            }
            else
            {
                copier.Copy<int?>(7, "$capture");
            }
        });

        Assert.StartsWith(field ? "$capture.Value.Value: " : "$capture.Value: ", error.Message);
        Assert.Contains("test.reject-int", error.Message);
        Assert.Throws<ObjectDisposedException>(() => copier.Copy(0));
    }

    [Fact]
    public void NullableFieldsFollowUnderlyingResetAndPreserveFieldBoundariesAcrossPolicies()
    {
        var source = new NullableOwner<ReadonlyValue> { Value = new ReadonlyValue(7) };
        var policy = new GraphCopyPolicy(
            referenceFallback: GraphReferenceStrategy.Clone,
            valueRules: [new("test.value-fields", type => type == typeof(ReadonlyValue) ? GraphValueAction.Fields : null)],
            fieldRules: [new("test.reset-number", field => field.DeclaringType == typeof(ReadonlyValue) &&
                field.Name == nameof(ReadonlyValue.Number) ? GraphFieldAction.Reset : null)]);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        var rootCopy = copier.Copy(source.Value);
        var fieldCopy = copier.Copy(source);

        Assert.True(rootCopy.HasValue);
        Assert.Equal(0, rootCopy.Value.Number);
        Assert.True(fieldCopy.Value.HasValue);
        Assert.Equal(0, fieldCopy.Value.Value.Number);
        ObjectGraphAssert.Equivalent(source, fieldCopy, scope, policy);
        before.AssertUnchanged();
        Assert.Equal(7, source.Value!.Value.Number);

        using var keptScope = new PredictionObjectScope();
        var kept = new PredictionGraphCopier(keptScope, new GraphCopyPolicy(
            referenceFallback: GraphReferenceStrategy.Clone)).Copy(source);
        Assert.Equal(7, kept.Value!.Value.Number);

        foreach (var action in new[] { GraphFieldAction.Keep, GraphFieldAction.Reset })
        {
            using var boundaryScope = new PredictionObjectScope();
            var boundaryPolicy = new GraphCopyPolicy(
                referenceFallback: GraphReferenceStrategy.Clone,
                valueRules: [new("test.reject-value", type => type == typeof(ReadonlyValue) ? GraphValueAction.Reject : null)],
                fieldRules: [new("test.field-boundary", field => field.DeclaringType == typeof(NullableOwner<ReadonlyValue>) ? action : null)]);
            var boundaryCopy = new PredictionGraphCopier(boundaryScope, boundaryPolicy).Copy(source);
            if (action == GraphFieldAction.Keep)
            {
                Assert.Equal(7, boundaryCopy.Value!.Value.Number);
            }
            else
            {
                Assert.Null(boundaryCopy.Value);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutableValueRootsAndBoxesHonorResetWithoutSharingBoxes(bool reset)
    {
        var value = new PlainValue { Number = 7 };
        object box = value;
        object otherBox = value;
        object[] source = [box, box, otherBox];
        var policy = new GraphCopyPolicy(valueFallback: GraphValueAction.Fields,
            fieldRules: [new("test.reset", field => reset && field.DeclaringType == typeof(PlainValue) ? GraphFieldAction.Reset : null)]);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        var copiedValue = copier.Copy(in value);
        var copiedBoxes = copier.Copy(source);

        Assert.Equal(reset ? 0 : 7, copiedValue.Number);
        Assert.Equal(reset ? 0 : 7, ((PlainValue)copiedBoxes[0]).Number);
        Assert.NotSame(box, copiedBoxes[0]);
        Assert.Same(copiedBoxes[0], copiedBoxes[1]);
        Assert.NotSame(copiedBoxes[0], copiedBoxes[2]);
        Assert.Same(box, scope.GetSource(copiedBoxes[0]));
        Assert.Equal(7, value.Number);
        Assert.Equal(7, ((PlainValue)box).Number);
    }

    [Fact]
    public void ReadonlyValueAndBoxReferencesAreRemappedWithoutReplacingTheRegisteredBox()
    {
        var node = new Node { Amount = 8 };
        var value = new ReadonlyReference(node);
        object box = value;
        node.Owner = box;
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        var copiedValue = copier.Copy(in value);
        var copiedBoxes = copier.Copy(new object[] { box, box });
        var copiedNode = scope.GetClone(node);

        Assert.Same(copiedNode, copiedValue.Target);
        Assert.Same(copiedNode, ((ReadonlyReference)copiedBoxes[0]).Target);
        Assert.Same(copiedBoxes[0], copiedBoxes[1]);
        Assert.Same(copiedBoxes[0], copiedNode.Owner);
        Assert.Same(box, scope.GetSource(copiedBoxes[0]));
        Assert.Same(node, value.Target);
        Assert.Same(node, ((ReadonlyReference)box).Target);
        Assert.Same(box, node.Owner);
    }

    private static GraphCopyPolicy CreatePlanPolicy() => new(
        referenceRules: [new("test.leaf", type => type == typeof(BaseLeaf) ? GraphReferenceStrategy.Share :
            type == typeof(PlannedOwner) || type == typeof(DerivedLeaf) ? GraphReferenceStrategy.Clone : null)],
        valueRules: [new("test.pair", type => type == typeof((int, int)) ? GraphValueAction.Fields : null)],
        fieldRules: [new("test.fields", field => field.Name switch
        {
            nameof(PlannedOwner.Reset) => GraphFieldAction.Reset,
            nameof(PlannedOwner.Ignored) => GraphFieldAction.Keep,
            _ => null
        })]);

    private class BaseLeaf;
    private sealed class DerivedLeaf : BaseLeaf { public int Amount = 5; }

    private sealed class PlannedOwner
    {
        public int Scalar = 3;
        public (int, int) Pair = (4, 5);
        public string Text = "leaf";
        public object Ignored = new();
        public int Reset = 6;
        public BaseLeaf Polymorphic = new DerivedLeaf();
    }

    private static GraphCopyPolicy CreatePolicy(bool keepTargets = false) => new(
        referenceRules: [new("test.nodes", type => type == typeof(Node) ? GraphReferenceStrategy.Clone : null)],
        valueRules: [new("test.values", type => type == typeof(State) || type == typeof(Envelope) ? GraphValueAction.Fields : null)],
        fieldRules: [new("test.target", field => keepTargets && field.DeclaringType == typeof(State) &&
            field.Name == nameof(State.Target) ? GraphFieldAction.Keep : null)]);

    private sealed class Node { public int Amount; public object? Owner; }
    private struct PlainValue { public int Number; }
    private readonly struct ReadonlyReference(Node target) { public readonly Node Target = target; }
    private struct State
    {
        public int Count;
        public Node? Target;
    }
    private readonly struct Envelope(State? value) { public readonly State? Value = value; }
    private readonly struct ReadonlyValue(int number) { public readonly int Number = number; }
    private sealed class NullableOwner<T> where T : struct { public T? Value; }

}
