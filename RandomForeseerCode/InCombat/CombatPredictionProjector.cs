using System.Diagnostics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

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
    PredictionRisk Risk);

/// <summary>
/// Streams a completed combat prediction history into the enabled HoverTip, damage, highlight, causal, and risk views.
/// </summary>
/// <remarks>
/// Feature gates are selected from the root action kind and applied independently to each entry. When chained
/// prediction is disabled, the first history entry owned by a nested action truncates the remaining visible timeline.
/// </remarks>
internal sealed class CombatPredictionProjector
{
    private const int MaxHoverTips = 10;

    private readonly CombatPredictionHistory _history;
    private readonly PredictionTraceFrame _rootFrame;

    private readonly Dictionary<Type, EntryProjectionRule> _rules = [];

    private readonly List<IHoverTip> _hoverTips = [];
    private readonly HashSet<CardModel> _highlightedCards = [];
    private readonly List<CombatPredictionDamageReceivedEntry> _damageEntries = [];
    private readonly List<CombatPredictionHistoryEntry> _relevantEntries = [];

    // Once an entry owned by a nested action triggers the chained-prediction gate, no later entry may contribute.
    private bool _projectionTruncated;

    private readonly CombatPredictionCausalTipBuilder _causalTips;

    /// <summary>
    /// Projects one completed combat-action history relative to its exact root action frame.
    /// </summary>
    /// <param name="history">The completed history produced by the same simulation as <paramref name="rootFrame"/>.</param>
    /// <param name="rootFrame">
    /// The root <see cref="PredictionActionKind.CardPlay"/> or <see cref="PredictionActionKind.PotionUse"/> frame.
    /// Its source is the original card or potion identity used by scope and causal classification.
    /// </param>
    /// <returns>A presentation projection containing only results accepted by enabled feature policies.</returns>
    /// <remarks>
    /// Callers must pass an action frame returned by the simulation entry point and must not combine a frame and
    /// history from different simulations. Entries are dispatched in timeline order; deferred entries use their
    /// resolved snapshot and completion boundary.
    /// </remarks>
    public static CombatPredictionProjection Project(CombatPredictionHistory history, PredictionTraceFrame rootFrame)
    {
        return new CombatPredictionProjector(history, rootFrame).Project();
    }

    private CombatPredictionProjector(CombatPredictionHistory history, PredictionTraceFrame rootFrame)
    {
        var action = rootFrame.Invocation.Action;
        if (action is not (PredictionActionKind.CardPlay or PredictionActionKind.PotionUse))
        {
            throw new ArgumentException(
                $"Root frame must be a card-play or potion-use action, but was {action}.",
                nameof(rootFrame));
        }

        _history = history;
        _rootFrame = rootFrame;

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
        var isCardPlay = _rootFrame.Invocation.Action switch
        {
            PredictionActionKind.CardPlay => true,
            PredictionActionKind.PotionUse => false,
            var action => throw new UnreachableException($"Unexpected action kind {action}.")
        };
        var settings = ModData.Settings;

        Register<CombatPredictionCardGeneratedEntry>(
            HandleCardGenerated,
            isCardPlay
                ? settings.CombatCardGenerationPredictionEnabled
                : settings.PotionCardGenerationPredictionEnabled);

        Register<CombatPredictionCardGenerationOptionsEntry>(
            HandleCardGenerationOptions,
            isCardPlay
                ? settings.CombatCardGenerationPredictionEnabled
                : settings.PotionCardGenerationPredictionEnabled);

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
            isCardPlay
                ? settings.CardDrawPredictionEnabled
                : settings.PotionDrawPredictionEnabled);

        Register<CombatPredictionCardCostsRandomizedEntry>(
            HandleCardCostsRandomized,
            isCardPlay
                ? settings.CardDrawPredictionEnabled
                : settings.PotionDrawPredictionEnabled);

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
    /// Entries outside the supplied root trace are ignored. When chained prediction is disabled, the first entry owned
    /// by a nested action truncates the remaining timeline even if that entry type has no projection rule.
    /// </remarks>
    private void Dispatch(CombatPredictionHistoryEntry entry)
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

    private CombatPredictionHistoryEntry? HandleCardGenerated(CombatPredictionCardGeneratedEntry entry)
    {
        if (entry.ResultKind is CardGenerationResultKind.Fixed)
        {
            return null;
        }

        var resolved = _history.GetResolvedEntry<CombatPredictionCardGenerationResolvedEntry>(entry);
        if (!resolved.PileAddSucceeded)
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.Card(resolved.Card.Preview));
        AddCausalEffect(entry, CausalEffectKind.GenerateCards, [resolved.Card.Preview]);
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleCardGenerationOptions(CombatPredictionCardGenerationOptionsEntry entry)
    {
        if (entry.Cards.Count == 0)
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.CardBundle([.. entry.Cards.SelectPreviews()]));
        AddCausalEffect(entry, CausalEffectKind.GenerateCards, entry.Cards.SelectPreviews());
        return entry;
    }

    private CombatPredictionHistoryEntry? HandlePotionGenerated(CombatPredictionPotionGeneratedEntry entry)
    {
        AddHoverTip(PredictionHoverTipFactory.Potion(entry.Potion));
        AddCausalEffect(entry, CausalEffectKind.GeneratePotion, [entry.Potion]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardsSelected(CombatPredictionCardsSelectedEntry entry)
    {
        if (entry.Cards.Count == 0)
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.CardBundle([.. entry.Cards.SelectPreviews()]));
        _highlightedCards.UnionWith(entry.Cards.SelectOriginals());
        AddCausalEffect(entry, CausalEffectKind.SelectCards, entry.Cards.SelectPreviews());
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleDrawPileAutoPlay(CombatPredictionAutoPlayFromDrawPileEntry entry)
    {
        AddHoverTip(PredictionHoverTipFactory.Card(entry.Card.Preview));
        AddCausalEffect(entry, CausalEffectKind.PlayCard, [entry.Card.Preview]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardDrawn(CombatPredictionCardDrawnEntry entry)
    {
        var resolved = _history.GetResolvedEntry<CombatPredictionCardDrawResolvedEntry>(entry);
        AddHoverTip(PredictionHoverTipFactory.Card(resolved.Card.Preview));
        AddCausalEffect(entry, CausalEffectKind.DrawCards, [resolved.Card.Preview]);
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleCardCostsRandomized(CombatPredictionCardCostsRandomizedEntry entry)
    {
        if (entry.Cards.Count == 0)
        {
            return null;
        }

        // Snecko Oil's final full-hand snapshot supersedes the draw tips recorded before cost randomization.
        _hoverTips.Clear();
        _hoverTips.AddRange(entry.Cards.SelectPreviews().ToPredictionHoverTips());
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleOrbChanneled(CombatPredictionOrbChanneledEntry entry)
    {
        // Deterministic channels are already described by their source; only random orb identities need a tip.
        if (entry.Trace!.Source is not (Chaos or TrashToTreasurePower))
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
