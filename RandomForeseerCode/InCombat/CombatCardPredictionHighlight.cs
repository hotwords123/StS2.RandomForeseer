using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardPredictionHighlight
{
    private static readonly Color PredictionHighlightColor = new(1f, 0.36f, 0f, 0.98f);
    private static HashSet<CardModel> _highlightedCards = [];

    public static void Show(IReadOnlyList<CardModel> cards)
    {
        var cardsToRefresh = _highlightedCards.Concat(cards).Distinct().ToArray();
        _highlightedCards = cards.ToHashSet();
        RefreshHandCards(cardsToRefresh);
    }

    public static void Clear()
    {
        Show([]);
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
internal static class CombatCardPredictionHandHighlightPatches
{
    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    [HarmonyPostfix]
    private static void ShowHighlightAfterCardUpdate(NHandCardHolder __instance)
    {
        CombatCardPredictionHighlight.ApplyHighlightToHolder(__instance);
    }
}
