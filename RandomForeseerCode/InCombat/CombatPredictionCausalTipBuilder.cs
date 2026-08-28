using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.RandomForeseerCode.Localization;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>
/// Collects causal lines for projected combat effects.
/// </summary>
/// <remarks>
/// Consecutive results are grouped by immutable trace frames and effect kind, so separate card replays remain
/// distinct and non-consecutive timeline effects are not reordered. The builder receives prediction model snapshots
/// and creates one localized text tip during projection finalization. <paramref name="rootFrame"/> must be the same
/// root action frame used to scope the projector entries supplied to <see cref="AddEffect"/>.
/// </remarks>
/// <param name="rootFrame">The exact root card-play or potion-use action frame used by the projector.</param>
internal sealed class CombatPredictionCausalTipBuilder(PredictionTraceFrame rootFrame)
{
    private const int MaxLines = 10;

    private readonly List<CausalGroup> _groups = [];

    /// <summary>
    /// Adds one semantic result accepted for presentation or retained as prerequisite causal context.
    /// </summary>
    /// <remarks>
    /// The entry must be the started/semantic entry rather than a deferred resolution entry so grouping follows the
    /// order perceived by the player. Models should be the snapshots used by the corresponding presentation result.
    /// </remarks>
    public void AddEffect(
        CombatPredictionHistoryEntry entry,
        CausalEffectKind effect,
        IEnumerable<AbstractModel> models)
    {
        if (entry.Trace is { } trace && TryGetCause(trace) is { } cause)
        {
            AddGroup(cause, effect, models);
        }
    }

    /// <summary>
    /// Finalizes the accumulated groups into one localized causal HoverTip.
    /// </summary>
    /// <returns>
    /// A causal tip when accepted results require listener, nested-action, or distinct effect-invocation attribution;
    /// otherwise <see langword="null"/> for one ordinary root effect.
    /// </returns>
    /// <remarks>This method must be called only after all relevant history entries have been added in timeline order.</remarks>
    public IHoverTip? Build()
    {
        if (_groups.All(IsRootDirectEffect) &&
            !_groups.Select(static group => group.SourceFrame).Distinct().Skip(1).Any())
        {
            return null;
        }

        var lines = _groups.Take(MaxLines).Select(FormatGroup).ToList();

        if (_groups.Count > MaxLines)
        {
            var more = ModLocalization.Text("causal_prediction.more");
            more.Add("Count", _groups.Count - MaxLines);
            lines.Add(more.GetFormattedText());
        }

        return PredictionHoverTipFactory.Text(GetRootTipKey(), description =>
        {
            description.Add("Lines", lines);
        });
    }

    private static CausalCause? TryGetCause(PredictionTraceFrame trace)
    {
        var sourceFrame = trace.FindOriginatingEffect();

        return sourceFrame is not null
            ? new CausalCause(sourceFrame, trace.Source == sourceFrame.Source ? null : trace)
            : null;
    }

    private void AddGroup(CausalCause cause, CausalEffectKind effect, IEnumerable<AbstractModel> models)
    {
        CausalGroup group;
        if (_groups is [.., var last] && last.IsMergeableWith(cause, effect))
        {
            group = _groups[^1];
        }
        else
        {
            group = new CausalGroup(cause, effect);
            _groups.Add(group);
        }

        group.Models.AddRange(models);
    }

    private bool IsRootDirectEffect(CausalGroup group)
    {
        return group.ListenerFrame is null && group.SourceFrame.FindOriginatingAction() == rootFrame;
    }

    private string FormatGroup(CausalGroup group)
    {
        var line = ModLocalization.Text($"causal_prediction.{GetEffectKey(group.Effect)}");
        line.Add("Source", group.Source.GetTitle());
        line.Add("Listener", group.Listener?.GetTitle() ?? string.Empty);
        line.Add("Models", [.. group.Models.Select(static model => model.GetTitle())]);
        return line.GetFormattedText();
    }

    private string GetRootTipKey()
    {
        return rootFrame.Invocation.Action switch
        {
            PredictionActionKind.CardPlay => "causal_prediction.card",
            PredictionActionKind.PotionUse => "causal_prediction.potion",
            var action => throw new InvalidOperationException($"Unexpected root action kind {action}.")
        };
    }

    private static string GetEffectKey(CausalEffectKind effect)
    {
        return effect switch
        {
            CausalEffectKind.ChannelOrbs => "channel_orbs",
            CausalEffectKind.DrawCards => "draw_cards",
            CausalEffectKind.GenerateCards => "generate_cards",
            CausalEffectKind.GeneratePotion => "generate_potion",
            CausalEffectKind.PlayCard => "play_card",
            CausalEffectKind.SelectCards => "select_cards",
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null)
        };
    }

    private sealed class CausalGroup(CausalCause cause, CausalEffectKind effect)
    {
        public CausalCause Cause { get; } = cause;

        public CausalEffectKind Effect { get; } = effect;

        public List<AbstractModel> Models { get; } = [];

        public PredictionTraceFrame SourceFrame => Cause.SourceFrame;

        public PredictionTraceFrame? ListenerFrame => Cause.ListenerFrame;

        public AbstractModel Source => Cause.Source;

        public AbstractModel? Listener => Cause.Listener;

        /// <summary>
        /// Determines whether this group can be merged with another group. Two groups are mergeable if they share
        /// the same source frame, listener model, and effect kind.
        /// </summary>
        public bool IsMergeableWith(CausalCause cause, CausalEffectKind effect)
        {
            return SourceFrame == cause.SourceFrame && Listener == cause.Listener && Effect == effect;
        }
    }

    private readonly record struct CausalCause(
        PredictionTraceFrame SourceFrame,
        PredictionTraceFrame? ListenerFrame)
    {
        public AbstractModel Source => SourceFrame.Source;

        public AbstractModel? Listener => ListenerFrame?.Source;
    }
}

/// <summary>
/// Semantic result kinds that can be described by the chained-effect projection.
/// </summary>
internal enum CausalEffectKind
{
    ChannelOrbs,
    DrawCards,
    GenerateCards,
    GeneratePotion,
    PlayCard,
    SelectCards
}
