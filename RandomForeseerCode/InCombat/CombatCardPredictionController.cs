using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardPredictionController
{
    private static ActiveCardPrediction? _activePrediction;

    private static bool _hasDamagePrediction;

    private static readonly List<IHoverTip> _cachedHoverTips = [];

    public static void OnCardHover(NHandCardHolder holder, bool isHovered)
    {
        if (isHovered)
        {
            UpdatePredictions(ActiveCardPredictionSource.Hover, holder);
        }
        else
        {
            ClearPredictions(ActiveCardPredictionSource.Hover, holder);
        }
    }

    public static void OnCardPlayStarted(NHandCardHolder holder)
    {
        UpdatePredictions(ActiveCardPredictionSource.CardPlay, holder);
    }

    public static void OnCardPlayTargetChanged(NHandCardHolder holder, Creature? target)
    {
        if (UpdatePredictions(ActiveCardPredictionSource.CardPlay, holder, target))
        {
            ShowCardPlayHoverTips(holder);
        }
    }

    public static void OnCardPlayTargetingStarted(Control control)
    {
        if (_activePrediction is not { Source: ActiveCardPredictionSource.CardPlay } activePrediction ||
            !ReferenceEquals(control, activePrediction.Holder.CardNode))
        {
            return;
        }

        ShowCardPlayHoverTips(activePrediction.Holder);
    }

    public static void OnCardPlayCleanedUp(NHandCardHolder holder)
    {
        ClearCardPlayHoverTips(holder);
        ClearPredictions(ActiveCardPredictionSource.CardPlay, holder);
    }

    private static bool UpdatePredictions(
        ActiveCardPredictionSource source,
        NHandCardHolder holder,
        Creature? target = null)
    {
        if (holder.CardModel is not { } card || !ShouldUpdatePredictions(source, card))
        {
            return false;
        }

        _activePrediction = new ActiveCardPrediction(source, holder, card, target);
        _cachedHoverTips.Clear();

        ShowDamagePrediction(card, target);
        ShowSelectionHighlight(card, target);
        AddCardGenerationHoverTips(card, target);

        // Card damage predictions share the same display surfaces as end-turn prediction.
        EndTurnPredictionController.SetCardDamageOverride(_hasDamagePrediction);
        return true;
    }

    private static bool ShouldUpdatePredictions(ActiveCardPredictionSource source, CardModel card)
    {
        if (_activePrediction is null)
        {
            return true;
        }

        return !ReferenceEquals(_activePrediction.Card, card) ||
            source == _activePrediction.Source ||
            source == ActiveCardPredictionSource.CardPlay;
    }

    private static void ClearPredictions(ActiveCardPredictionSource source, NHandCardHolder holder)
    {
        if (!ShouldClearPredictions(source, holder))
        {
            return;
        }

        _activePrediction = null;
        _cachedHoverTips.Clear();

        ClearDamagePrediction();
        CombatCardPredictionHighlight.Clear();

        // Refresh end-turn prediction in case the card hover was taking precedence over it.
        EndTurnPredictionController.SetCardDamageOverride(false);
    }

    private static bool ShouldClearPredictions(ActiveCardPredictionSource source, NHandCardHolder holder)
    {
        // On a successful play, vanilla reparents the NCard away from the holder before
        // NCardPlay.Cleanup postfix runs, so holder.CardModel may already be null here.
        // The NHandCardHolder reference itself is stable for the play lifecycle.
        return _activePrediction is { } activePrediction &&
            activePrediction.Source == source &&
            ReferenceEquals(activePrediction.Holder, holder);
    }

    private static void ShowDamagePrediction(CardModel card, Creature? target)
    {
        if (GetDamagePredictionResult(card, target) is { } prediction)
        {
            CombatPredictionOverlay.Show(prediction);
            DamagePredictionHealthBarForecast.Set(prediction);
            _hasDamagePrediction = true;
        }
        else
        {
            ClearDamagePrediction();
        }
    }

    private static DamagePredictionResult? GetDamagePredictionResult(CardModel card, Creature? target)
    {
        try
        {
            if (OrbPrediction.Predict(card, target) is { } prediction)
            {
                _cachedHoverTips.AddRange(prediction.ToHoverTips());
                return prediction.DamagePrediction;
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat orb prediction failed for {card.Id}: {ex}");
        }

        try
        {
            if (RandomTargetAttackPrediction.Predict(card) is { } prediction)
            {
                _cachedHoverTips.AddRange(prediction.ToHoverTips());
                return prediction.DamagePrediction;
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat random target attack prediction failed for {card.Id}: {ex}");
        }

        return null;
    }

    private static void ShowSelectionHighlight(CardModel card, Creature? target)
    {
        CombatCardSelectionPredictionResult prediction;

        try
        {
            prediction = CombatCardSelectionPrediction.Predict(card, target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card selection hand highlight prediction failed for {card.Id}: {ex}");
            CombatCardPredictionHighlight.Clear();
            return;
        }

        CombatCardPredictionHighlight.Show(prediction.CardBundles
            .SelectMany(static bundle => bundle)
            .Select(static card => card.Original)
            .ToList());

        _cachedHoverTips.AddRange(prediction.ToHoverTips());
    }

    private static void AddCardGenerationHoverTips(CardModel card, Creature? target)
    {
        try
        {
            if (CombatCardGenerationPrediction.Predict(card, target) is { } prediction)
            {
                _cachedHoverTips.AddRange(prediction.ToHoverTips());
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card generation target prediction failed for {card.Id}: {ex}");
        }
    }

    private static void ShowCardPlayHoverTips(NHandCardHolder holder)
    {
        ClearCardPlayHoverTips(holder);
        if (_cachedHoverTips.Count == 0)
        {
            return;
        }

        // NTargetManager blocks normal hover tips while selecting a target. This tooltip is an
        // explicit card-play prediction surface, so temporarily bypass that global block.
        var shouldBlockHoverTips = NHoverTipSet.shouldBlockHoverTips;
        NHoverTipSet.shouldBlockHoverTips = false;
        try
        {
            NHoverTipSet.CreateAndShow(holder, _cachedHoverTips)?.SetAlignmentForCardHolder(holder);
        }
        finally
        {
            NHoverTipSet.shouldBlockHoverTips = shouldBlockHoverTips;
        }
    }

    private static void ClearDamagePrediction()
    {
        if (_hasDamagePrediction)
        {
            CombatPredictionOverlay.Clear();
            DamagePredictionHealthBarForecast.Clear();
            _hasDamagePrediction = false;
        }
    }

    private static void ClearCardPlayHoverTips(NHandCardHolder holder)
    {
        NHoverTipSet.Remove(holder);
    }

    private sealed record ActiveCardPrediction(
        ActiveCardPredictionSource Source,
        NHandCardHolder Holder,
        CardModel Card,
        Creature? Target);

    private enum ActiveCardPredictionSource
    {
        Hover,
        CardPlay
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatCardPredictionHandPatches
{
    [HarmonyPatch("DoCardHoverEffects")]
    [HarmonyPostfix]
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
    [HarmonyPatch(nameof(NCardPlay.OnCreatureHover))]
    [HarmonyPostfix]
    private static void UpdatePredictionsOnCreatureHover(NCardPlay __instance, NCreature creature)
    {
        CombatCardPredictionController.OnCardPlayTargetChanged(__instance.Holder, creature.Entity);
    }

    [HarmonyPatch(nameof(NCardPlay.OnCreatureUnhover))]
    [HarmonyPostfix]
    private static void UpdatePredictionsOnCreatureUnhover(NCardPlay __instance)
    {
        CombatCardPredictionController.OnCardPlayTargetChanged(__instance.Holder, null);
    }

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
    [HarmonyPatch(
        nameof(NTargetManager.StartTargeting),
        [
            typeof(TargetType),
            typeof(Control),
            typeof(TargetMode),
            typeof(Func<bool>),
            typeof(Func<Node, bool>)
        ])]
    [HarmonyPostfix]
    private static void ShowPredictionHoverTipsOnTargetingStarted(Control control)
    {
        CombatCardPredictionController.OnCardPlayTargetingStarted(control);
    }
}
