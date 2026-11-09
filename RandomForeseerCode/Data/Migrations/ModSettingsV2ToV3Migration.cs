using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace RandomForeseer.Data.Migrations;

internal sealed class ModSettingsV2ToV3Migration : IMigration
{
    public int FromVersion => 2;

    public int ToVersion => 3;

    public bool Migrate(JsonObject data)
    {
        data[nameof(ModSettings.ShowDriftWarnings)] = false;
        return true;
    }
}
