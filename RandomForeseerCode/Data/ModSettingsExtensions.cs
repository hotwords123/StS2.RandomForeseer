using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.Data;

internal static class ModSettingsExtensions
{
    extension(ModSettings settings)
    {
        public bool IsPredictionEnabled => GetCurrentNetGameType() switch
        {
            NetGameType.Singleplayer => settings.SingleplayerPredictionEnabled,
            NetGameType.Host or NetGameType.Client => settings.MultiplayerPredictionEnabled,
            _ => false
        };

        public Color DamagePredictionHealthBarColorValue =>
            ModSettingsColorControl.TryDeserializeColorForSettings(settings.DamagePredictionHealthBarColor, out var color)
                ? color
                : new(ModSettings.Default.DamagePredictionHealthBarColor);

        public bool Allows(PredictionFairness fairness)
        {
            if (!settings.FairModeEnabled)
            {
                return true;
            }

            return fairness switch
            {
                PredictionFairness.Fair => true,
                PredictionFairness.UnfairInSingleplayer => GetCurrentNetGameType() != NetGameType.Singleplayer,
                PredictionFairness.UnfairInAllModes => false,
                _ => throw new ArgumentOutOfRangeException(nameof(fairness), fairness, "Unknown fairness value")
            };
        }
    }

    private static NetGameType GetCurrentNetGameType()
    {
        return RunManager.Instance is { IsInProgress: true, NetService: { } netService }
            ? netService.Type
            : NetGameType.None;
    }
}
