using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class CompatibilityUtils
{
    /// <summary>
    /// Determines whether a runtime type belongs to the base game rather than a loaded Mod.
    /// </summary>
    public static bool IsBaseGameType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        _ = AssemblyInfo.ModForType(type, out var isBaseGame);
        return isBaseGame;
    }

    /// <summary>
    /// Determines whether prediction is allowed for a runtime type under the current compatibility settings.
    /// </summary>
    public static bool IsPredictionAllowedForType(Type type)
    {
        return !ModData.Settings.PredictBaseGameCardsOnly || IsBaseGameType(type);
    }

    /// <summary>
    /// Applies the Hook-listener compatibility filter while preserving listener order.
    /// </summary>
    public static IEnumerable<AbstractModel> FilterHookListeners(IEnumerable<AbstractModel> listeners)
    {
        return ModData.Settings.InvokeBaseGameHookListenersOnly
            ? listeners.Where(listener => IsBaseGameType(listener.GetType()))
            : listeners;
    }
}
