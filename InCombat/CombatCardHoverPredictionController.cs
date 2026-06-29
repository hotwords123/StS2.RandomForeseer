using HarmonyLib;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace RandomForeseer.InCombat;

internal static class CombatCardHoverPredictionController
{
    private static readonly ConditionalWeakTable<NHandCardHolder, CardModel> CardPlaySources = [];

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
        UpdateOrbOverlay(card);
        UpdateSelectionHighlight(card);
    }

    private static void ClearPredictionsForCard(CardModel card)
    {
        CombatPredictionOverlay.Clear(card);
        CombatPredictionHealthBarForecast.Clear(card);
        CombatCardSelectionPredictionHighlight.Clear(card);
    }

    private static void UpdateOrbOverlay(CardModel card)
    {
        try
        {
            var prediction = OrbPrediction.Predict(card);
            if (prediction == OrbPredictionResult.Empty)
            {
                CombatPredictionOverlay.Clear(card);
                CombatPredictionHealthBarForecast.Clear(card);
                return;
            }

            CombatPredictionOverlay.Show(card, prediction.OverlayContent);
            CombatPredictionHealthBarForecast.Set(card, prediction.OverlayContent);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat orb prediction overlay failed for {card.Id}: {ex}");
            CombatPredictionOverlay.Clear(card);
            CombatPredictionHealthBarForecast.Clear(card);
            return;
        }
    }

    private static void UpdateSelectionHighlight(CardModel card)
    {
        try
        {
            var prediction = CombatCardSelectionPrediction.GetPrediction(card);
            CombatCardSelectionPredictionHighlight.Show(card, prediction.SelectedCards);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card selection hand highlight prediction failed for {card.Id}: {ex}");
            CombatCardSelectionPredictionHighlight.Clear(card);
            return;
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
