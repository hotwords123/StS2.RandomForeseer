using RandomForeseer.Data.Migrations;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Utils.Persistence;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace RandomForeseer.Data;

internal static class ModData
{
    public const string SettingsKey = "settings";
    public const string SettingsFileName = "settings.json";

    private static readonly ModDataStore Store = ModDataStore.For(Entry.ModId);

    private static bool _isRegistered;

    public static ModSettings Settings => Store.Get<ModSettings>(SettingsKey);

    public static void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            Store.Register(
                SettingsKey,
                SettingsFileName,
                SaveScope.Global,
                () => new ModSettings(),
                autoCreateIfMissing: true,
                migrationConfig: new ModDataMigrationConfig
                {
                    CurrentDataVersion = ModSettings.CurrentSchemaVersion,
                    MinimumSupportedDataVersion = 0,
                },
                migrations:
                [
                    new ModSettingsV0ToV1Migration(),
                    new ModSettingsV1ToV2Migration(),
                    new ModSettingsV2ToV3Migration()
                ]);
        }

        _isRegistered = true;
    }
}
