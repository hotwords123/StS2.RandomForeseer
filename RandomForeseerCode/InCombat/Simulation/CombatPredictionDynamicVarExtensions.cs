using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Telemetry;
using STS2RitsuLib.Cards.DynamicVars;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class CombatPredictionDynamicVarExtensions
{
    private delegate DynamicVar GetDynamicVarDelegate(CalculatedVar calculatedVar);

    private static readonly GetDynamicVarDelegate GetBaseVar =
        AccessTools.Method(typeof(CalculatedVar), "GetBaseVar").CreateDelegate<GetDynamicVarDelegate>();

    private static readonly GetDynamicVarDelegate GetExtraVar =
        AccessTools.Method(typeof(CalculatedVar), "GetExtraVar").CreateDelegate<GetDynamicVarDelegate>();

    public static decimal InvokeCalculate(
        this DynamicVar dynamicVar,
        CombatPredictionSimulator simulator,
        PredictedCard card,
        Creature? target)
    {
        return dynamicVar switch
        {
            CalculatedVar calculatedVar =>
                calculatedVar.InvokeCalculate(simulator, card, target),
            IComputedDynamicVar computedDynamicVar =>
                computedDynamicVar.InvokeCalculate(simulator, card, target),
            _ => dynamicVar.BaseValue
        };
    }

    public static decimal InvokeCalculate(
        this CalculatedVar calculatedVar,
        CombatPredictionSimulator simulator,
        PredictedCard card,
        Creature? target)
    {
        var multiplierCalc = calculatedVar._multiplierCalc
            ?? throw new InvalidOperationException("CalculatedVar simulation requires a multiplier calculation function.");

        using var _ = simulator.PushActionSource(card.Original, PredictionActionKind.DynamicVariableCalculation);
        simulator.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);

        try
        {
            // This may not be accurate since the combat state is not fully simulated, but it is the best
            // we can do without simulating the entire combat state.
            var num = multiplierCalc(card.Preview, target);
            var baseVar = GetBaseVar(calculatedVar);
            var extraVar = GetExtraVar(calculatedVar);
            return baseVar.BaseValue + extraVar.BaseValue * num;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"CalculatedVar simulation failed: {ex}");
            ModTelemetry.CaptureException(ex, "combat_dynamic_var_prediction", "calculate");
            return 0m;
        }
    }

    public static decimal InvokeCalculate(
        this IComputedDynamicVar computedDynamicVar,
        CombatPredictionSimulator simulator,
        PredictedCard card,
        Creature? target)
    {
        using var _ = simulator.PushActionSource(card.Original, PredictionActionKind.DynamicVariableCalculation);
        simulator.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);

        try
        {
            return computedDynamicVar.Calculate(target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"IComputedDynamicVar simulation failed: {ex}");
            ModTelemetry.CaptureException(ex, "combat_dynamic_var_prediction", "calculate");
            return 0m;
        }
    }
}
