using HarmonyLib;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.Compatibility;

/// <summary>
/// Verifies that the installed game accepts the same uncategorized Harmony patches as <see cref="Entry.Initialize"/>.
/// This catches changed runtime-only targets, injected arguments and transpiler sites without launching Godot.
/// Optional integration categories and actual UI behavior are outside this check.
/// </summary>
[Collection(GameTestCollection.Name)]
public sealed class GamePatchCompatibilityTests : GameTestBase
{
    [Fact]
    public void GameAcceptsCorePatchTargetsAndTranspilers()
    {
        var harmony = new Harmony($"RandomForeseer.Tests.Compatibility.{Guid.NewGuid():N}");
        try
        {
            harmony.PatchAllUncategorized(typeof(Entry).Assembly);
            Assert.NotEmpty(harmony.GetPatchedMethods());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
