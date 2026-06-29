using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Combat.HealthBars;

namespace RandomForeseer.InCombat;

internal sealed class CombatPredictionHealthBarForecastSource : IHealthBarForecastSource
{
    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        return CombatPredictionHealthBarForecast.GetSegments(context);
    }
}

internal static class CombatPredictionHealthBarForecast
{
    private static readonly Color ForecastColor = new("E8C91A");
    private static readonly Dictionary<Creature, int> DamageByTarget = [];

    public static IEnumerable<HealthBarForecastSegment> GetSegments(HealthBarForecastContext context)
    {
        return DamageByTarget.TryGetValue(context.Creature, out var amount) && amount > 0
            ? [new HealthBarForecastSegment(
                amount,
                ForecastColor,
                HealthBarForecastGrowthDirection.FromRight,
                HealthBarForecastOrder.ForSideTurnEnd(context.Creature, CombatSide.Player))]
            : [];
    }

    public static void Set(DamagePredictionResult prediction)
    {
        var staleTargets = DamageByTarget.Keys.ToArray();
        DamageByTarget.Clear();

        foreach (var target in prediction.Targets)
        {
            var damage = (int)target.TotalUnblockedDamage;
            if (damage > 0)
            {
                DamageByTarget[target.Target] = damage;
            }
        }

        RefreshHealthBars(staleTargets.Concat(DamageByTarget.Keys));
    }

    public static void Clear()
    {
        if (DamageByTarget.Count == 0)
        {
            return;
        }

        var staleTargets = DamageByTarget.Keys.ToList();
        DamageByTarget.Clear();
        RefreshHealthBars(staleTargets);
    }

    private static void RefreshHealthBars(IEnumerable<Creature> targets)
    {
        if (NCombatRoom.Instance == null)
        {
            return;
        }

        foreach (var target in targets.Distinct())
        {
            NCombatRoom.Instance
                .GetCreatureNode(target)
                ?.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar")
                ?.Call(NCreatureStateDisplay.MethodName.RefreshValues);
        }
    }
}
