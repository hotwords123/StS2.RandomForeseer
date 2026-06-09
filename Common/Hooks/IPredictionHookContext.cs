using RandomForeseer.Common;

namespace RandomForeseer.Common.Hooks;

internal interface IPredictionHookContext
{
    PredictionRiskTracker RiskTracker { get; }
}
