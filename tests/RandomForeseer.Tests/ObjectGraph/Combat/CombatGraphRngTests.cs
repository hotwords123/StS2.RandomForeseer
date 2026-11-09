using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.InCombat.ObjectGraph;
using RandomForeseer.Tests.Infrastructure;
using RandomForeseer.ObjectGraph;
using RandomForeseer.Tests.ObjectGraph.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph.Combat;

/// <summary>Verifies RNG state, sequence and identity isolation through <see cref="CombatGraphPolicy"/>.</summary>
/// <remarks>
/// Uses real Rng/MegaRandom and initialized player/run stream sets. Serialization supplies an independent
/// sequence oracle only; graph capture uses ordinary fields. Does not consume Rng.Chaotic or global mock streams.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class CombatGraphRngTests : GameTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RngAndMegaRandomPreserveCurrentStateAndAliases(bool engineFirst)
    {
        var rng = new Rng(271828);
        _ = Draw(rng);
        // Direct engine draws make state impossible to reconstruct from the wrapper counter alone.
        rng._random.NextBytes(new byte[19]);
        object[] source = engineFirst ? [rng._random, rng, rng] : [rng, rng._random, rng];
        var policy = CombatGraphPolicy.Instance;
        var before = GraphSourceSnapshot.Capture(source, policy);
        var saved = rng.ToSerializable();
        var expected = new Rng(saved);
        using var scope = new PredictionObjectScope();

        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        var copiedRng = scope.GetClone(rng);
        var copiedEngine = scope.GetClone(rng._random);

        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        Assert.Same(copiedEngine, copiedRng._random);
        Assert.Equal(saved, copiedRng.ToSerializable());
        before.AssertUnchanged();

        for (var index = 0; index < 32; index++)
        {
            Assert.Equal(Draw(expected), Draw(copiedRng));
            var expectedBytes = new byte[19];
            var actualBytes = new byte[19];
            expected._random.NextBytes(expectedBytes);
            copiedEngine.NextBytes(actualBytes);
            Assert.Equal(expectedBytes, actualBytes);
            Assert.Equal(expected._random.NextBool(), copiedEngine.NextBool());
            Assert.Equal(expected._random.NextFloat(), copiedEngine.NextFloat());
        }

        Assert.Equal(expected.ToSerializable(), copiedRng.ToSerializable());
        Assert.Equal(saved, rng.ToSerializable());
        before.AssertUnchanged();
    }

    [Fact]
    public void PlayerRngSetStreamsAdvanceIndependently()
    {
        VerifyIndependentStreams(new PlayerRngSet(314159), Enum.GetValues<PlayerRngType>(),
            (set, key) => set.GetRng(key));
    }

    [Fact]
    public void RunRngSetStreamsAdvanceIndependently()
    {
        VerifyIndependentStreams(new RunRngSet("graph-b5"), Enum.GetValues<RunRngType>(),
            (set, key) => set.GetRng(key));
    }

    [Fact]
    public void SharedRunStreamsRemainOneRngAcrossGraphRoots()
    {
        var rng = new Rng(161803);
        _ = Draw(rng);
        // The game's Bestiary constructor deliberately assigns one Rng to every stream.
        var source = new RunRngSet(rng);
        var policy = CombatGraphPolicy.Instance;
        var before = GraphSourceSnapshot.Capture(source, policy);
        var expected = new Rng(rng.ToSerializable());
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, policy);
        var copiedRng = copier.Copy(rng);
        var clone = copier.Copy(source);

        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        foreach (var key in Enum.GetValues<RunRngType>())
        {
            Assert.Same(copiedRng, clone.GetRng(key));
            Assert.Equal(Draw(expected), Draw(clone.GetRng(key)));
        }

        Assert.Equal(expected.ToSerializable(), copiedRng.ToSerializable());
        before.AssertUnchanged();
    }

    private static void VerifyIndependentStreams<TSet, TKey>(TSet source, TKey[] keys,
        Func<TSet, TKey, Rng> getRng) where TSet : class
    {
        for (var index = 0; index < keys.Length; index++)
        {
            for (var draw = 0; draw <= index; draw++)
            {
                _ = Draw(getRng(source, keys[index]));
            }
        }

        var policy = CombatGraphPolicy.Instance;
        var before = GraphSourceSnapshot.Capture(source, policy);
        var saved = keys.Select(key => getRng(source, key).ToSerializable()).ToArray();
        using var scope = new PredictionObjectScope();
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        ObjectGraphAssert.Equivalent(source, clone, scope, policy);
        before.AssertUnchanged();

        for (var index = 0; index < keys.Length; index++)
        {
            var copiedRng = getRng(clone, keys[index]);
            Assert.Same(scope.GetClone(getRng(source, keys[index])), copiedRng);
            var otherStreams = keys.Where((_, slot) => slot != index).Select(key => getRng(clone, key)).ToArray();
            var othersBefore = GraphSourceSnapshot.Capture(otherStreams, policy);
            var expected = new Rng(saved[index]);
            for (var draw = 0; draw < 16; draw++)
            {
                Assert.Equal(Draw(expected), Draw(copiedRng));
            }

            Assert.Equal(expected.ToSerializable(), copiedRng.ToSerializable());
            othersBefore.AssertUnchanged();
            before.AssertUnchanged();
        }
    }

    private static (int Integer, ulong Unsigned, bool Boolean, float Single, double Double, double Gaussian) Draw(Rng rng) =>
        (rng.NextInt(-1000, 1000), rng.NextUnsignedLong(), rng.NextBool(), rng.NextFloat(-5, 5),
            rng.NextDouble(), rng.NextGaussianDouble(0.5, 0.2));
}
