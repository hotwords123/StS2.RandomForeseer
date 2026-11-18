using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace RandomForeseer.Data.Migrations;

internal sealed class ModSettingsV3ToV4Migration : IMigration
{
    // Array indices are the persisted values in schema V3.
    private static readonly string[] HoverTipOrderNames =
    [
        nameof(PredictionHoverTipOrder.BeforeVanilla),
        nameof(PredictionHoverTipOrder.AfterVanilla)
    ];

    private static readonly string[] EndTurnDisplayModeNames =
    [
        nameof(EndTurnPredictionDisplayMode.ButtonHover),
        nameof(EndTurnPredictionDisplayMode.Always)
    ];

    public int FromVersion => 3;

    public int ToVersion => 4;

    public bool Migrate(JsonObject data)
    {
        var level = data.GetBoolean("ShowDriftWarnings") == true
            ? PredictionDriftWarningLevel.All
            : PredictionDriftWarningLevel.Reduced;
        data.SetIfMissing(nameof(ModSettings.PredictionDriftWarningLevel), JsonValue.Create(level.ToString()));
        data.Remove("ShowDriftWarnings");

        ConvertOrdinal(data, nameof(ModSettings.PredictionTextHoverTipOrder), HoverTipOrderNames);
        ConvertOrdinal(data, nameof(ModSettings.PredictionCardHoverTipOrder), HoverTipOrderNames);
        data.MoveProperty("EndTurnPredictionDisplayMode", nameof(ModSettings.EndTurnDamageIndicatorDisplayMode));
        ConvertOrdinal(data, nameof(ModSettings.EndTurnDamageIndicatorDisplayMode), EndTurnDisplayModeNames);
        ConvertOrdinal(data, nameof(ModSettings.EndTurnHealthBarForecastDisplayMode), EndTurnDisplayModeNames);
        return true;
    }

    private static void ConvertOrdinal(JsonObject data, string propertyName, string[] names)
    {
        if (data[propertyName] is not JsonValue value || !value.TryGetValue<int>(out var ordinal))
        {
            return;
        }

        if (ordinal >= 0 && ordinal < names.Length)
        {
            data[propertyName] = names[ordinal];
        }
        else
        {
            data.Remove(propertyName);
        }
    }
}
