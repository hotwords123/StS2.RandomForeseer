using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;
using RandomForeseer.RandomForeseerCode.Settings;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.InCombat;

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
