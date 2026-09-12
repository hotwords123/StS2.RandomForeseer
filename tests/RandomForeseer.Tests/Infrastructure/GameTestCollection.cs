namespace RandomForeseer.Tests.Infrastructure;

/// <summary>
/// Serializes tests that use process-global game state and prevents them from overlapping other collections.
/// Pure tests retain xUnit's default parallelization.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GameTestCollection
{
    public const string Name = "Game runtime";
}
