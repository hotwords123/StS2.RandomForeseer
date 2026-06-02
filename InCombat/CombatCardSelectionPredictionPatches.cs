using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatCardSelectionPredictionHandHighlightPatches
{
    private static readonly Color PredictionHighlightColor = new(1f, 0.36f, 0f, 0.98f);
    private static CardModel? _sourceCard;
    private static HashSet<CardModel> _highlightedCards = [];

    public static bool IsHighlighted(CardModel? card)
    {
        return card != null && _highlightedCards.Contains(card);
    }

    [HarmonyPatch("DoCardHoverEffects")]
    [HarmonyPostfix]
    private static void UpdatePredictionHighlightsOnHover(NHandCardHolder __instance, bool isHovered)
    {
        var source = __instance.CardNode?.Model;
        if (source == null)
        {
            return;
        }

        if (!isHovered)
        {
            if (_sourceCard == source)
            {
                SetHighlightedCards(null, new HashSet<CardModel>());
            }

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

        SetHighlightedCards(source, prediction.HandCardsToHighlight);
    }

    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    [HarmonyPostfix]
    private static void ShowPredictionHighlight(NHandCardHolder __instance)
    {
        var card = __instance.CardNode?.Model;
        if (!IsHighlighted(card) || __instance.CardNode == null)
        {
            return;
        }

        __instance.CardNode.CardHighlight.AnimShow();
        __instance.CardNode.CardHighlight.Modulate = PredictionHighlightColor;
    }

    private static void SetHighlightedCards(CardModel? source, IReadOnlySet<CardModel> cards)
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
