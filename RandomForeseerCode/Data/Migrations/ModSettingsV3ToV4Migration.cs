using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace RandomForeseer.Data.Migrations;

internal sealed class ModSettingsV3ToV4Migration : IMigration
{
    public int FromVersion => 3;

    public int ToVersion => 4;

    public bool Migrate(JsonObject data)
    {
        var level = data.GetBoolean("ShowDriftWarnings") == true
            ? PredictionDriftWarningLevel.All
            : PredictionDriftWarningLevel.Reduced;
        data.SetIfMissing(nameof(ModSettings.PredictionDriftWarningLevel), JsonValue.Create((int)level));
        data.Remove("ShowDriftWarnings");
        return true;
    }
}
