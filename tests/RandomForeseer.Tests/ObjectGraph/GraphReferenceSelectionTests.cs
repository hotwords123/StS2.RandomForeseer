using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects source/context decisions through <see cref="GraphReferenceSelection"/> in cached plans and delegate targets.</summary>
/// <remarks>
/// Pure managed graphs reuse one policy across different contexts. Sealed reference fields, nested structs,
/// arrays and lists must dispatch instance selectors, and callbacks copied before their targets must use the same decisions.
/// </remarks>
public sealed class GraphReferenceSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedPolicyUsesEachContextForFieldsCollectionsAndCallbackTargets(bool callbackFirst)
    {
        var first = new Node { Amount = 3 };
        var second = new Node { Amount = 7 };
        first.Next = first;
        second.Next = second;
        var source = new Container
        {
            First = first, Second = second, Slot = new(first),
            Array = [first, second, first], List = [second, first, second],
            FirstRead = first.Read, SecondRead = second.Read
        };
        var policy = CreatePolicy();
        foreach (var selected in new[] { first, second, first })
        {
            var context = new SelectionContext(selected);
            var before = GraphSourceSnapshot.Capture(source, policy, context);
            using var scope = new PredictionObjectScope();
            var copier = new PredictionGraphCopier(scope, policy, context);
            var callback = ReferenceEquals(selected, first) ? source.FirstRead : source.SecondRead;
            Func<int>? earlyCallback = callbackFirst ? copier.Copy(callback) : null;

            var clone = copier.Copy(source);

            ObjectGraphAssert.Equivalent(source, clone, scope, policy, context);
            var copiedSelected = scope.GetClone(selected);
            var copiedCallback = ReferenceEquals(selected, first) ? clone.FirstRead : clone.SecondRead;
            var shared = ReferenceEquals(selected, first) ? second : first;
            var sharedCallback = ReferenceEquals(selected, first) ? source.SecondRead : source.FirstRead;
            Assert.NotSame(selected, copiedSelected);
            Assert.Same(copiedSelected, copiedSelected.Next);
            Assert.Same(copiedSelected, copiedCallback.Target);
            Assert.Same(copiedCallback, copier.Copy(callback));
            if (callbackFirst) Assert.Same(earlyCallback, copiedCallback);
            Assert.False(scope.TryGetClone(shared, out _));
            Assert.Same(sharedCallback, copier.Copy(sharedCallback));
            Assert.Same(clone.First, clone.Slot.Value);
            Assert.Same(clone.First, clone.Array[0]);
            Assert.Same(clone.Second, clone.Array[1]);
            Assert.Same(clone.First, clone.Array[2]);
            Assert.Same(clone.Second, clone.List[0]);
            Assert.Same(clone.First, clone.List[1]);
            Assert.Same(clone.Second, clone.List[2]);

            copiedSelected.Amount = 99;
            Assert.Equal(99, copiedCallback());
            before.AssertUnchanged();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrWrongContextFailsCaptureAndInvalidatesScope(bool wrongType)
    {
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, CreatePolicy(), context: wrongType ? new object() : null);

        Assert.Throws<InvalidOperationException>(() => copier.Copy(new Node()));
        Assert.Throws<ObjectDisposedException>(() => copier.Copy(new Node()));
    }

    private static GraphCopyPolicy CreatePolicy() => new(
        referenceRules:
        [
            new("test.selected-node", type => type == typeof(Node),
                GraphReferenceStrategy.Choose(selection =>
                    ReferenceEquals(selection.GetSource<Node>(), selection.GetContext<SelectionContext>().Source)
                        ? GraphReferenceAction.Clone : GraphReferenceAction.Share))
        ],
        referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);

    private sealed record SelectionContext(Node Source);
    private sealed class Node
    {
        public int Amount;
        public Node? Next;
        public int Read() => Amount;
    }

    private readonly record struct Slot(Node Value);
    private sealed class Container
    {
        public required Node First;
        public required Node Second;
        public Slot Slot;
        public required Node[] Array;
        public required List<Node> List;
        public required Func<int> FirstRead;
        public required Func<int> SecondRead;
    }
}
