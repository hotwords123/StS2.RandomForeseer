using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors.Hooks.CardReward;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors;

// Prediction-facing facade for mirrored out-of-combat hooks. It owns context construction,
// modifier enumeration, and hook phase order while registries remain implementation details.
internal static class HookMirrors
{
    // Mirrors Hook.TryModifyCardRewardOptions followed by its Late phase.
    public static bool TryModifyCardRewardOptions(
        RunPredictionContext runContext,
        List<CardCreationResult> results,
        CardCreationOptions options,
        out List<AbstractModel> modifiers,
        IEnumerable<AbstractModel>? extraListeners = null)
    {
        var context = new TryModifyCardRewardOptionsMirrorContext
        {
            RunContext = runContext,
            Results = results,
            Options = options
        };
        var extraListenerList = extraListeners?.ToList() ?? [];
        modifiers = [];

        foreach (var modifier in runContext.RunState.IterateHookListeners(null).Concat(extraListenerList))
        {
            if (TryModifyCardRewardOptionsMirrors.Invoke(modifier, context))
            {
                modifiers.Add(modifier);
            }
        }

        foreach (var modifier in runContext.RunState.IterateHookListeners(null).Concat(extraListenerList))
        {
            if (TryModifyCardRewardOptionsMirrors.InvokeLate(modifier, context))
            {
                modifiers.Add(modifier);
            }
        }

        return modifiers.Count > 0;
    }
}
