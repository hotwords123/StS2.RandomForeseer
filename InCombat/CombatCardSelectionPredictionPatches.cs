using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace RandomForeseer.InCombat;

internal static class CombatCardSelectionPredictionHighlight
{
    private static readonly Color PredictionHighlightColor = new(1f, 0.36f, 0f, 0.98f);
    private static CardModel? _sourceCard;
    private static HashSet<CardModel> _highlightedCards = [];

    public static void ApplyHighlightsForSource(CardModel? source)
    {
        if (source == null)
        {
            return;
        }

        CombatCardSelectionPredictionResult prediction;
        try
        {
            prediction = CombatCardSelectionPrediction.GetPrediction(source);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card selection hand highlight prediction failed for {source.Id}: {ex}");
            return;
        }

        SetHighlightedCards(source, prediction.SelectedCards);
    }

    public static void ClearHighlightsForSource(CardModel? source)
    {
        if (source != null && _sourceCard == source)
        {
            SetHighlightedCards(null, []);
        }
    }

    public static void ApplyHighlightToHolder(NHandCardHolder holder)
    {
        if (!holder.IsNodeReady() ||
            holder.CardNode is not { Model: { } card } cardNode ||
            !_highlightedCards.Contains(card))
        {
            return;
        }

        cardNode.CardHighlight.AnimShow();
        cardNode.CardHighlight.Modulate = PredictionHighlightColor;
    }

    private static void SetHighlightedCards(CardModel? source, IReadOnlyList<CardModel> cards)
    {
        var cardsToRefresh = _highlightedCards
            .Concat(cards)
            .Distinct()
            .ToList();

        _sourceCard = source;
        _highlightedCards = cards.ToHashSet();

        RefreshHandCards(cardsToRefresh);
    }

    private static void RefreshHandCards(IEnumerable<CardModel> cards)
    {
        var hand = NPlayerHand.Instance;
        if (hand == null)
        {
            return;
        }

        foreach (var card in cards)
        {
            if (hand.GetCardHolder(card) is NHandCardHolder holder)
            {
                holder.UpdateCard();
            }
        }
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatCardSelectionPredictionHandHighlightPatches
{
    [HarmonyPatch("DoCardHoverEffects")]
    [HarmonyPostfix]
    private static void UpdateHighlightsOnCardHover(NHandCardHolder __instance, bool isHovered)
    {
        var source = __instance.CardNode?.Model;

        if (isHovered)
        {
            CombatCardSelectionPredictionHighlight.ApplyHighlightsForSource(source);
        }
        else
        {
            CombatCardSelectionPredictionHighlight.ClearHighlightsForSource(source);
        }
    }

    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    [HarmonyPostfix]
    private static void ShowHighlightAfterCardUpdate(NHandCardHolder __instance)
    {
        CombatCardSelectionPredictionHighlight.ApplyHighlightToHolder(__instance);
    }
}

[HarmonyPatch(typeof(NPlayerHand), "StartCardPlay")]
internal static class CombatCardSelectionPredictionStartCardPlayHighlightPatch
{
    private static void Postfix(NHandCardHolder holder)
    {
        CombatCardSelectionPredictionHighlight.ApplyHighlightsForSource(holder.CardModel);
    }
}

[HarmonyPatch(typeof(NCardPlay), "Cleanup")]
internal static class CombatCardSelectionPredictionCardPlayCleanupHighlightPatch
{
    private static void Postfix(NCardPlay __instance)
    {
        CombatCardSelectionPredictionHighlight.ClearHighlightsForSource(__instance.Holder?.CardModel);
    }
}
