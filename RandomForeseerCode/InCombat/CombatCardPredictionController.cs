using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardPredictionController
{
    private static ActiveCardPrediction? _activePrediction;

    private static bool _hasDamagePrediction;

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
        UpdatePredictions(ActiveCardPredictionSource.CardPlay, holder, target);
    }

    public static void OnCardPlayCleanedUp(NHandCardHolder holder)
    {
        ClearPredictions(ActiveCardPredictionSource.CardPlay, holder);
    }

    private static void UpdatePredictions(
        ActiveCardPredictionSource source,
        NHandCardHolder holder,
        Creature? target = null)
    {
        if (holder.CardModel is not { } card || !ShouldUpdatePredictions(source, card))
        {
            return;
        }

        _activePrediction = new ActiveCardPrediction(source, holder, card, target);

        ShowDamagePrediction(card, target);
        ShowSelectionHighlight(card, target);

        // Card damage predictions share the same display surfaces as end-turn prediction.
        EndTurnPredictionController.SetCardDamageOverride(_hasDamagePrediction);
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
        DamagePredictionResult prediction;

        try
        {
            prediction = OrbPrediction.PredictDamage(card, target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat orb prediction failed for {card.Id}: {ex}");
            ClearDamagePrediction();
            return;
        }

        if (!prediction.HasTargets)
        {
            ClearDamagePrediction();
            return;
        }

        CombatPredictionOverlay.Show(prediction);
        DamagePredictionHealthBarForecast.Set(prediction);
        _hasDamagePrediction = true;
    }

    private static void ShowSelectionHighlight(CardModel card, Creature? target)
    {
        IReadOnlyList<CardModel> selectedCards;

        try
        {
            selectedCards = CombatCardSelectionPrediction.PredictSelectedCards(card, target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card selection hand highlight prediction failed for {card.Id}: {ex}");
            CombatCardPredictionHighlight.Clear();
            return;
        }

        CombatCardPredictionHighlight.Show(selectedCards);
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
