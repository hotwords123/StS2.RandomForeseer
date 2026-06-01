using System.Reflection;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Utils.Persistence;

namespace RandomForeseer;

internal sealed class RandomForeseerSettingsData
{
    public bool EnableTransformPrediction { get; set; } = true;

    public bool EnablePotionCardPrediction { get; set; } = true;

    public bool EnableCombatCardPrediction { get; set; } = true;

    public bool EnableDriftwoodRerollPrediction { get; set; } = true;

    public bool EnableOutOfCombatRelicPrediction { get; set; } = true;

    public bool EnableFrozenEye { get; set; } = true;

    public bool EnableAncientEventDebugReroll { get; set; }
}

internal static class RandomForeseerSettings
{
    private const string DataKey = "settings";
    private const string EnableTransformPredictionKey = "enable_transform_prediction";
    private const string EnablePotionCardPredictionKey = "enable_potion_card_prediction";
    private const string EnableCombatCardPredictionKey = "enable_combat_card_prediction";
    private const string EnableDriftwoodRerollPredictionKey = "enable_driftwood_reroll_prediction";
    private const string EnableOutOfCombatRelicPredictionKey = "enable_out_of_combat_relic_prediction";
    private const string EnableFrozenEyeKey = "enable_frozen_eye";
    private const string EnableAncientEventDebugRerollKey = "enable_ancient_event_debug_reroll";

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

    private static readonly IModSettingsValueBinding<bool> EnablePotionCardPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePotionCardPrediction,
            (settings, value) => settings.EnablePotionCardPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableFrozenEyeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableFrozenEye,
            (settings, value) => settings.EnableFrozenEye = value);

    private static readonly IModSettingsValueBinding<bool> EnableCombatCardPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatCardPrediction,
            (settings, value) => settings.EnableCombatCardPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableDriftwoodRerollPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableDriftwoodRerollPrediction,
            (settings, value) => settings.EnableDriftwoodRerollPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableOutOfCombatRelicPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableOutOfCombatRelicPrediction,
            (settings, value) => settings.EnableOutOfCombatRelicPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableAncientEventDebugRerollBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableAncientEventDebugReroll,
            (settings, value) => settings.EnableAncientEventDebugReroll = value);

    public static bool EnableTransformPrediction => EnableTransformPredictionBinding.Read();

    public static bool EnablePotionCardPrediction => EnablePotionCardPredictionBinding.Read();

    public static bool EnableCombatCardPrediction => EnableCombatCardPredictionBinding.Read();

    public static bool EnableDriftwoodRerollPrediction => EnableDriftwoodRerollPredictionBinding.Read();

    public static bool EnableOutOfCombatRelicPrediction => EnableOutOfCombatRelicPredictionBinding.Read();

    public static bool EnableFrozenEye => EnableFrozenEyeBinding.Read();

    public static bool EnableAncientEventDebugReroll => EnableAncientEventDebugRerollBinding.Read();

    public static void Register()
    {
        RegisterData();

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithModDisplayName(Text("mod.name", "Random Foreseer"));
            page.WithTitle(Text("page.title", "Random Foreseer"));
            page.WithSortOrder(0);
            page.WithDescription(Text(
                "page.description",
                "Configure prediction features for random outcomes. Some settings may require Save & Load to take effect."));

            page.AddSection("out_of_combat_prediction", section =>
            {
                section.WithTitle(Text("section.out_of_combat_prediction.title", "Out-of-combat prediction"));
                section.WithDescription(Text(
                    "section.out_of_combat_prediction.description",
                    "Controls random outcomes shown outside combat."));

                section.AddToggle(
                    EnableTransformPredictionKey,
                    Text("toggle.enable_transform_prediction.label", "Predict transform results"),
                    EnableTransformPredictionBinding,
                    Text(
                        "toggle.enable_transform_prediction.description",
                        "When enabled, deck transform confirmation previews show the exact card the current RNG will produce."),
                    () => true);

                section.AddToggle(
                    EnableDriftwoodRerollPredictionKey,
                    Text("toggle.enable_driftwood_reroll_prediction.label", "Predict Driftwood rerolls"),
                    EnableDriftwoodRerollPredictionBinding,
                    Text(
                        "toggle.enable_driftwood_reroll_prediction.description",
                        "When enabled, Driftwood's card reward reroll button shows the exact cards the reroll will offer."),
                    () => true);

                section.AddToggle(
                    EnableOutOfCombatRelicPredictionKey,
                    Text("toggle.enable_out_of_combat_relic_prediction.label", "Predict out-of-combat relic results"),
                    EnableOutOfCombatRelicPredictionBinding,
                    Text(
                        "toggle.enable_out_of_combat_relic_prediction.description",
                        "When enabled, out-of-combat relic tooltips show immediate random results such as cards, relics, potions, curses, and transforms."),
                    () => true);
            });

            page.AddSection("in_combat_prediction", section =>
            {
                section.WithTitle(Text("section.in_combat_prediction.title", "In-combat prediction"));
                section.WithDescription(Text(
                    "section.in_combat_prediction.description",
                    "Controls random outcomes shown during combat."));

                section.AddToggle(
                    EnablePotionCardPredictionKey,
                    Text("toggle.enable_potion_card_prediction.label", "Predict potion card results"),
                    EnablePotionCardPredictionBinding,
                    Text(
                        "toggle.enable_potion_card_prediction.description",
                        "When enabled, random-card potion tooltips show the exact cards the current RNG will produce."),
                    () => true);

                section.AddToggle(
                    EnableCombatCardPredictionKey,
                    Text("toggle.enable_combat_card_prediction.label", "Predict combat card generation"),
                    EnableCombatCardPredictionBinding,
                    Text(
                        "toggle.enable_combat_card_prediction.description",
                        "When enabled, combat card tooltips show the exact random cards the current RNG will generate."),
                    () => true);

                section.AddToggle(
                    EnableFrozenEyeKey,
                    Text("toggle.enable_frozen_eye.label", "Enable Frozen Eye"),
                    EnableFrozenEyeBinding,
                    Text(
                        "toggle.enable_frozen_eye.description",
                        "When enabled, the combat draw pile view shows cards in draw order."),
                    () => true);
            });

        });

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithModDisplayName(Text("mod.name", "Random Foreseer"));
            page.WithTitle(Text("page.debug.title", "Debug"));
            page.WithSortOrder(1);
            page.WithDescription(Text(
                "page.debug.description",
                "Controls tools intended for development and verification."));

            page.AddSection("ancient_event_debug", section =>
            {
                section.WithTitle(Text("section.ancient_event_debug.title", "Ancient events"));
                section.WithDescription(Text(
                    "section.ancient_event_debug.description",
                    "Debug tools for Ancient event pages."));

                section.AddToggle(
                    EnableAncientEventDebugRerollKey,
                    Text("toggle.enable_ancient_event_debug_reroll.label", "Enable Ancient event debug reroll"),
                    EnableAncientEventDebugRerollBinding,
                    Text(
                        "toggle.enable_ancient_event_debug_reroll.description",
                        "When enabled, Ancient event pages show a debug Reroll button that regenerates the current option set."),
                    () => true);
            });
        }, "debug");
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
