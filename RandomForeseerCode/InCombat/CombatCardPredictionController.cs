using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardPredictionController
{
    private static CardPredictionSession? _session;

    public static void OnCardHover(NHandCardHolder holder, bool isHovered)
    {
        if (isHovered)
        {
            BeginSession(CombatPredictionSessionMode.Hover, holder);
        }
        else
        {
            EndSession(CombatPredictionSessionMode.Hover, holder);
        }
    }

    public static void OnCardPlayStarted(NHandCardHolder holder)
    {
        BeginSession(CombatPredictionSessionMode.Action, holder);
    }

    public static void OnCardPlayTargetingStarting(Control control)
    {
        if (_session is not { Mode: CombatPredictionSessionMode.Action, IsTargeting: false } session ||
            !ReferenceEquals(control, session.Holder.CardNode))
        {
            return;
        }

        session.BeginTargeting(NTargetManager.Instance);
    }

    public static void OnCardPlayCleanedUp(NHandCardHolder holder)
    {
        ClearCardPlayHoverTips(holder);
        EndSession(CombatPredictionSessionMode.Action, holder);
    }

    /// <summary>
    /// Reuses the projection already produced for the active local hand hover.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the active hover session owns this card, including when that session produced no
    /// projection. Callers must not run a fallback simulation in that case.
    /// </returns>
    public static bool TryGetActiveHoverTips(CardModel card, out IReadOnlyList<IHoverTip> hoverTips)
    {
        if (_session is { Mode: CombatPredictionSessionMode.Hover } session && session.Source == card)
        {
            hoverTips = session.Projection?.HoverTips ?? [];
            return true;
        }

        hoverTips = [];
        return false;
    }

    private static void OnProjectionChanged(CardPredictionSession session)
    {
        if (session != _session || !session.IsTargeting)
        {
            return;
        }

        ShowCardPlayHoverTips(session);
    }

    private static void BeginSession(CombatPredictionSessionMode mode, NHandCardHolder holder)
    {
        var settings = ModData.Settings;

        if (!settings.IsPredictionEnabled || !settings.CardPlayPredictionEnabled ||
            !CombatManager.Instance.IsInProgress ||
            holder.CardModel is not { Owner.Creature.CombatState: not null } card ||
            _session?.Mode > mode)
        {
            return;
        }

        var session = new CardPredictionSession(card, holder, mode);
        session.ProjectionChanged += () => OnProjectionChanged(session);

        var previousSession = _session;
        _session = session;
        try
        {
            session.RefreshUntargeted();
        }
        finally
        {
            // Let the new session take projection ownership before releasing the old one. Disposing first would
            // briefly clear the shared UI and refresh end-turn prediction before the new projection replaces it.
            previousSession?.Dispose();
        }
    }

    private static void EndSession(CombatPredictionSessionMode mode, NHandCardHolder holder)
    {
        // On a successful play, vanilla reparents the NCard away from the holder before
        // NCardPlay.Cleanup postfix runs, so holder.CardModel may already be null here.
        // The NHandCardHolder reference itself is stable for the play lifecycle.
        if (_session is not { } session || session.Mode != mode || !ReferenceEquals(session.Holder, holder))
        {
            return;
        }

        _session = null;
        session.Dispose();
    }

    private static void ShowCardPlayHoverTips(CardPredictionSession session)
    {
        var holder = session.Holder;
        ClearCardPlayHoverTips(holder);
        if (session.Projection?.HoverTips is not { Count: > 0 } hoverTips)
        {
            return;
        }

        // NTargetManager blocks normal hover tips while selecting a target. This tooltip is an
        // explicit card-play prediction surface, so temporarily bypass that global block.
        var shouldBlockHoverTips = NHoverTipSet.shouldBlockHoverTips;
        NHoverTipSet.shouldBlockHoverTips = false;
        try
        {
            NHoverTipSet.CreateAndShow(holder, hoverTips)?.SetAlignmentForCardHolder(holder);
        }
        finally
        {
            NHoverTipSet.shouldBlockHoverTips = shouldBlockHoverTips;
        }
    }

    private static void ClearCardPlayHoverTips(NHandCardHolder holder)
    {
        NHoverTipSet.Remove(holder);
    }

    private sealed class CardPredictionSession(
        CardModel card,
        NHandCardHolder holder,
        CombatPredictionSessionMode mode)
        : CombatPredictionSession(mode)
    {
        public override AbstractModel Source => card;

        public NHandCardHolder Holder { get; } = holder;

        protected override CombatPredictionProjection? Predict(Creature? target)
        {
            return CombatCardPrediction.Predict(card, target);
        }
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatCardPredictionHandPatches
{
    [HarmonyPatch("DoCardHoverEffects")]
    [HarmonyPrefix]
    private static void UpdatePredictionOnCardHover(NHandCardHolder __instance, bool isHovered)
    {
        CombatCardPredictionController.OnCardHover(__instance, isHovered);
    }
}

[HarmonyPatch(typeof(NPlayerHand))]
internal static class CombatCardPredictionPlayerHandPatches
{
    [HarmonyPatch(nameof(NPlayerHand.StartCardPlay))]
    [HarmonyPrefix]
    private static void UpdatePredictionsOnCardPlayStarted(NHandCardHolder holder)
    {
        CombatCardPredictionController.OnCardPlayStarted(holder);
    }
}

[HarmonyPatch(typeof(NCardPlay))]
internal static class CombatCardPredictionCardPlayPatches
{
    [HarmonyPatch("Cleanup")]
    [HarmonyPostfix]
    private static void CleanupPredictions(NCardPlay __instance)
    {
        CombatCardPredictionController.OnCardPlayCleanedUp(__instance.Holder);
    }
}

[HarmonyPatch(typeof(NTargetManager))]
internal static class CombatCardPredictionTargetManagerPatches
{
    // NTargetManager also has a Vector2 overload for target pickers that only know a
    // screen position. Card play uses the Control overload with the card node, which
    // lets us verify that this targeting session belongs to the active dragged card.
    // This must run as a prefix: StartTargeting synchronously calls OnTargetingStarted
    // on every NCreature, and an already focused creature emits CreatureHovered there.
    // Subscribing in a postfix would miss that initial target event.
    [HarmonyPatch(
        nameof(NTargetManager.StartTargeting),
        [
            typeof(TargetType),
            typeof(Control),
            typeof(TargetMode),
            typeof(Func<bool>),
            typeof(Func<Node, bool>)
        ])]
    [HarmonyPrefix]
    private static void ObservePredictionTargetsBeforeTargetingStarts(Control control)
    {
        CombatCardPredictionController.OnCardPlayTargetingStarting(control);
    }
}
