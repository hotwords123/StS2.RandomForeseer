using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>Applies the card highlights requested by the active combat-action projection.</summary>
internal static class CombatPredictionCardHighlight
{
    private static readonly Color PredictionHighlightColor = new(1f, 0.36f, 0f, 0.98f);

    private static readonly HashSet<CardModel> HighlightedCards = [];
    private static readonly HashSet<CardModel> QueuedCards = [];
    private static bool _refreshQueued;

    /// <summary>Replaces the projected card set and refreshes holders affected by either the old or new set.</summary>
    public static void Show(IEnumerable<CardModel> cards)
    {
        QueuedCards.UnionWith(HighlightedCards);
        HighlightedCards.Clear();
        HighlightedCards.UnionWith(cards);
        QueuedCards.UnionWith(HighlightedCards);

        if (QueuedCards.Count == 0 || _refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Callable.From(FlushQueuedCards).CallDeferred();
    }

    /// <summary>Removes every projected card highlight while preserving vanilla highlight state.</summary>
    public static void Clear()
    {
        Show([]);
    }

    /// <summary>Reapplies the prediction color after vanilla refreshes a highlighted hand-card holder.</summary>
    public static void ApplyHighlightToHolder(NHandCardHolder holder)
    {
        if (!holder.IsNodeReady())
        {
            return;
        }

        var cardNode = holder.CardNode;
        if (cardNode is not { Model: { } card } || !HighlightedCards.Contains(card))
        {
            return;
        }

        cardNode.CardHighlight.AnimShow();
        cardNode.CardHighlight.Modulate = PredictionHighlightColor;
    }

    private static void FlushQueuedCards()
    {
        var cardsToRefresh = QueuedCards.ToArray();
        QueuedCards.Clear();
        _refreshQueued = false;

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
            try
            {
                if (hand.GetCardHolder(card) is NHandCardHolder holder &&
                    GodotObject.IsInstanceValid(holder) &&
                    GodotObject.IsInstanceValid(holder.CardNode))
                {
                    holder.UpdateCard();
                }
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Combat card highlight failed on refresh: {ex}");
                ModTelemetry.CaptureException(ex, "combat_card_highlight", "refresh_card");
            }
        }
    }
}
