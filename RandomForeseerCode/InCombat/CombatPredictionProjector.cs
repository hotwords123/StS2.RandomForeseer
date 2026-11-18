using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.Common;
using RandomForeseer.Common.HoverTips;
using RandomForeseer.Data;
using RandomForeseer.InCombat.Extensions;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

/// <summary>
/// Contains the presentation payload produced from one unified combat-action simulation.
/// </summary>
/// <param name="HoverTips">Ordered prediction tips before the game separates text and card containers.</param>
/// <param name="DamagePrediction">Damage overlay and health-bar payload accepted by damage-specific gates.</param>
/// <param name="HighlightedCards">Original live cards selected by accepted card-selection entries.</param>
/// <param name="Risk">Shared risk accumulated through the completion boundaries of accepted results.</param>
internal sealed record CombatPredictionProjection(
    IReadOnlyList<IHoverTip> HoverTips,
    DamagePrediction DamagePrediction,
    IReadOnlySet<CardModel> HighlightedCards,
    IPredictionRisk Risk);

/// <summary>
/// Streams completed combat prediction history into hover tips, damage, and card highlights.
/// </summary>
/// <remarks>
/// Card and potion projections use the root action to scope effects and apply the chained prediction setting.
/// End-turn projections read the full timeline and can scope non-damage effects to one player.
/// </remarks>
internal sealed class CombatPredictionProjector
{
    private const int MaxHoverTips = 10;

    private readonly CombatPredictionHistory _history;
    private readonly PredictionTraceFrame? _rootFrame;
    private readonly Player? _playerFilter;

    private readonly Dictionary<Type, EntryProjectionRule> _rules = [];

    private readonly List<IHoverTip> _hoverTips = [];
    private readonly HashSet<CardModel> _highlightedCards = [];
    private readonly List<CombatPredictionDamageReceivedEntry> _damageEntries = [];
    private readonly List<CombatPredictionHistoryEntry> _relevantEntries = [];

    // Once an entry owned by a nested action triggers the chained-prediction gate, no later entry may contribute.
    private bool _projectionTruncated;

    private readonly CombatPredictionCausalTipBuilder _causalTips;

    /// <summary>
    /// Projects a card or potion action, or the full end-turn history.
    /// </summary>
    /// <param name="history">The completed history of the action or end-turn simulation.</param>
    /// <param name="rootFrame">
    /// The root <see cref="PredictionActionKind.CardPlay"/> or <see cref="PredictionActionKind.PotionUse"/> frame,
    /// or <see langword="null"/> for a complete end-turn simulation.
    /// </param>
    /// <param name="playerFilter">The owner of projected non-damage effects, or <see langword="null"/> for all players.</param>
    /// <returns>A presentation projection containing only results accepted by enabled feature policies.</returns>
    /// <remarks>
    /// An action frame must come from the same simulation as the history. Entries are dispatched in timeline order.
    /// Deferred entries use their resolved snapshot and completion boundary.
    /// </remarks>
    public static CombatPredictionProjection Project(
        CombatPredictionHistory history,
        PredictionTraceFrame? rootFrame,
        Player? playerFilter = null)
    {
        return new CombatPredictionProjector(history, rootFrame, playerFilter).Project();
    }

    private CombatPredictionProjector(
        CombatPredictionHistory history,
        PredictionTraceFrame? rootFrame,
        Player? playerFilter)
    {
        if (rootFrame is not null &&
            rootFrame.Invocation.Action is not (PredictionActionKind.CardPlay or PredictionActionKind.PotionUse))
        {
            throw new ArgumentException(
                $"Root frame must be a card-play or potion-use action, but was {rootFrame.Invocation.Action}.",
                nameof(rootFrame));
        }

        _history = history;
        _rootFrame = rootFrame;
        _playerFilter = playerFilter;

        _causalTips = new(rootFrame);

        RegisterRules();
    }

    private CombatPredictionProjection Project()
    {
        foreach (var entry in _history.Entries)
        {
            if (_projectionTruncated)
            {
                break;
            }

            Dispatch(entry);
        }

        return FinalizeProjection();
    }

