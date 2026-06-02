using System.Reflection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Utils.Persistence;

namespace RandomForeseer;

internal sealed class RandomForeseerSettingsData
{
    public bool EnableSingleplayerPrediction { get; set; } = true;

    public bool EnableMultiplayerPrediction { get; set; } = true;

    public bool EnableFairMode { get; set; } = true;

    public bool EnableTransformPrediction { get; set; } = true;

    public bool EnablePotionCardPrediction { get; set; } = true;

    public bool EnableCombatCardPrediction { get; set; } = true;

    public bool EnableCombatCardSelectionPrediction { get; set; } = true;

    public bool EnableDriftwoodRerollPrediction { get; set; } = true;

    public bool EnableOutOfCombatRelicPrediction { get; set; } = true;

    public bool EnableEventOptionPrediction { get; set; } = true;

    public int SlipperyBridgeRerollPreviewCount { get; set; } = 5;

    public bool EnableFrozenEye { get; set; } = true;

    public bool EnableAncientEventDebugReroll { get; set; }
}

internal static class RandomForeseerSettings
{
    private const string DataKey = "settings";
    private const string EnableSingleplayerPredictionKey = "enable_singleplayer_prediction";
    private const string EnableMultiplayerPredictionKey = "enable_multiplayer_prediction";
    private const string EnableFairModeKey = "enable_fair_mode";
    private const string EnableTransformPredictionKey = "enable_transform_prediction";
    private const string EnablePotionCardPredictionKey = "enable_potion_card_prediction";
    private const string EnableCombatCardPredictionKey = "enable_combat_card_prediction";
    private const string EnableCombatCardSelectionPredictionKey = "enable_combat_card_selection_prediction";
    private const string EnableDriftwoodRerollPredictionKey = "enable_driftwood_reroll_prediction";
    private const string EnableOutOfCombatRelicPredictionKey = "enable_out_of_combat_relic_prediction";
    private const string EnableEventOptionPredictionKey = "enable_event_option_prediction";
    private const string SlipperyBridgeRerollPreviewCountKey = "slippery_bridge_reroll_preview_count";
    private const string EnableFrozenEyeKey = "enable_frozen_eye";
    private const string EnableAncientEventDebugRerollKey = "enable_ancient_event_debug_reroll";

    private static bool _isDataRegistered;

    private static readonly I18N SettingsLocalization =
        RitsuLibFramework.CreateModLocalization(
            Entry.ModId,
            "settings",
            pckFolders: [$"{Entry.ResPath}/localization/settings"],
            resourceAssembly: Assembly.GetExecutingAssembly());

