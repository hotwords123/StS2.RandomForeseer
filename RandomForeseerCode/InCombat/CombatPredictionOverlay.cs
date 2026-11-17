using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Nodes;

namespace RandomForeseer.InCombat;

internal static class CombatPredictionOverlay
{
    private const float IntentGap = 6f;
    private const float OrbAvoidanceGap = 8f;

    private static readonly Dictionary<Creature, NCombatPredictionDamageIndicator> Indicators = [];

    public static IReadOnlyList<Control> ActiveIndicators =>
        [.. Indicators.Values.Where(static indicator => indicator.IsVisibleInTree())];

    /// <summary>
    /// Shows a damage projection, including its corresponding risk flag.
    /// </summary>
    public static void Show(
        DamagePrediction prediction,
        PredictionRisk risk,
        Func<Creature, IEnumerable<IHoverTip>>? getHoverTips = null)
    {
        var staleTargets = Indicators.Keys
            .Except(prediction.Targets.Select(static target => target.Target))
            .ToList();
        foreach (var target in staleTargets)
        {
            Indicators.Remove(target, out var indicator);
            indicator?.QueueFreeSafely();
        }

        foreach (var target in prediction.Targets)
        {
            if (!Indicators.TryGetValue(target.Target, out var indicator))
            {
                indicator = CreateIndicator(target.Target);
                if (indicator == null)
                {
                    continue;
                }
            }

            indicator.SetPrediction(target, risk.HasVisibleRisk);
            indicator.SetHoverTips(getHoverTips?.Invoke(target.Target) ?? []);
        }

        RefreshPositions();
    }

    public static void Clear()
    {
        foreach (var indicator in Indicators.Values)
        {
            indicator.QueueFreeSafely();
        }
        Indicators.Clear();
    }

    public static void RefreshPositions()
    {
        if (NCombatRoom.Instance is not { } combatRoom)
        {
            Clear();
            return;
        }

        foreach (var (target, indicator) in Indicators.ToList())
        {
            var creatureNode = combatRoom.GetCreatureNode(target);
            if (creatureNode == null)
            {
                indicator.QueueFreeSafely();
                RemoveIndicator(target, indicator);
                continue;
            }

            var indicatorSize = indicator.GetGlobalRect().Size;
            indicator.GlobalPosition = GetIndicatorPosition(creatureNode, indicatorSize);
            indicator.Modulate = creatureNode.Visuals.Modulate with { A = indicator.Modulate.A };
        }
    }

    private static NCombatPredictionDamageIndicator? CreateIndicator(Creature target)
    {
        var parent = NCombatRoom.Instance?.GetCreatureNode(target)?.GetParent();
        if (parent == null)
        {
            return null;
        }

        var indicator = new NCombatPredictionDamageIndicator(target);
        parent.AddChildSafely(indicator);

        Indicators[target] = indicator;
        return indicator;
    }

    private static void RemoveIndicator(Creature target, NCombatPredictionDamageIndicator indicator)
    {
        if (Indicators.TryGetValue(target, out var existing) && ReferenceEquals(existing, indicator))
        {
            Indicators.Remove(target);
        }
    }

    private static Vector2 GetIndicatorPosition(NCreature creatureNode, Vector2 indicatorSize)
    {
        var intentRect = creatureNode.IntentContainer.GetGlobalRect();
        var position = new Vector2(
            intentRect.GetCenter().X - indicatorSize.X / 2f,
            intentRect.Position.Y - indicatorSize.Y - IntentGap);

        if (creatureNode.OrbManager is { } orbManager)
        {
            var candidateRect = new Rect2(position, indicatorSize);
            var orbNodes = orbManager.GetNode<Control>("%Orbs").GetChildren().OfType<NOrb>();

            foreach (var orb in orbNodes)
            {
                var orbRect = orb.GetNode<Control>("%SelectionReticle").GetGlobalRect().Grow(OrbAvoidanceGap);
                if (candidateRect.Intersects(orbRect))
                {
                    position.Y = Math.Min(position.Y, orbRect.Position.Y - indicatorSize.Y);
                }
            }
        }

        return position;
    }
}
