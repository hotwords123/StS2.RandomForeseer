using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.Tests.Common;

/// <summary>Checks value capture and shared references in <see cref="PredictionCloner.ShallowClone{T}"/>.</summary>
/// <remarks>Pure CLR tests; game-model isolation is covered by <see cref="PredictionModelClonerTests"/>.</remarks>
public sealed class PredictionClonerTests
{
    [Fact]
    public void CopiesRuntimeTypeWithoutRunningConstructorOrGetters()
    {
        var constructions = 0;
        BaseState source = new DerivedState(() => constructions++, 17) { Amount = 3 };

        var copy = Assert.IsType<DerivedState>(PredictionCloner.ShallowClone(source));

        Assert.NotSame(source, copy);
        Assert.Equal(1, constructions);
        Assert.Equal(17, copy.InitialValue);
        Assert.Equal(3, copy.Amount);
    }

    [Fact]
    public void CopiesValuesButSharesReferencedState()
    {
        var source = new DerivedState(() => { }, 0) { Amount = 3 };
        source.Link = source;

        var copy = PredictionCloner.ShallowClone(source);
        source.Amount = 8;
        source.Values.Add(1);

        Assert.Equal(3, copy.Amount);
        Assert.Same(source.Values, copy.Values);
        Assert.Equal([1], copy.Values);
        Assert.Same(source, copy.Link);
    }

    private abstract class BaseState(int initialValue)
    {
        private readonly int _initialValue = initialValue;
        public int InitialValue => _initialValue;
        public int Amount { get; set; }
        public int UnsafeGetter => throw new InvalidOperationException("Cloning must not evaluate properties.");
    }

    private sealed class DerivedState : BaseState
    {
        public readonly List<int> Values = [];
        public object? Link;

        public DerivedState(Action onConstructed, int initialValue) : base(initialValue) => onConstructed();
    }
}
