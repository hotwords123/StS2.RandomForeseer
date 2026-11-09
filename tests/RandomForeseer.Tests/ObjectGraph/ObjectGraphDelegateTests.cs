using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects callback identity and captured-state isolation in <see cref="PredictionGraphCopier"/>.</summary>
/// <remarks>
/// Exercises normal lambdas, instance methods, multicast order and boxed receivers. Invoking copied callbacks
/// must affect copied targets, including closure cycles and targets also reachable through ordinary graph edges.
/// </remarks>
public sealed class ObjectGraphDelegateTests
{
    [Fact]
    public void StatelessAndImmutableTargetsShareTheirDelegates()
    {
        var receiver = new ImmutableReceiver(7);
        Func<int> read = receiver.Read;
        Func<int> combined = read + read;
        Delegate[] callbacks = [(Func<int, int>)(static value => value + 1),
            (Func<int, int>)(value => value + 1), (Action)NoOp, read, combined];
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, new GraphCopyPolicy());

        foreach (var callback in callbacks)
        {
            Assert.Same(callback, copier.Copy(callback));
            Assert.False(scope.TryGetClone(callback, out _));
        }
    }

    [Fact]
    public void ClosureCyclesAndRepeatedCallbacksUseTheCopiedState()
    {
        var receiver = new MutableReceiver { Number = 3 };
        Action callback = () => receiver.Number++;
        receiver.Callback = callback;
        object[] source = [callback, callback, receiver];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        var clone = copier.Copy(source);
        var copiedCallback = (Action)clone[0];
        var copiedReceiver = (MutableReceiver)clone[2];
        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Same(copiedCallback, clone[1]);
        Assert.Same(copiedCallback, copiedReceiver.Callback);
        Assert.Same(copiedCallback, copier.Copy(callback));
        Assert.Same(callback, scope.GetSource(copiedCallback));
        Assert.NotSame(callback.Target, copiedCallback.Target);

        copiedCallback();
        Assert.Equal(4, copiedReceiver.Number);
        Assert.Equal(3, receiver.Number);
        before.AssertUnchanged();
    }

    [Fact]
    public void MulticastCopiesInstanceTargetsAndPreservesInvocationOrderAndAliases()
    {
        var receiver = new MutableReceiver();
        Action first = receiver.AppendOne;
        Action second = receiver.AppendTwo;
        Action shared = NoOp;
        Action callback = first + first + shared + second;
        receiver.Callback = callback;
        object[] source = [callback, first, second, receiver];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);

        var clone = copier.Copy(source);
        var copiedCallback = (Action)clone[0];
        var copiedReceiver = (MutableReceiver)clone[3];
        var entries = copiedCallback.GetInvocationList();
        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Equal(4, entries.Length);
        Assert.Same(clone[1], entries[0]);
        Assert.Same(entries[0], entries[1]);
        Assert.Same(shared, entries[2]);
        Assert.Same(clone[2], entries[3]);
        Assert.Same(copiedCallback, copiedReceiver.Callback);
        Assert.Same(copiedReceiver, entries[0].Target);

        copiedCallback();
        Assert.Equal(112, copiedReceiver.Number);
        Assert.Equal(0, receiver.Number);
        before.AssertUnchanged();
    }

    [Fact]
    public void ExplicitReceiverRulesOverrideImmutableTargetSharing()
    {
        var receiver = new ImmutableReceiver(7);
        Func<int> callback = receiver.Read;
        foreach (var reset in new[] { false, true, false })
        {
            var policy = new GraphCopyPolicy(
                referenceRules: [new("test.receiver", type => reset && type == typeof(ImmutableReceiver)
                    ? GraphReferenceStrategy.Clone : null)],
                fieldRules: [new("test.reset", field => reset && field.DeclaringType == typeof(ImmutableReceiver)
                    ? GraphFieldAction.Reset : null)]);
            using var scope = new PredictionObjectScope();
            var copy = new PredictionGraphCopier(scope, policy).Copy(callback);

            Assert.Equal(reset ? 0 : 7, copy());
            if (reset)
            {
                Assert.NotSame(callback, copy);
                Assert.Same(scope.GetClone(receiver), copy.Target);
            }
            else
            {
                Assert.Same(callback, copy);
            }

            Assert.Equal(7, callback());
        }
    }

    [Fact]
    public void BoxedMethodReceiverKeepsItsMappedIdentity()
    {
        var value = new MutableValue();
        Action callback = value.Increment;
        object[] source = [callback, callback.Target!];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();

        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var copiedCallback = (Action)clone[0];
        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Same(clone[1], copiedCallback.Target);
        copiedCallback();
        Assert.Equal(1, ((MutableValue)clone[1]).Number);
        Assert.Equal(0, ((MutableValue)source[1]).Number);
        before.AssertUnchanged();
    }

    private static void NoOp() { }
    private sealed class ImmutableReceiver(int number)
    {
        public readonly int Number = number;
        public int Read() => Number;
    }

    private sealed class MutableReceiver
    {
        public int Number;
        public Action? Callback;
        public void AppendOne() => Number = Number * 10 + 1;
        public void AppendTwo() => Number = Number * 10 + 2;
    }

    private struct MutableValue
    {
        public int Number;
        public void Increment() => Number++;
    }
}
