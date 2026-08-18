using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>Owns combat potion hover, use, targeting, and presentation sessions.</summary>
internal static class CombatPotionPredictionController
{
    private static PotionPredictionSession? _session;

    public static void OnPotionFocus(NPotionHolder holder)
    {
        BeginSession(CombatPredictionSessionMode.Hover, holder);
    }

    public static void OnPotionUnfocus(NPotionHolder holder)
    {
        EndSession(CombatPredictionSessionMode.Hover, holder);
    }

    public static void OnPotionRemoved(NPotionHolder holder)
    {
        if (_session is { } session && ReferenceEquals(session.Holder, holder))
        {
            ClearHolderHoverTips(holder);
            _session = null;
            session.Dispose();
        }
    }

    public static void OnPotionPopupOpen(NPotionHolder holder)
    {
        BeginSession(CombatPredictionSessionMode.Action, holder);
    }

    public static void OnPotionTargetingStart(NPotionHolder holder)
    {
        if (_session is { Mode: CombatPredictionSessionMode.Action, IsTargeting: false } session &&
            ReferenceEquals(session.Holder, holder))
        {
            session.BeginTargeting(NTargetManager.Instance);
        }
    }

    public static void OnPotionPopupClose(NPotionHolder holder)
    {
        if (_session is not { IsTargeting: true })
        {
            EndSession(CombatPredictionSessionMode.Action, holder);
        }
    }

    /// <summary>Returns the cached result for the active potion interaction without running a second simulation.</summary>
    /// <returns>
    /// <see langword="true"/> when the current hover or action session owns this exact potion, including when its projection is empty.
    /// Callers must not run a fallback simulation in that case.
    /// </returns>
    public static bool TryGetActiveHoverTips(PotionModel potion, out IReadOnlyList<IHoverTip> hoverTips)
    {
        if (_session is { } session && session.Source == potion)
        {
            hoverTips = session.Projection?.HoverTips ?? [];
            return true;
        }

        hoverTips = [];
        return false;
    }

    private static void OnProjectionChanged(PotionPredictionSession session)
    {
        if (session == _session && session.IsTargeting)
        {
            ShowTargetingHoverTips(session);
        }
    }

    private static void OnTargetingFinishing(PotionPredictionSession session)
    {
        if (_session == session)
        {
            ClearHolderHoverTips(session.Holder);
            _session = null;
            session.Dispose();
        }
    }

    private static void BeginSession(CombatPredictionSessionMode mode, NPotionHolder holder)
    {
        var settings = ModData.Settings;

        if (!settings.IsPredictionEnabled || !settings.PotionPredictionEnabled ||
            !CombatManager.Instance.IsInProgress ||
            holder.Potion?.Model is not { Owner.Creature.CombatState: not null } potion ||
            _session?.Mode > mode)
        {
            return;
        }

        var session = new PotionPredictionSession(potion, holder, mode);
        session.ProjectionChanged += () => OnProjectionChanged(session);
        session.TargetingFinishing += () => OnTargetingFinishing(session);

        var previousSession = _session;
        _session = session;
        try
        {
            session.RefreshUntargeted();
        }
        finally
        {
            // Transfer projection ownership before releasing the old session to avoid clearing shared UI between them.
            previousSession?.Dispose();
        }
    }

    private static void EndSession(CombatPredictionSessionMode mode, NPotionHolder holder)
    {
        if (_session is { } session && session.Mode == mode && ReferenceEquals(session.Holder, holder))
        {
            _session = null;
            session.Dispose();
        }
    }

    private static void ShowTargetingHoverTips(PotionPredictionSession session)
    {
        var holder = session.Holder;
        ClearHolderHoverTips(holder);
        if (session.Projection?.HoverTips is not { Count: > 0 } hoverTips)
        {
            return;
        }

        // NTargetManager blocks ordinary HoverTips during selection. This target-specific surface deliberately
        // bypasses the block and remains attached to the potion holder while the selected target is active.
        var shouldBlockHoverTips = NHoverTipSet.shouldBlockHoverTips;
        NHoverTipSet.shouldBlockHoverTips = false;
        try
        {
            NHoverTipSet.CreateAndShow(holder, hoverTips, HoverTipAlignment.Center)
                ?.SetGlobalPosition(
                    holder.GlobalPosition +
                    Vector2.Down * holder.Size.Y * Mathf.Max(1.5f, holder.Scale.Y));
        }
        finally
        {
            NHoverTipSet.shouldBlockHoverTips = shouldBlockHoverTips;
        }
    }

    private static void ClearHolderHoverTips(NPotionHolder holder)
    {
        NHoverTipSet.Remove(holder);
    }

    private sealed class PotionPredictionSession(
        PotionModel potion,
        NPotionHolder holder,
        CombatPredictionSessionMode mode)
        : CombatPredictionSession(mode)
    {
        public override AbstractModel Source => potion;

        public NPotionHolder Holder { get; } = holder;

        protected override CombatPredictionProjection? Predict(Creature? target)
        {
            return CombatPotionPrediction.Predict(potion, target);
        }
    }
}

[HarmonyPatch(typeof(NPotionHolder))]
internal static class CombatPotionPredictionHolderPatches
{
    // This prefix must establish the session before vanilla OnFocus reads PotionModel.HoverTips.
    [HarmonyPatch("OnFocus")]
    [HarmonyPrefix]
    private static void BeginHoverPrediction(NPotionHolder __instance)
    {
        if (!__instance._isFocused)
        {
            CombatPotionPredictionController.OnPotionFocus(__instance);
        }
    }

    [HarmonyPatch("OnUnfocus")]
    [HarmonyPrefix]
    private static void EndHoverPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionUnfocus(__instance);
    }

    // A prefix is required before TargetNode calls StartTargeting: StartTargeting re-emits an already-focused
    // creature, and TargetNode may synchronously focus a creature or multiplayer nameplate immediately afterward.
    [HarmonyPatch(nameof(NPotionHolder.TargetNode))]
    [HarmonyPrefix]
    private static void BeginTargetPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionTargetingStart(__instance);
    }

    // Clear prediction state before vanilla replaces the potion with the empty-slot presentation.
    [HarmonyPatch(nameof(NPotionHolder.RemoveUsedPotion))]
    [HarmonyPrefix]
    private static void EndUsedPotionPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionRemoved(__instance);
    }

    [HarmonyPatch(nameof(NPotionHolder.DiscardPotion))]
    [HarmonyPrefix]
    private static void EndDiscardedPotionPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionRemoved(__instance);
    }
}

[HarmonyPatch(typeof(NPotionPopup))]
internal static class CombatPotionPredictionPopupPatches
{
    // NPotionPopup._Ready shows hover tips for the potion, so a prefix is required to establish the session
    // before vanilla reads PotionModel.HoverTips.
    [HarmonyPatch(nameof(NPotionPopup.Create))]
    [HarmonyPrefix]
    private static void BeginActionPrediction(NPotionHolder holder)
    {
        CombatPotionPredictionController.OnPotionPopupOpen(holder);
    }

    [HarmonyPatch(nameof(NPotionPopup.Remove))]
    [HarmonyPrefix]
    private static void EndActionPrediction(NPotionPopup __instance)
    {
        CombatPotionPredictionController.OnPotionPopupClose(__instance._holder);
    }

    [HarmonyPatch(nameof(NPotionPopup._ExitTree))]
    [HarmonyPrefix]
    private static void EndExitedPopupPrediction(NPotionPopup __instance)
    {
        CombatPotionPredictionController.OnPotionPopupClose(__instance._holder);
    }
}
