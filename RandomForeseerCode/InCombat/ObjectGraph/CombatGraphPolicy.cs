using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.InCombat.ObjectGraph;

/// <summary>Shares canonical/mutable model dispatch and ordinary managed field copying over the common rules.</summary>
/// <remarks>
/// Reuses type plans across synchronous, single-threaded captures. Capture alone does not authorize
/// running original gameplay methods.
/// </remarks>
internal static class CombatGraphPolicy
{
    public static GraphCopyPolicy Instance { get; } = new(
        referenceRules: [CommonGraphRules.Models],
        referenceFallback: GraphReferenceStrategy.Clone,
        valueFallback: GraphValueAction.Fields);
}
