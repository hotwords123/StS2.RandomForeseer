using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common.Hooks;

internal enum HookResultKind
{
    // The hook was recognized, but its runtime conditions did not require a preview change.
    Ignored,

    // The hook was recognized and prevents the predicted action from continuing.
    Blocked,

    // The handler mirrored the hook and changed the preview state.
    Applied,

    // The model overrides the hook, but no safe handler or original-call path is registered.
    Unsupported,

    // The hook is recognized, but only partially modeled; downstream state or RNG may diverge.
    DriftRisk
}

internal sealed record HookResult(HookResultKind Kind, AbstractModel Model)
{
    public bool IsPredictionRisk => Kind is HookResultKind.DriftRisk or HookResultKind.Unsupported;
}
