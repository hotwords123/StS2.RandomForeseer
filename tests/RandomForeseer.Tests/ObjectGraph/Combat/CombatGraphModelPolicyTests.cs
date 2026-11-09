using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.ObjectGraph.Policies;
using RandomForeseer.Tests.Infrastructure;
using RandomForeseer.ObjectGraph;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

#pragma warning disable xUnit2017 // Exercise hash lookup directly; enumeration assertions can miss broken buckets.

namespace RandomForeseer.Tests.ObjectGraph.Combat;

/// <summary>Checks opt-in <see cref="CommonGraphRules.Models"/> dispatch using real game models.</summary>
/// <remarks>Uses the isolated model registry to exercise hash lookups with canonical and mutable model keys.</remarks>
[Collection(GameTestCollection.Name)]
public sealed class CombatGraphModelPolicyTests : GameTestBase
{
    [Fact]
    public void ModelKeysPreserveCanonicalSharingAndMutableIdentity()
    {
        ModelDb.Inject(typeof(StrikeIronclad));
        var canonical = ModelDb.Card<StrikeIronclad>();
        var mutable = canonical.ToMutable();
        var dictionary = new Dictionary<AbstractModel, object> { [canonical] = mutable, [mutable] = canonical };
        var set = new HashSet<AbstractModel> { canonical, mutable };
        object[] source = [dictionary, set];
        var policy = new GraphCopyPolicy(referenceRules: [CommonGraphRules.Models],
            referenceFallback: GraphReferenceStrategy.Clone, valueFallback: GraphValueAction.Fields);
        var before = GraphSourceSnapshot.Capture(source, policy);
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var copiedMutable = scope.GetClone(mutable);
        var copiedDictionary = scope.GetClone(dictionary);
        var copiedSet = scope.GetClone(set);

        var boundaries = ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Contains(boundaries, boundary => boundary.RuleId == "reference.model");
        Assert.False(scope.TryGetClone(canonical, out _));
        Assert.NotSame(mutable, copiedMutable);
        Assert.Same(copiedMutable, copiedDictionary[canonical]);
        Assert.Same(canonical, copiedDictionary[copiedMutable]);
        Assert.True(copiedSet.Contains(canonical));
        Assert.True(copiedSet.Contains(copiedMutable));
        Assert.False(copiedSet.Contains(mutable));
        Assert.False(copiedDictionary.ContainsKey(mutable));
        Assert.True(copiedDictionary.Remove(copiedMutable));
        before.AssertUnchanged();
    }
}
