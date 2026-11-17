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

internal interface IPredictionRisk
{
    bool HasRisk { get; }

    bool HasVisibleRisk { get; }

    /// <summary>Creates the presentation tips for this risk snapshot.</summary>
    IReadOnlyList<IHoverTip> ToHoverTips();
}

internal sealed class EmptyPredictionRisk : IPredictionRisk
{
    public static EmptyPredictionRisk Instance { get; } = new();

    public bool HasRisk => false;

    public bool HasVisibleRisk => false;

    public IReadOnlyList<IHoverTip> ToHoverTips() => [];
}

internal static class PredictionRiskReasonExtensions
{
    extension(PredictionRiskReason reason)
    {
        public bool ShouldShow(ModSettings settings)
        {
            return settings.PredictionDriftWarningLevel switch
            {
                PredictionDriftWarningLevel.Off => false,
                PredictionDriftWarningLevel.Reduced => reason is not
                    (PredictionRiskReason.MethodNotMirrored or PredictionRiskReason.MethodMirrorIncomplete),
                PredictionDriftWarningLevel.All => true,
                _ => false
            };
        }
    }
}
