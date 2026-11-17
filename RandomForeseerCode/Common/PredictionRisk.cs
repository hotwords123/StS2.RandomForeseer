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

    public abstract bool HasVisibleRisk { get; }

    /// <summary>Creates the presentation tips for this risk snapshot.</summary>
    public IEnumerable<IHoverTip> ToHoverTips() => HasVisibleRisk ? GetHoverTips() : [];

    protected static bool ShouldShow(PredictionRiskReason reason)
    {
        return ModData.Settings.PredictionDriftWarningLevel switch
        {
            PredictionDriftWarningLevel.Off => false,
            PredictionDriftWarningLevel.Reduced => reason is not
                (PredictionRiskReason.MethodNotMirrored or PredictionRiskReason.MethodMirrorIncomplete),
            PredictionDriftWarningLevel.All => true,
            _ => false
        };
    }

    protected abstract IReadOnlyList<IHoverTip> GetHoverTips();

    private sealed class EmptyPredictionRisk : PredictionRisk
    {
        public override bool HasRisk => false;

        public override bool HasVisibleRisk => false;

        protected override IReadOnlyList<IHoverTip> GetHoverTips() => [];
    }
}
