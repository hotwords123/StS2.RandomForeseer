using System.Collections;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

#pragma warning disable xUnit2017 // Exercise hash lookup directly; enumeration assertions can miss broken buckets.

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects query semantics, cycles and element aliases of the collections admitted by <see cref="GraphCopyPolicy"/>.</summary>
/// <remarks>
/// Uses real .NET collections, removed entries, identity keys and immutable value keys with ordinary equality.
/// Rejected nested elements must report complete field/index/key/value paths through <see cref="PredictionGraphCopier"/>,
/// including after successful DFS siblings and during key validation outside recursive child copying.
/// Bulk array/list copies must honor policy-local field rules, nullable presence and per-instance sharing.
/// </remarks>
public sealed class ObjectGraphCollectionTests
{
    [Fact]
    public void ListPreservesElementAliasesAndCycles()
    {
        var list = new List<object>(9);
        var item = new Node();
        list.Add(item);
        list.Add(list);
        object[] source = [list, item];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();

        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var copied = (List<object>)clone[0];
        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Same(copied, copied[1]);
        Assert.Same(clone[1], copied[0]);
        copied.RemoveAt(0);
        copied.Add(new Node());
        before.AssertUnchanged();
        Assert.Same(item, list[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DictionaryPreservesLookupElementAliasesAndCycles(int comparerKind)
    {
        IEqualityComparer<string> comparer = comparerKind switch
        {
            1 => StringComparer.Ordinal,
            2 => StringComparer.OrdinalIgnoreCase,
            _ => EqualityComparer<string>.Default
        };
        var dictionary = new Dictionary<string, Node>(11, comparer);
        var node = new Node { Owner = dictionary, Amount = 3 };
        dictionary.Add("Alpha", node);
        dictionary.Add("Removed", new Node());
        dictionary.Add("Beta", node);
        dictionary.Remove("Removed");
        object[] source = [dictionary, node];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();
        new PredictionGraphCopier(scope, policy).Copy(source);
        var copied = scope.GetClone(dictionary);

        ObjectGraphAssert.Equivalent(source, scope.GetClone(source), scope, policy);
        Assert.Equal(comparerKind == 2, copied.ContainsKey("ALPHA"));
        Assert.False(copied.ContainsKey("Removed"));
        Assert.Same(scope.GetClone(node), copied["Alpha"]);
        Assert.Same(copied, copied["Alpha"].Owner);
        Assert.Same(copied["Alpha"], copied["Beta"]);
        copied["Alpha"].Amount = 20;
        copied.Add("New", new Node());
        copied.Remove("Beta");
        before.AssertUnchanged();
    }

    [Fact]
    public void EnumDictionaryAndSetRemainQueryableAndIndependentAfterRemoval()
    {
        var set = new HashSet<Slot>(13) { Slot.First, Slot.Second, Slot.Third };
        set.Remove(Slot.Second);
        var dictionary = new Dictionary<Slot, object> { [Slot.First] = set };
        object[] source = [dictionary, set];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var copied = scope.GetClone(set);

        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Same(copied, scope.GetClone(dictionary)[Slot.First]);
        Assert.True(set.SetEquals(copied));
        Assert.True(copied.TryGetValue(Slot.First, out var found));
        Assert.Equal(Slot.First, found);
        Assert.False(copied.TryGetValue(Slot.Second, out _));
        Assert.True(copied.Add(Slot.Second));
        Assert.True(copied.Remove(Slot.First));
        scope.GetClone(dictionary).Add(Slot.Second, copied);
        before.AssertUnchanged();
    }

    [Fact]
    public void EmptyCollectionsAreIndependentAndUsable()
    {
        object[] source = [new Dictionary<string, object>(), new HashSet<Slot>(), new List<object>()];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        ((Dictionary<string, object>)clone[0]).Add("new", clone);
        Assert.True(((HashSet<Slot>)clone[1]).Add(Slot.First));
        ((List<object>)clone[2]).Add(clone);
        Assert.Same(clone, ((Dictionary<string, object>)clone[0])["new"]);
        Assert.Same(clone, Assert.Single((List<object>)clone[2]));
        Assert.Empty((Dictionary<string, object>)source[0]);
        Assert.Empty((HashSet<Slot>)source[1]);
        Assert.Empty((List<object>)source[2]);
    }

    [Theory]
    [InlineData("array", "$capture.Owner[12]")]
    [InlineData("list", "$capture.Owner[1][0]")]
    [InlineData("dictionary-value", "$capture.Owner[1].Value[1]")]
    [InlineData("dictionary-key", "$capture.Owner[1].Key")]
    [InlineData("set", "$capture.Owner[1]")]
    public void RejectedNestedElementsPreserveCompleteDiagnosticPaths(string kind, string expectedPath)
    {
        var rejected = Task.CompletedTask;
        var array = Enumerable.Repeat<object>("shared", 13).ToArray();
        array[0] = new object[] { array };
        array[12] = rejected;
        var source = new Node
        {
            Owner = kind switch
            {
                "array" => array,
                "list" => new List<object> { new object[] { "shared" }, new object[] { rejected } },
                "dictionary-value" => new Dictionary<string, object>
                    { ["earlier"] = new Node(), ["key"] = new List<object> { "shared", rejected } },
                "dictionary-key" => new Dictionary<object, object>(ReferenceEqualityComparer.Instance)
                    { [new Node()] = "earlier", [rejected] = "value" },
                "set" => new HashSet<object>(ReferenceEqualityComparer.Instance) { new Node(), rejected },
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            }
        };
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields));

        var error = Assert.Throws<NotSupportedException>(() => copier.Copy(source, "$capture"));

        Assert.StartsWith($"{expectedPath}: ", error.Message);
        Assert.Throws<ObjectDisposedException>(() => scope.GetClone(source));
    }

    [Fact]
    public void EnumerableWrapperPreservesBackingCollectionAndElementAliases()
    {
        var node = new Node { Amount = 3 };
        var wrapper = new EnumerableWrapper([node, node]);
        object[] source = [wrapper, wrapper.Items, node];
        using var scope = new PredictionObjectScope();

        var clone = new PredictionGraphCopier(scope, new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields)).Copy(source);
        var copied = (EnumerableWrapper)clone[0];

        Assert.NotSame(wrapper, copied);
        Assert.NotSame(wrapper.Items, copied.Items);
        Assert.Same(clone[1], copied.Items);
        Assert.Same(clone[2], copied.Items[0]);
        Assert.Same(copied.Items[0], copied.Items[1]);
        copied.Items[0].Amount = 9;
        copied.Items.Clear();
        Assert.Equal(3, node.Amount);
        Assert.Equal(2, wrapper.Items.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdentityKeysRemainQueryableAcrossBackReferences(bool explicitComparer)
    {
        IEqualityComparer<object> comparer = explicitComparer ? ReferenceEqualityComparer.Instance : EqualityComparer<object>.Default;
        var dictionary = new Dictionary<object, object>(comparer);
        var set = new HashSet<object>(comparer);
        var node = new Node { Owner = dictionary, Amount = 7 };
        dictionary.Add(node, set);
        dictionary.Add(dictionary, node);
        set.Add(node);
        set.Add(set);
        // Copying the node first reaches the dictionary while its key is still being populated.
        object[] source = [node, dictionary, set];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var copiedNode = scope.GetClone(node);
        var copiedDictionary = scope.GetClone(dictionary);
        var copiedSet = scope.GetClone(set);

        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Same(copiedSet, copiedDictionary[copiedNode]);
        Assert.Same(copiedNode, copiedDictionary[copiedDictionary]);
        Assert.True(copiedSet.Contains(copiedSet));
        Assert.True(copiedSet.Contains(copiedNode));
        Assert.False(copiedDictionary.ContainsKey(node));
        Assert.False(copiedSet.Contains(node));
        copiedNode.Amount = 12;
        Assert.True(copiedSet.Remove(copiedNode));
        Assert.True(copiedDictionary.Remove(copiedNode));
        before.AssertUnchanged();
    }

    [Fact]
    public void IdentityComparerKeepsMutableValueKeysQueryableAfterCopy()
    {
        var key = new ValueKey { Amount = 3 };
        var accepted = new HashSet<ValueKey>(ReferenceEqualityComparer.Instance) { key };
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields)).Copy(accepted);
        var copiedKey = scope.GetClone(key);
        copiedKey.Amount = 99;
        Assert.True(clone.Contains(copiedKey));
        Assert.False(clone.Contains(key));
    }

