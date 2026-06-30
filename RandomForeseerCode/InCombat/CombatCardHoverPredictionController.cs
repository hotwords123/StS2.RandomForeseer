using HarmonyLib;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardHoverPredictionController
{
    private static readonly ConditionalWeakTable<NHandCardHolder, CardModel> CardPlaySources = [];

    private static CardModel? _sourceCard;

    private static bool _hasDamagePrediction;

    public static void OnCardHover(NHandCardHolder holder, bool isHovered)
    {
        if (holder.CardModel is not { } card)
        {
            return;
        }

        if (isHovered)
        {
            UpdatePredictionsForCard(card);
        }
        else
        {
            ClearPredictionsForCard(card);
        }
    }

    public static void OnCardPlayStarted(NHandCardHolder holder)
    {
        if (holder.CardModel is not { } card)
        {
            return;
        }

        CardPlaySources.AddOrUpdate(holder, card);
        UpdatePredictionsForCard(card);
    }

    public static void OnCardPlayCleanedUp(NHandCardHolder holder)
    {
        if (!CardPlaySources.TryGetValue(holder, out var card))
        {
            return;
        }

        CardPlaySources.Remove(holder);
        ClearPredictionsForCard(card);
    }

    private static void UpdatePredictionsForCard(CardModel card)
    {
        _sourceCard = card;

        ShowDamagePrediction(card);
        ShowSelectionHighlight(card);

        // Card damage predictions share the same display surfaces as end-turn prediction.
        EndTurnPredictionController.SetCardDamageOverride(_hasDamagePrediction);
    }

    private static void ClearPredictionsForCard(CardModel card)
    {
        if (!ReferenceEquals(_sourceCard, card))
        {
            return;
        }

        _sourceCard = null;

        ClearDamagePrediction();
        CombatCardSelectionPredictionHighlight.Clear();

        // Refresh end-turn prediction in case the card hover was taking precedence over it.
        EndTurnPredictionController.SetCardDamageOverride(false);
    }

    private static void ShowDamagePrediction(CardModel card)
    {
        DamagePredictionResult prediction;

        try
        {
            prediction = OrbPrediction.PredictDamage(card);
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

    private static void ShowSelectionHighlight(CardModel card)
    {
        CombatCardSelectionPredictionResult prediction;

        try
        {
            prediction = CombatCardSelectionPrediction.GetPrediction(card);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card selection hand highlight prediction failed for {card.Id}: {ex}");
            return;
        }

        CombatCardSelectionPredictionHighlight.Show(prediction.SelectedCards);
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
}

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatCardHoverPredictionHandPatches
{
    [HarmonyPatch("DoCardHoverEffects")]
    [HarmonyPostfix]
    private static void UpdatePredictionsOnCardHover(NHandCardHolder __instance, bool isHovered)
    {
        CombatCardHoverPredictionController.OnCardHover(__instance, isHovered);
    }
}

[HarmonyPatch(typeof(NPlayerHand), "StartCardPlay")]
internal static class CombatCardHoverPredictionStartCardPlayPatch
{
    private static void Postfix(NHandCardHolder holder)
    {
        CombatCardHoverPredictionController.OnCardPlayStarted(holder);
    }
}

[HarmonyPatch(typeof(NCardPlay), "Cleanup")]
internal static class CombatCardHoverPredictionCardPlayCleanupPatch
{
    private static void Postfix(NCardPlay __instance)
    {
        CombatCardHoverPredictionController.OnCardPlayCleanedUp(__instance.Holder);
    }
}

[HarmonyPatch(typeof(NCombatRoom))]
internal static class CombatPredictionOverlayCombatRoomPatches
{
    [HarmonyPatch("RemoveCreatureNode")]
    [HarmonyPostfix]
    private static void RefreshOverlayAfterCreatureRemoved()
    {
        CombatPredictionOverlay.RefreshPositions();
    }
}
