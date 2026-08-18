using RandomForeseer.RandomForeseerCode.Localization;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Telemetry;

internal static class ModTelemetry
{
    private const int MaxRecentExceptionFingerprints = 128;

    private static readonly Lock ExceptionFingerprintLock = new();
    private static readonly Queue<string> RecentExceptionFingerprintOrder = [];
    private static readonly HashSet<string> RecentExceptionFingerprints = new(StringComparer.Ordinal);

    private static ITelemetryClient? _client;

    public static void Register()
    {
        RitsuLibFramework.RegisterTelemetryContributionProvider(new SettingsTelemetryContribution());
        RitsuLibFramework.RegisterTelemetryApplicant(new TelemetryApplicant
        {
            ApplicantId = Entry.ModId,
            OwnerModId = Entry.ModId,
            DisplayName = "Random Foreseer",
            DisplayNameText = T("telemetry.applicant.display_name"),
            Adapter = new ModTelemetryFilter(TelemetryBuildConfiguration.CreateAdapter()),
            Requests =
            [
                new TelemetryRequest
                {
                    RequestId = "basic_usage",
                    Category = TelemetryDataCategory.BasicUsage,
                    Description = "telemetry.basic_usage.description",
                    DescriptionText = T("telemetry.basic_usage.description"),
                    ContributionSubscriptions = [SettingsTelemetryContribution.Id]
                },
                TelemetryRequest.ModInventory(T("telemetry.mod_inventory.description")),
                TelemetryRequest.Diagnostics(T("telemetry.diagnostics.description"))
            ]
        });

        _client = RitsuLibFramework.GetTelemetryClient(Entry.ModId);
    }

    public static void CaptureException(Exception exception, string subsystem, string operation)
    {
        try
        {
            if (_client is not { } client ||
                !client.IsEnabled("diagnostics") ||
                !TryMarkRecent(exception, subsystem, operation))
            {
                return;
            }

            client.CaptureException(exception, new Dictionary<string, object?>
            {
                ["capture_mode"] = "manual",
                ["capture_source"] = $"random_foreseer/{subsystem}",
                ["subsystem"] = subsystem,
                ["operation"] = operation
            });
        }
        catch (Exception telemetryException)
        {
            Entry.Logger.Warn(
                $"Telemetry exception capture failed for {subsystem}/{operation}: {telemetryException.Message}");
        }
    }

    private static bool TryMarkRecent(Exception exception, string subsystem, string operation)
    {
        var stackHead = exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
        var fingerprint = string.Join(
            '\n',
            subsystem,
            operation,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            stackHead);

        lock (ExceptionFingerprintLock)
        {
            if (!RecentExceptionFingerprints.Add(fingerprint))
            {
                return false;
            }

            RecentExceptionFingerprintOrder.Enqueue(fingerprint);
            while (RecentExceptionFingerprintOrder.Count > MaxRecentExceptionFingerprints)
            {
                RecentExceptionFingerprints.Remove(RecentExceptionFingerprintOrder.Dequeue());
            }

            return true;
        }
    }

    private static ModSettingsText T(string key)
    {
        return ModSettingsText.I18N(ModLocalization.SettingsLocalization, key, key);
    }
}