    [Fact]
    public void ImmutableModelIdsRemainQueryableWithDefaultPolicy()
    {
        var id = new ModelId("EVENT", "FIXTURE_EVENT");
        var source = new HashSet<ModelId> { id };
        using var accepted = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(accepted, new GraphCopyPolicy());
        var clone = copier.Copy(source);
        Assert.True(clone.TryGetValue(new ModelId(id.Category, id.Entry), out var found));
        Assert.Same(id, found);

        var dictionary = new Dictionary<ModelId, string> { [id] = "value" };
        var copiedDictionary = copier.Copy(dictionary);
        Assert.Equal("value", copiedDictionary[new ModelId(id.Category, id.Entry)]);
    }

    [Fact]
    public void ValueCollectionsRespectFieldRulesAcrossPolicies()
    {
        var value = new PlainValue { Number = 9 };
        PlainValue?[] array = [value, null, value];
        List<PlainValue?> list = [value, null, value];
        var matrix = Array.CreateInstance(typeof(PlainValue), [1, 2], [-1, 3]);
        matrix.SetValue(value, -1, 3);
        matrix.SetValue(value, -1, 4);

        foreach (var reset in new[] { false, true, false })
        {
            var policy = new GraphCopyPolicy(valueFallback: GraphValueAction.Fields,
                fieldRules: [new("test.reset", field => reset && field.DeclaringType == typeof(PlainValue) ? GraphFieldAction.Reset : null)]);
            using var scope = new PredictionObjectScope();
            var copier = new PredictionGraphCopier(scope, policy);
            var arrayCopy = copier.Copy(array);
            var listCopy = copier.Copy(list);
            var matrixCopy = copier.Copy(matrix);

            Assert.Equal(reset ? 0 : 9, arrayCopy[0]!.Value.Number);
            Assert.Equal(reset ? 0 : 9, listCopy[2]!.Value.Number);
            Assert.Null(arrayCopy[1]);
            Assert.Null(listCopy[1]);
            Assert.Equal(-1, matrixCopy.GetLowerBound(0));
            Assert.Equal(3, matrixCopy.GetLowerBound(1));
            Assert.Equal(reset ? 0 : 9, ((PlainValue)matrixCopy.GetValue(-1, 4)!).Number);
            Assert.Same(array, scope.GetSource(arrayCopy));
            Assert.Same(list, scope.GetSource(listCopy));
            arrayCopy[0] = null;
            listCopy.Clear();
            Assert.Equal(9, array[0]!.Value.Number);
            Assert.Equal(3, list.Count);
            Assert.Equal(9, ((PlainValue)matrix.GetValue(-1, 4)!).Number);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullableCollectionsRejectOnlyPresentElementsWithTheirIndex(bool list)
    {
        var policy = new GraphCopyPolicy(valueRules:
            [new("test.reject-int", type => type == typeof(int) ? GraphValueAction.Reject : null)]);
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);
        object empty = list ? new List<int?>() : Array.Empty<int?>();
        object absent = list ? new List<int?> { null, null } : new int?[] { null, null };
        object present = list ? new List<int?> { null, 7 } : new int?[] { null, 7 };

        Assert.NotSame(empty, copier.Copy(empty));
        Assert.NotSame(absent, copier.Copy(absent));
        var error = Assert.Throws<NotSupportedException>(() => copier.Copy(present, "$values"));
        Assert.StartsWith("$values[1].Value: ", error.Message);
        Assert.Throws<ObjectDisposedException>(() => scope.GetClone(absent));
    }

    [Fact]
    public void SealedReferenceCollectionsDistinguishFixedSharingFromInstanceChoices()
    {
        var shared = new Node { Amount = 1 };
        var mutable = new Node { Amount = 2 };
        Node[] array = [shared, mutable, mutable];
        List<Node> list = [shared, mutable, mutable];
        foreach (var fixedShare in new[] { true, false, true })
        {
            var strategy = fixedShare ? GraphReferenceStrategy.Share : GraphReferenceStrategy.Choose(
                selection => selection.GetSource<Node>().Amount == 1 ? GraphReferenceAction.Share : GraphReferenceAction.Clone);
            var policy = new GraphCopyPolicy(referenceRules:
                [new("test.nodes", type => type == typeof(Node) ? strategy : null)]);
            using var scope = new PredictionObjectScope();
            var copier = new PredictionGraphCopier(scope, policy);
            var arrayCopy = copier.Copy(array);
            var listCopy = copier.Copy(list);

            Assert.Same(shared, arrayCopy[0]);
            Assert.Same(shared, listCopy[0]);
            Assert.Same(arrayCopy[1], arrayCopy[2]);
            Assert.Same(arrayCopy[1], listCopy[1]);
            Assert.Same(listCopy[1], listCopy[2]);
            if (fixedShare)
            {
                Assert.Same(mutable, arrayCopy[1]);
                Assert.False(scope.TryGetClone(mutable, out _));
            }
            else
            {
                Assert.NotSame(mutable, arrayCopy[1]);
                Assert.Same(mutable, scope.GetSource(arrayCopy[1]));
            }
        }
    }

    private struct PlainValue { public int Number; }

    private sealed class ValueKey
    {
        public int Amount;
        public override bool Equals(object? other) => other is ValueKey key && key.Amount == Amount;
        public override int GetHashCode() => Amount;
    }

    private enum Slot { First, Second, Third }
    private sealed class Node { public object? Owner; public int Amount; }

    private sealed class EnumerableWrapper(List<Node> items) : IEnumerable<Node>
    {
        public readonly List<Node> Items = items;
        public IEnumerator<Node> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