    private static readonly IModSettingsValueBinding<bool> EnableSingleplayerPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableSingleplayerPrediction,
            (settings, value) => settings.EnableSingleplayerPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableMultiplayerPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableMultiplayerPrediction,
            (settings, value) => settings.EnableMultiplayerPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableFairModeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableFairMode,
            (settings, value) => settings.EnableFairMode = value);

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

    private static readonly IModSettingsValueBinding<bool> EnableCombatCardSelectionPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatCardSelectionPrediction,
            (settings, value) => settings.EnableCombatCardSelectionPrediction = value);

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

    private static readonly IModSettingsValueBinding<bool> EnableEventOptionPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableEventOptionPrediction,
            (settings, value) => settings.EnableEventOptionPrediction = value);

    private static readonly IModSettingsValueBinding<int> SlipperyBridgeRerollPreviewCountBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, int>(
            Entry.ModId,
            DataKey,
            settings => settings.SlipperyBridgeRerollPreviewCount,
            (settings, value) => settings.SlipperyBridgeRerollPreviewCount = value);

    private static readonly IModSettingsValueBinding<bool> EnableAncientEventDebugRerollBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableAncientEventDebugReroll,
            (settings, value) => settings.EnableAncientEventDebugReroll = value);

    public static bool EnableSingleplayerPrediction => EnableSingleplayerPredictionBinding.Read();

    public static bool EnableMultiplayerPrediction => EnableMultiplayerPredictionBinding.Read();

    public static bool EnableFairMode => EnableFairModeBinding.Read();

    public static bool EnableTransformPrediction => EnableTransformPredictionBinding.Read();

    public static bool EnablePotionCardPrediction => EnablePotionCardPredictionBinding.Read();

    public static bool EnableCombatCardPrediction => EnableCombatCardPredictionBinding.Read();

    public static bool EnableCombatCardSelectionPrediction => EnableCombatCardSelectionPredictionBinding.Read();

    public static bool EnableDriftwoodRerollPrediction => EnableDriftwoodRerollPredictionBinding.Read();

    public static bool EnableOutOfCombatRelicPrediction => EnableOutOfCombatRelicPredictionBinding.Read();

    public static bool EnableEventOptionPrediction => EnableEventOptionPredictionBinding.Read();

    public static int SlipperyBridgeRerollPreviewCount => Math.Clamp(SlipperyBridgeRerollPreviewCountBinding.Read(), 1, 10);

    public static bool EnableFrozenEye => EnableFrozenEyeBinding.Read();

    public static bool EnableAncientEventDebugReroll => EnableAncientEventDebugRerollBinding.Read();

    public static bool IsPredictionFeatureEnabled(bool featureEnabled)
    {
        if (!featureEnabled)
        {
            return false;
        }

        return GetCurrentNetGameType() switch
        {
            NetGameType.Singleplayer => EnableSingleplayerPrediction,
            NetGameType.Host or NetGameType.Client => EnableMultiplayerPrediction,
            _ => true
        };
    }

    public static bool IsFairPredictionAllowed(PredictionFairness fairness)
    {
        if (!EnableFairMode)
        {
            return true;
        }

        return fairness switch
        {
            PredictionFairness.Fair => true,
            PredictionFairness.UnfairInSingleplayer => GetCurrentNetGameType() != NetGameType.Singleplayer,
            PredictionFairness.UnfairInAllModes => false,
            _ => true
        };
    }

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

            page.AddSection("general_prediction", section =>
            {
                section.WithTitle(Text("section.general_prediction.title", "General settings"));
                section.WithDescription(Text(
                    "section.general_prediction.description",
                    "Controls prediction availability across game modes."));

                section.AddToggle(
                    EnableSingleplayerPredictionKey,
                    Text("toggle.enable_singleplayer_prediction.label", "Enable singleplayer prediction"),
                    EnableSingleplayerPredictionBinding,
                    Text(
                        "toggle.enable_singleplayer_prediction.description",
                        "When disabled, prediction features do not take effect in singleplayer even if their individual settings are enabled."),
                    () => true);

                section.AddToggle(
                    EnableMultiplayerPredictionKey,
                    Text("toggle.enable_multiplayer_prediction.label", "Enable multiplayer prediction"),
                    EnableMultiplayerPredictionBinding,
                    Text(
                        "toggle.enable_multiplayer_prediction.description",
                        "When disabled, prediction features do not take effect in multiplayer even if their individual settings are enabled."),
                    () => true);

                section.AddToggle(
                    EnableFairModeKey,
                    Text("toggle.enable_fair_mode.label", "Enable fair mode"),
                    EnableFairModeBinding,
                    Text(
                        "toggle.enable_fair_mode.description",
                        "When enabled, prediction is limited to information that can be obtained through Save & Load."),
                    () => true);
            });

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

                section.AddToggle(
                    EnableEventOptionPredictionKey,
                    Text("toggle.enable_event_option_prediction.label", "Predict event option results"),
                    EnableEventOptionPredictionBinding,
                    Text(
                        "toggle.enable_event_option_prediction.description",
                        "When enabled, non-Ancient event option tooltips show immediate random results such as rewards, upgrades, and offered follow-up choices."),
                    () => true);

                section.AddIntSlider(
                    SlipperyBridgeRerollPreviewCountKey,
                    Text("slider.slippery_bridge_reroll_preview_count.label", "Slippery Bridge reroll previews"),
                    SlipperyBridgeRerollPreviewCountBinding,
                    1,
                    10,
                    1,
                    value => value.ToString(),
                    Text(
                        "slider.slippery_bridge_reroll_preview_count.description",
                        "How many future Hold On rerolls to preview for Slippery Bridge."));
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
                    EnableCombatCardSelectionPredictionKey,
                    Text("toggle.enable_combat_card_selection_prediction.label", "Predict combat card selection"),
                    EnableCombatCardSelectionPredictionBinding,
                    Text(
                        "toggle.enable_combat_card_selection_prediction.description",
                        "When enabled, combat card tooltips and hand highlights show the exact existing cards the current RNG will select."),
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

            page.AddSection("out_of_combat_relic_debug", section =>
            {
                section.WithTitle(Text("section.out_of_combat_relic_debug.title", "Out-of-combat relic prediction"));
                section.WithDescription(Text(
                    "section.out_of_combat_relic_debug.description",
                    "Debug tools for out-of-combat relic prediction."));

                section.AddButton(
                    "offer_predicted_non_ancient_relics",
                    Text("button.offer_predicted_non_ancient_relics.label", "Offer predicted non-Ancient relics"),
                    Text(
                        "button.offer_predicted_non_ancient_relics.text",
                        "Offer"),
                    Debug.OutOfCombatRelicDebugRewards.OfferPredictedNonAncientRelics,
                    ModSettingsButtonTone.Danger,
                    Text(
                        "button.offer_predicted_non_ancient_relics.description",
                        "Only works during a run. Opens a reward screen with the non-Ancient relics covered by pickup prediction. This can change game content."));
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

    private static NetGameType GetCurrentNetGameType()
    {
        return RunManager.Instance.IsInProgress
            ? RunManager.Instance.NetService.Type
            : NetGameType.None;
    }
}

internal enum PredictionFairness
{
    Fair,
    UnfairInSingleplayer,
    UnfairInAllModes
}
