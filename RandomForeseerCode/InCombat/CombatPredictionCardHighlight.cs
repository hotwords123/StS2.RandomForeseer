using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.Common;
using RandomForeseer.Data;
using RandomForeseer.Telemetry;

namespace RandomForeseer.InCombat;

internal sealed record CardHighlight(IReadOnlyList<AbstractModel> Sources);

/// <summary>Displays prediction highlights and source icons on the supplied hand cards.</summary>
internal static class CombatPredictionCardHighlight
{
    private const string SourceIconsName = $"{Entry.ModId}SourceIcons";
    private static readonly Color PredictionHighlightColor = new(1f, 0.36f, 0f, 0.98f);

    private static readonly Dictionary<CardModel, CardHighlight> EmptyHighlights = [];

    private static readonly HashSet<CardModel> QueuedCards = [];
    private static bool _refreshQueued;
    private static IReadOnlyDictionary<CardModel, CardHighlight> _highlights = EmptyHighlights;

    public static void Show(IReadOnlyDictionary<CardModel, CardHighlight> highlights)
    {
        if (!ModData.Settings.CardHighlightSourceIconsEnabled)
        {
            highlights = highlights.ToDictionary(pair => pair.Key, _ => new CardHighlight([]));
        }

        var oldHighlights = _highlights;
        _highlights = highlights;

        foreach (var card in oldHighlights.Keys.Except(highlights.Keys))
        {
            QueuedCards.Add(card);
        }

        foreach (var (card, highlight) in highlights)
        {
            if (!oldHighlights.TryGetValue(card, out var existing) ||
                !highlight.Sources.Select(source => source.Id)
                    .SequenceEqual(existing.Sources.Select(source => source.Id)))
            {
                QueuedCards.Add(card);
            }
        }

        if (QueuedCards.Count == 0 || _refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Callable.From(FlushQueuedCards).CallDeferred();
    }

    public static void Clear() => Show(EmptyHighlights);

    /// <summary>Reapplies the prediction color after vanilla refreshes a highlighted hand-card holder.</summary>
    public static void ApplyHighlightToHolder(NHandCardHolder holder)
    {
        if (!holder.IsNodeReady())
        {
            return;
        }

        UpdateSourceIcons(holder);
        var cardNode = holder.CardNode;
        if (cardNode?.Model is not { } card || !_highlights.ContainsKey(card))
        {
            return;
        }

        cardNode.CardHighlight.AnimShow();
        cardNode.CardHighlight.Modulate = PredictionHighlightColor;
    }

    private static void UpdateSourceIcons(NHandCardHolder holder)
    {
        var container = holder.GetNodeOrNull<Control>(SourceIconsName);
        if (container != null)
        {
            foreach (var child in container.GetChildren())
            {
                container.RemoveChildSafely(child);
                child.QueueFreeSafely();
            }

            container.Visible = false;
        }

        if (holder.CardNode?.Model is not { } card || !_highlights.TryGetValue(card, out var highlight))
        {
            return;
        }

        if (container == null)
        {
            container = new Control
            {
                Name = SourceIconsName,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            holder.AddChild(container);
        }

        for (var i = 0; i < highlight.Sources.Count; i++)
        {
            var icon = PredictionSourceIcon.Create(highlight.Sources[i], 64f);
            // Card bounds are (-150, -211) to (150, 211). Leave 21px of the badge inside the top edge.
            icon.Position = new Vector2(86f - i * 54f, -254f);
            container.AddChild(icon);
        }

        container.Visible = true;
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
