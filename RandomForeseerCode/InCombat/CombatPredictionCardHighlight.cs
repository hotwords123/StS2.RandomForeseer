using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>Applies the card highlights requested by the active combat-action projection.</summary>
internal static class CombatPredictionCardHighlight
{
    private static readonly Color PredictionHighlightColor = new(1f, 0.36f, 0f, 0.98f);
    private static HashSet<CardModel> _highlightedCards = [];

    /// <summary>Replaces the projected card set and refreshes holders affected by either the old or new set.</summary>
    public static void Show(IEnumerable<CardModel> cards)
    {
        var cardsToRefresh = _highlightedCards;
        _highlightedCards = [.. cards];
        cardsToRefresh.UnionWith(_highlightedCards);
        RefreshHandCards(cardsToRefresh);
    }

    /// <summary>Removes every projected card highlight while preserving vanilla highlight state.</summary>
    public static void Clear()
    {
        Show([]);
    }

    /// <summary>Reapplies the prediction color after vanilla refreshes a highlighted hand-card holder.</summary>
    public static void ApplyHighlightToHolder(NHandCardHolder holder)
    {
        if (holder.IsNodeReady() &&
            holder.CardNode is { Model: { } card } cardNode &&
            _highlightedCards.Contains(card))
        {
            cardNode.CardHighlight.AnimShow();
            cardNode.CardHighlight.Modulate = PredictionHighlightColor;
        }
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
