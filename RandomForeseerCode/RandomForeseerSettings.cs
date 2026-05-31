using System.Reflection;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Utils.Persistence;

namespace RandomForeseer;

internal sealed class RandomForeseerSettingsData
{
    public bool EnableTransformPrediction { get; set; } = true;
}

internal static class RandomForeseerSettings
{
    private const string DataKey = "settings";
    private const string EnableTransformPredictionKey = "enable_transform_prediction";

    private static bool _isDataRegistered;

    private static readonly I18N SettingsLocalization =
        RitsuLibFramework.CreateModLocalization(
            Entry.ModId,
            "settings",
            pckFolders: [$"{Entry.ResPath}/localization/settings"],
            resourceAssembly: Assembly.GetExecutingAssembly());

    private static readonly IModSettingsValueBinding<bool> EnableTransformPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableTransformPrediction,
            (settings, value) => settings.EnableTransformPrediction = value);

    public static bool EnableTransformPrediction => EnableTransformPredictionBinding.Read();

    public static void Register()
    {
        RegisterData();

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithModDisplayName(Text("mod.name", "Random Foreseer"));
            page.WithTitle(Text("page.title", "Random Foreseer"));
            page.WithDescription(Text(
                "page.description",
                "Configure prediction features for random outcomes."));

            page.AddSection("prediction", section =>
            {
                section.WithTitle(Text("section.prediction.title", "Prediction"));
                section.WithDescription(Text(
                    "section.prediction.description",
                    "Controls which random outcomes are shown before confirmation."));

                section.AddToggle(
                    EnableTransformPredictionKey,
                    Text("toggle.enable_transform_prediction.label", "Predict transform results"),
                    EnableTransformPredictionBinding,
                    Text(
                        "toggle.enable_transform_prediction.description",
                        "When enabled, deck transform confirmation previews show the exact card the current RNG will produce."),
                    () => true);
            });
        });
    }

    private static void RegisterData()
    {
        if (_isDataRegistered)
        {
            return;
        }

        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            RitsuLibFramework.GetDataStore(Entry.ModId).Register(
                DataKey,
                "settings.json",
                SaveScope.Global,
                () => new RandomForeseerSettingsData(),
                autoCreateIfMissing: true,
                migrationConfig: null,
                migrations: null);
        }

        _isDataRegistered = true;
    }

    private static ModSettingsText Text(string key, string fallback)
    {
        return ModSettingsText.I18N(SettingsLocalization, key, fallback);
    }
}
