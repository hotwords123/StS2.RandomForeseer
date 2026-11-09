using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using RandomForeseer.Data;
using STS2RitsuLib.Combat.HealthBars;

namespace RandomForeseer.InCombat;

internal sealed class DamagePredictionHealthBarForecastSource : IHealthBarForecastSource
{
    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        return DamagePredictionHealthBarForecast.GetSegments(context);
    }
}

internal static class DamagePredictionHealthBarForecast
{
    private static readonly Dictionary<Creature, int> DamageByTarget = [];

    public static IEnumerable<HealthBarForecastSegment> GetSegments(HealthBarForecastContext context)
    {
        return DamageByTarget.TryGetValue(context.Creature, out var amount) && amount > 0
            ? [new HealthBarForecastSegment(
                amount,
                ModData.Settings.DamagePredictionHealthBarColorValue,
                HealthBarForecastGrowthDirection.FromRight,
                HealthBarForecastOrder.ForSideTurnEnd(context.Creature, CombatSide.Player))]
            : [];
    }

    /// <summary>
    /// Applies a damage projection to the shared combat-action/end-turn forecast surface.
    /// </summary>
    public static void Set(DamagePrediction prediction)
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

    public static void RefreshActiveForecasts()
    {
        RefreshHealthBars(DamageByTarget.Keys);
    }

    private static void RefreshHealthBars(IEnumerable<Creature> targets)
    {
        HashSet<Creature> targetsSet = [.. targets];

        foreach (var creatureNode in NCombatRoom.Instance?.CreatureNodes ?? [])
        {
            if (targetsSet.Contains(creatureNode.Entity))
            {
                creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar")
                    ?.GetNodeOrNull<NHealthBar>("%HealthBar")
                    ?.RefreshValues();
            }
        }

        foreach (var childNode in NRun.Instance?.GlobalUi?.MultiplayerPlayerContainer?.GetChildren() ?? [])
        {
            if (childNode is NMultiplayerPlayerState playerStateNode &&
                targetsSet.Contains(playerStateNode.Player.Creature))
            {
                playerStateNode.GetNodeOrNull<NHealthBar>("%HealthBar")?.RefreshValues();
            }
        }
    }
}