    /// <summary>
    /// Declares the single projection rule for each supported semantic history entry type.
    /// </summary>
    /// <remarks>
    /// Card- and potion-specific settings are selected once from the root action kind, so nested actions inherit the
    /// projection policy of the card or potion whose prediction the player requested.
    /// </remarks>
    private void RegisterRules()
    {
        var rootAction = _rootFrame?.Invocation.Action;
        var settings = ModData.Settings;

        Register<CombatPredictionCardGeneratedEntry>(
            HandleCardGenerated,
            rootAction is PredictionActionKind.PotionUse
                ? settings.PotionCardGenerationPredictionEnabled
                : settings.CombatCardGenerationPredictionEnabled);

        Register<CombatPredictionCardGenerationOptionsEntry>(
            HandleCardGenerationOptions,
            rootAction is PredictionActionKind.PotionUse
                ? settings.PotionCardGenerationPredictionEnabled
                : settings.CombatCardGenerationPredictionEnabled);

        Register<CombatPredictionPotionGeneratedEntry>(
            HandlePotionGenerated,
            settings.PotionGenerationPredictionEnabled);

        Register<CombatPredictionCardsSelectedEntry>(
            HandleCardsSelected,
            settings.CombatCardSelectionPredictionEnabled);

        Register<CombatPredictionAutoPlayFromDrawPileEntry>(
            HandleDrawPileAutoPlay,
            settings.AutoPlayFromDrawPilePredictionEnabled);

        Register<CombatPredictionCardDrawnEntry>(
            HandleCardDrawn,
            rootAction is PredictionActionKind.PotionUse
                ? settings.PotionDrawPredictionEnabled
                : settings.CardDrawPredictionEnabled);

        Register<CombatPredictionCardCostsRandomizedEntry>(
            HandleCardCostsRandomized,
            rootAction is PredictionActionKind.PotionUse
                ? settings.PotionDrawPredictionEnabled
                : settings.CardDrawPredictionEnabled);

        Register<CombatPredictionOrbChanneledEntry>(
            HandleOrbChanneled,
            settings.CombatOrbGenerationPredictionEnabled);

        Register<CombatPredictionDamageReceivedEntry>(
            HandleDamage,
            settings.CombatDamagePredictionEnabled);
    }

    private void Register<TEntry>(Func<TEntry, CombatPredictionHistoryEntry?> handler, bool enabled)
        where TEntry : CombatPredictionHistoryEntry
    {
        _rules.Add(typeof(TEntry), new EntryProjectionRule(entry => handler((TEntry)entry), enabled));
    }

    /// <summary>
    /// Applies the root-action projection rule for the entry's exact semantic type.
    /// </summary>
    /// <remarks>
    /// With a root frame, only entries in its trace are eligible. When chained prediction is disabled, the first
    /// entry owned by a nested action truncates the remaining timeline even if that entry type has no projection rule.
    /// </remarks>
    private void Dispatch(CombatPredictionHistoryEntry entry)
    {
        if (_rootFrame != null)
        {
            if (entry.Trace?.FindOriginatingAction() is not { } actionFrame ||
                !actionFrame.Ancestors().Contains(_rootFrame))
            {
                return;
            }

            if (!ModData.Settings.ChainedCardEffectPredictionEnabled && actionFrame != _rootFrame)
            {
                _projectionTruncated = true;
                return;
            }
        }

        if (!_rules.TryGetValue(entry.GetType(), out var rule))
        {
            return;
        }

        if (!rule.Enabled)
        {
            return;
        }

        if (rule.Handler(entry) is { } relevantEntry)
        {
            _relevantEntries.Add(relevantEntry);
        }
    }

    private bool MatchesPlayer(Player? owner) => _playerFilter is null || _playerFilter == owner;

