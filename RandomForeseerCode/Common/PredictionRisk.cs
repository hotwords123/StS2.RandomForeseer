using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common;

internal enum PredictionRiskReason
{
    MethodNotMirrored,
    MethodMirrorIncomplete,
    UnresolvedPlayerChoice,
    CardDrawLimitExceeded,
    OrbChannelLimitExceeded,
}

internal sealed record PredictionRisk(
    bool HasRisk,
    IReadOnlyList<AbstractModel> Models,
    IReadOnlySet<PredictionRiskReason> Reasons)
{
    public static PredictionRisk None { get; } = new(false, [], new HashSet<PredictionRiskReason>());
}
