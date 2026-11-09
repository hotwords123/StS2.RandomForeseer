using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace RandomForeseer.Data.Migrations;

internal sealed class ModSettingsV1ToV2Migration : IMigration
{
    public int FromVersion => 1;

    public int ToVersion => 2;

    public bool Migrate(JsonObject data)
    {
        const string sourceName = nameof(ModSettings.RelicPickupPredictionEnabled);
        const string targetName = nameof(ModSettings.AncientRelicPickupPredictionEnabled);
        if (data.TryGetPropertyValue(sourceName, out var value))
        {
            data.SetIfMissing(targetName, value);
        }

        return true;
    }
}
