using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using RandomForeseer.RandomForeseerCode.InCombat.Nodes;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatPredictionOverlay
{
    private const float IntentGap = 6f;

    private static readonly Dictionary<Creature, NCombatPredictionDamageIndicator> Indicators = [];

    public static IReadOnlyList<Control> ActiveIndicators =>
        Indicators.Values.Where(static indicator => indicator.IsVisibleInTree()).ToList();

    public static void Show(
        DamagePredictionResult prediction,
        Func<Creature, IEnumerable<IHoverTip>>? getHoverTips = null)
    {
        var activeTargets = prediction.Targets.Select(static target => target.Target).ToHashSet();
        foreach (var (target, indicator) in Indicators.ToList())
        {
            if (!activeTargets.Contains(target))
            {
                indicator.QueueFreeSafely();
                Indicators.Remove(target);
            }
        }

        foreach (var target in prediction.Targets)
        {
            var indicator = GetOrCreateIndicator(target.Target);
            indicator?.SetPrediction(target, prediction.HasRisk);
            indicator?.SetHoverTips(getHoverTips?.Invoke(target.Target) ?? []);
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
        if (NCombatRoom.Instance == null)
        {
            Clear();
            return;
        }

        foreach (var (target, indicator) in Indicators.ToList())
        {
            var creatureNode = NCombatRoom.Instance.GetCreatureNode(target);
            if (creatureNode == null || !indicator.IsInsideTree())
            {
                indicator.QueueFreeSafely();
                Indicators.Remove(target);
                continue;
            }

            var intentRect = creatureNode.IntentContainer.GetGlobalRect();
            var indicatorSize = indicator.GetGlobalRect().Size;
            indicator.GlobalPosition = new Vector2(
                intentRect.GetCenter().X - indicatorSize.X / 2f,
                intentRect.Position.Y - indicatorSize.Y - IntentGap);
        }
    }

    private static NCombatPredictionDamageIndicator? GetOrCreateIndicator(Creature target)
    {
        if (Indicators.TryGetValue(target, out var existing) && existing.IsInsideTree())
        {
            return existing;
        }

        var parent = NCombatRoom.Instance?.GetCreatureNode(target)?.GetParent();
        if (parent == null)
        {
            return null;
        }

        var indicator = NCombatPredictionDamageIndicator.Create(target);
        parent.AddChildSafely(indicator);
        Indicators[target] = indicator;
        return indicator;
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.RemoveCreatureNode))]
internal static class CombatPredictionOverlayRefreshOnCreatureRemovedPatch
{
    private static void Postfix()
    {
        CombatPredictionOverlay.RefreshPositions();
    }
}
