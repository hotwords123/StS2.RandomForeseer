using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class PotionTargetPredictionController
{
    private static ActivePotionTargeting? _activeTargeting;

    private static long _nextSessionId;

    public static long Begin(NPotionHolder holder, TargetType targetType)
    {
        Clear();

        if (targetType != TargetType.AnyPlayer ||
            holder.Potion?.Model is not { } source ||
            source.TargetType != TargetType.AnyPlayer)
        {
            return 0;
        }

        var targetManager = NTargetManager.Instance;
        var activeTargeting = new ActivePotionTargeting(++_nextSessionId, holder, source, targetManager);
        _activeTargeting = activeTargeting;
        NHoverTipSet.Remove(holder);

        targetManager.CreatureHovered += OnCreatureHovered;
        targetManager.CreatureUnhovered += OnCreatureUnhovered;
        targetManager.TargetingEnded += OnTargetingEnded;
        return activeTargeting.SessionId;
    }

    public static async Task CleanupAfterCompletion(NPotionHolder holder, long sessionId, Task targetingTask)
    {
        try
        {
            await targetingTask;
        }
        finally
        {
            if (_activeTargeting is { } activeTargeting &&
                activeTargeting.SessionId == sessionId &&
                ReferenceEquals(activeTargeting.Holder, holder))
            {
                Clear();
            }
        }
    }

    private static void OnCreatureHovered(NCreature creature)
    {
        if (_activeTargeting is not { } activeTargeting ||
            creature.Entity.Player is not { } target ||
            target.Creature.IsDead)
        {
            return;
        }

        activeTargeting.Target = creature;
        ShowHoverTips(activeTargeting, target);
    }

    private static void OnCreatureUnhovered(NCreature creature)
    {
        if (_activeTargeting is not { Target: { } target } activeTargeting ||
            !ReferenceEquals(target, creature))
        {
            return;
        }

        activeTargeting.Target = null;
        NHoverTipSet.Remove(activeTargeting.Holder);
    }

    private static void OnTargetingEnded()
    {
        Clear();
    }

    private static void ShowHoverTips(ActivePotionTargeting activeTargeting, Player target)
    {
        IReadOnlyList<IHoverTip> hoverTips;
        try
        {
            hoverTips = PotionPrediction.GetHoverTips(activeTargeting.Source, target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn(
                $"Potion target prediction failed for {activeTargeting.Source.Id} targeting {target.NetId}: {ex}");
            hoverTips = [];
        }

        NHoverTipSet.Remove(activeTargeting.Holder);
        if (hoverTips.Count == 0)
        {
            return;
        }

        // NTargetManager blocks ordinary hover tips while targeting. This is an explicit
        // target-specific prediction surface, so restore the global flag after showing it.
        var shouldBlockHoverTips = NHoverTipSet.shouldBlockHoverTips;
        NHoverTipSet.shouldBlockHoverTips = false;
        try
        {
            NHoverTipSet.CreateAndShow(activeTargeting.Holder, hoverTips, HoverTipAlignment.Center)
                ?.SetGlobalPosition(
                    activeTargeting.Holder.GlobalPosition +
                    Vector2.Down * activeTargeting.Holder.Size.Y * Mathf.Max(1.5f, activeTargeting.Holder.Scale.Y));
        }
        finally
        {
            NHoverTipSet.shouldBlockHoverTips = shouldBlockHoverTips;
        }
    }

    private static void Clear()
    {
        if (_activeTargeting is not { } activeTargeting)
        {
            return;
        }

        _activeTargeting = null;
        activeTargeting.TargetManager.CreatureHovered -= OnCreatureHovered;
        activeTargeting.TargetManager.CreatureUnhovered -= OnCreatureUnhovered;
        activeTargeting.TargetManager.TargetingEnded -= OnTargetingEnded;
        NHoverTipSet.Remove(activeTargeting.Holder);
    }

    private sealed class ActivePotionTargeting(
        long sessionId,
        NPotionHolder holder,
        PotionModel source,
        NTargetManager targetManager)
    {
        public long SessionId { get; } = sessionId;

        public NPotionHolder Holder { get; } = holder;

        public PotionModel Source { get; } = source;

        public NTargetManager TargetManager { get; } = targetManager;

        public NCreature? Target { get; set; }
    }
}
