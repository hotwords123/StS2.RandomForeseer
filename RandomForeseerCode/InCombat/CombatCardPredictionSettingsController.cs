using RandomForeseer.Data;
using RandomForeseer.InCombat.Mirrors.Cards.OnPlay;
using RandomForeseer.Settings;
using STS2RitsuLib.Settings;

namespace RandomForeseer.InCombat;

/// <summary>
/// Synchronizes live combat-card prediction policies that are owned by the settings page.
/// </summary>
internal static class CombatCardPredictionSettingsController
{
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        SyncInferenceSetting();
        ModSettingsBindingWriteEvents.ValueWritten += OnSettingsValueWritten;
        _isRegistered = true;
    }

    private static void OnSettingsValueWritten(IModSettingsBinding binding)
    {
        if (ReferenceEquals(binding, SettingsUiBindings.InferCardOnPlayEffectsEnabled))
        {
            SyncInferenceSetting();
        }
        else if (ReferenceEquals(binding, SettingsUiBindings.PredictBaseGameCardsOnly))
        {
            CardOnPlayMirrors.InvalidateLookupCache();
        }
    }

    private static void SyncInferenceSetting()
    {
        CardOnPlayMirrors.AllowInference = ModData.Settings.InferCardOnPlayEffectsEnabled;
    }
}
