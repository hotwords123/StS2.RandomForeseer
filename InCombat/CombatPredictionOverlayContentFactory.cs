using MegaCrit.Sts2.Core.HoverTips;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

internal static class CombatPredictionOverlayContentFactory
{
    public static CombatPredictionOverlayContent FromDamageHistory(
        CombatPredictionSimulator simulator,
        IReadOnlyList<IHoverTip> hoverTips)
    {
        var targets = simulator.DamageHistory
            .GroupBy(static entry => entry.Receiver)
            .Select(group => new CombatPredictionTargetOverlay(
                group.Key,
                group
                    .Select(static entry => new CombatPredictionDamageLine(
                        entry.Result.TotalDamage,
                        entry.Result.UnblockedDamage,
                        entry.SourceModel))
                    .ToList(),
                simulator.State.GetCreature(group.Key).IsDead))
            .ToList();

        return targets.Count > 0
            ? new CombatPredictionOverlayContent(targets, hoverTips)
            : CombatPredictionOverlayContent.Empty;
    }
}
