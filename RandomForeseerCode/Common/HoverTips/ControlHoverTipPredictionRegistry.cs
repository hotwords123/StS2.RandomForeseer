using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using RandomForeseer.InCombat;
using RandomForeseer.Integrations.LemonSpire;
using RandomForeseer.OutOfCombat;
using RandomForeseer.Telemetry;

namespace RandomForeseer.Common.HoverTips;

/// <summary>
/// Collects prediction HoverTips from registered control providers.
/// </summary>
/// <remarks>
/// Providers are invoked in registration order. A provider failure is logged and isolated, so other providers still
/// contribute results; callers should therefore treat the returned list as best-effort prediction output.
/// </remarks>
internal static class ControlHoverTipPredictionRegistry
{
    private static readonly (string Name, Func<Control, IReadOnlyList<IHoverTip>> GetHoverTips)[] Providers =
    [
        ("event option", EventOptionHoverTips.GetHoverTips),
        ("merchant entry", MerchantEntryHoverTips.GetHoverTips),
        ("transform selection", TransformSelectionHoverTips.GetHoverTips),
        ("treasure room relic", TreasureRoomRelicHoverTips.GetHoverTips),
        ("relic choice selection", RelicChoiceSelectionHoverTips.GetHoverTips),
        ("rest site", RestSiteHoverTips.GetHoverTips),
        ("combat transform selected holder", CombatTransformSelectedHoverTips.GetHoverTips),
        ("card reward alternative", CardRewardAlternativeButtonHoverTips.GetHoverTips),
        ("lemonSpire", LemonSpireControlHoverTips.GetHoverTips)
    ];

    /// <summary>
    /// Invokes all registered providers and concatenates their successful results.
    /// </summary>
    /// <remarks>
    /// Provider exceptions are logged and skipped. This method does not deduplicate or otherwise normalize the
    /// returned tips; the eventual HoverTip presentation pipeline owns those responsibilities.
    /// </remarks>
    /// <param name="owner">Hovered control passed to every registered provider.</param>
    /// <returns>Tips returned by providers that completed successfully.</returns>
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        List<IHoverTip> predictionTips = [];
        foreach (var (name, getHoverTips) in Providers)
        {
            try
            {
                predictionTips.AddRange(getHoverTips(owner));
            }
            catch (Exception ex)
            {
                var inputType = owner.GetType();
                Entry.Logger.Warn($"Hover tip prediction provider '{name}' failed for {inputType}: {ex}");
                ModTelemetry.CaptureException(
                    ex,
                    "prediction_hover_tip_registry",
                    "build_hover_tips",
                    new
                    {
                        Provider = name,
                        InputType = TelemetryContext.ForType(inputType)
                    });
            }
        }

        return predictionTips;
    }
}
