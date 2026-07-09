using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class CombatPredictionDynamicVarExtensions
{
    private delegate DynamicVar GetDynamicVarDelegate(CalculatedVar calculatedVar);

    private static readonly GetDynamicVarDelegate GetBaseVar =
        AccessTools.Method(typeof(CalculatedVar), "GetBaseVar").CreateDelegate<GetDynamicVarDelegate>();

    private static readonly GetDynamicVarDelegate GetExtraVar =
        AccessTools.Method(typeof(CalculatedVar), "GetExtraVar").CreateDelegate<GetDynamicVarDelegate>();

    public static decimal SimulateCalculate(
        this CalculatedVar calculatedVar,
        CombatPredictionSimulator simulator,
        Creature? target)
    {
        simulator.MarkCurrentSourceRisky();

        if (calculatedVar._owner is not CardModel card)
        {
            Entry.Logger.Warn("CalculatedVar simulation skipped: owner is not a CardModel.");
            return 0m;
        }

        if (calculatedVar._multiplierCalc is not { } multiplierCalc)
        {
            Entry.Logger.Warn("CalculatedVar simulation skipped: multiplier calculation function is null.");
            return 0m;
        }

        try
        {
            // This may not be accurate since the combat state is not fully simulated, but it is the best
            // we can do without simulating the entire combat state.
            var num = multiplierCalc(card, target);
            var baseVar = GetBaseVar(calculatedVar);
            var extraVar = GetExtraVar(calculatedVar);
            return baseVar.BaseValue + extraVar.BaseValue * num;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"CalculatedVar simulation failed: {ex}");
            return 0m;
        }
    }
}
