using MegaCrit.Sts2.Core.HoverTips;
using RandomForeseer.Data;

namespace RandomForeseer.Common;

internal enum PredictionRiskReason
{
    MethodNotMirrored,
    MethodMirrorIncomplete,
    UnresolvedPlayerChoice,
    CardDrawLimitExceeded,
    OrbChannelLimitExceeded,
}

internal abstract class PredictionRisk
{
    public static PredictionRisk None { get; } = new EmptyPredictionRisk();

    public abstract bool HasRisk { get; }

    /// <summary>Creates the presentation tips for this risk snapshot.</summary>
    public IEnumerable<IHoverTip> ToHoverTips()
    {
        return HasRisk && ModData.Settings.ShowDriftWarnings
            ? GetHoverTips()
            : [];
    }

    protected abstract IEnumerable<IHoverTip> GetHoverTips();

    private sealed class EmptyPredictionRisk : PredictionRisk
    {
        public override bool HasRisk => false;

        protected override IEnumerable<IHoverTip> GetHoverTips() => [];
    }
}
