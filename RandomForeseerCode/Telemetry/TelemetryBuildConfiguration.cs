using STS2RitsuLib.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Telemetry;

internal static class TelemetryBuildConfiguration
{
    private const string PostHogHostMetadataName = "RandomForeseerPostHogHost";
    private const string PostHogProjectApiKeyMetadataName = "RandomForeseerPostHogProjectApiKey";

    public static ITelemetryAdapter CreateAdapter()
    {
        Entry.AssemblyMetadata.TryGetValue(PostHogHostMetadataName, out var host);
        Entry.AssemblyMetadata.TryGetValue(PostHogProjectApiKeyMetadataName, out var projectApiKey);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(projectApiKey))
        {
            return new DisabledTelemetryAdapter(
                "PostHog host or project API key was not configured at build time.");
        }

        try
        {
            return new PostHogTelemetryAdapter(host, projectApiKey);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"PostHog telemetry adapter initialization failed: {ex}");
            return new DisabledTelemetryAdapter("PostHog telemetry adapter initialization failed.");
        }
    }
}