    private CombatPredictionHistoryEntry? HandleCardGenerated(CombatPredictionCardGeneratedEntry entry)
    {
        if (entry.ResultKind is CardGenerationResultKind.Fixed || !MatchesPlayer(entry.Card.Preview.Owner))
        {
            return null;
        }

        var resolved = _history.GetResolvedEntry<CombatPredictionCardGenerationResolvedEntry>(entry);
        if (resolved.PreviewCard is null)
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.Card(resolved.PreviewCard));
        AddCausalEffect(entry, CausalEffectKind.GenerateCards, [resolved.PreviewCard]);
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleCardGenerationOptions(CombatPredictionCardGenerationOptionsEntry entry)
    {
        if (entry.PreviewCards is not [var card, ..] || !MatchesPlayer(card.Owner))
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.CardBundle(entry.PreviewCards));
        AddCausalEffect(entry, CausalEffectKind.GenerateCards, entry.PreviewCards);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandlePotionGenerated(CombatPredictionPotionGeneratedEntry entry)
    {
        if (!MatchesPlayer(entry.PreviewPotion.Owner))
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.Potion(entry.PreviewPotion));
        AddCausalEffect(entry, CausalEffectKind.GeneratePotion, [entry.PreviewPotion]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardsSelected(CombatPredictionCardsSelectedEntry entry)
    {
        if (entry.PreviewCards is not [var card, ..] || !MatchesPlayer(card.Owner))
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.CardBundle(entry.PreviewCards));
        AddCausalEffect(entry, CausalEffectKind.SelectCards, entry.PreviewCards);
        _highlightedCards.UnionWith(entry.SourceCards);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleDrawPileAutoPlay(CombatPredictionAutoPlayFromDrawPileEntry entry)
    {
        if (!MatchesPlayer(entry.Card.Preview.Owner))
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.Card(entry.Card.Preview));
        AddCausalEffect(entry, CausalEffectKind.PlayCard, [entry.Card.Preview]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardDrawn(CombatPredictionCardDrawnEntry entry)
    {
        if (!MatchesPlayer(entry.Card.Preview.Owner))
        {
            return null;
        }

        var resolved = _history.GetResolvedEntry<CombatPredictionCardDrawResolvedEntry>(entry);
        AddHoverTip(PredictionHoverTipFactory.Card(resolved.PreviewCard));
        AddCausalEffect(entry, CausalEffectKind.DrawCards, [resolved.PreviewCard]);
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleCardCostsRandomized(CombatPredictionCardCostsRandomizedEntry entry)
    {
        if (entry.PreviewCards is not [var card, ..] || !MatchesPlayer(card.Owner))
        {
            return null;
        }

        // Snecko Oil's final full-hand snapshot supersedes the draw tips recorded before cost randomization.
        _hoverTips.Clear();
        _hoverTips.AddRange(entry.PreviewCards.ToPredictionHoverTips());
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleOrbChanneled(CombatPredictionOrbChanneledEntry entry)
    {
        // Deterministic channels are already described by their source; only random orb identities need a tip.
        if (!MatchesPlayer(entry.Orb.Owner) ||
            entry.Trace!.Source is not (Chaos or TrashToTreasurePower or TheMagician))
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.Orb(entry.Orb));
        AddCausalEffect(entry, CausalEffectKind.ChannelOrbs, [entry.Orb]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleDamage(CombatPredictionDamageReceivedEntry entry)
    {
        if (!DamagePredictionProjector.ShouldIncludeEntry(entry))
        {
            return null;
        }

        _damageEntries.Add(entry);
        return entry;
    }

    private CombatPredictionProjection FinalizeProjection()
    {
        var damagePrediction = DamagePredictionProjector.Project(_damageEntries);
        var risk = _history.GetRisk(_relevantEntries);
        if (_causalTips.Build() is { } causalTip)
        {
            _hoverTips.Insert(0, causalTip);
        }
        _hoverTips.AddRange(risk.ToHoverTips());

        return new CombatPredictionProjection(
            _hoverTips,
            damagePrediction,
            _highlightedCards,
            risk);
    }

    private void AddHoverTip(IHoverTip hoverTip)
    {
        if (_rootFrame is null && hoverTip.IsPredictionCardHoverTip())
        {
            return;
        }

        if (_hoverTips.Count < MaxHoverTips)
        {
            _hoverTips.Add(hoverTip);
        }
    }

    private void AddCausalEffect(
        CombatPredictionHistoryEntry entry,
        CausalEffectKind effect,
        IEnumerable<AbstractModel> results)
    {
        _causalTips.AddEffect(entry, effect, results);
    }

    private delegate CombatPredictionHistoryEntry? EntryHandler(CombatPredictionHistoryEntry entry);

    /// <summary>
    /// Describes the projection rule for a single semantic history entry type.
    /// </summary>
    /// <param name="Handler">Projects an enabled entry and returns the history boundary consumed for shared risk.</param>
    /// <param name="Enabled">Whether the entry type is enabled by the current settings.</param>
    private readonly record struct EntryProjectionRule(EntryHandler Handler, bool Enabled);
}
