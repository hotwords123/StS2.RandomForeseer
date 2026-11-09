using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.Tests.ObjectGraph.Combat;

/// <summary>Combines the opt-in combat model rule with the staged NullRunState boundary; events use the common Reset rule.</summary>
internal static class CombatGraphFixturePolicy
{
    private static readonly GraphReferenceRule SkipNullRunState = new(
        "fixture.null-run-state",
        type => type == typeof(NullRunState) ? GraphReferenceStrategy.Share : null);

    public static GraphCopyPolicy Create() => new(
        referenceRules: [SkipNullRunState, CommonGraphRules.Models],
        referenceFallback: GraphReferenceStrategy.Clone,
        valueFallback: GraphValueAction.Fields);
}
