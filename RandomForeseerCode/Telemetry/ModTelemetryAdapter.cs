using STS2RitsuLib.Telemetry;

namespace RandomForeseer.Telemetry;

internal sealed class ModTelemetryAdapter(ITelemetryAdapter inner) : ITelemetryAdapter
{
    private const string ModVersionMetadataName = "RandomForeseerModVersion";
    private const string UnknownModVersion = "<unknown>";

    private readonly string _modVersion = ResolveModVersion();

    public string AdapterId => inner.AdapterId;

    public string EndpointDescription => inner.EndpointDescription;

    public ValueTask<TelemetrySendResult> SendAsync(
        TelemetryApplicant applicant,
        IReadOnlyList<TelemetryEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var telemetryEvent in events)
        {
            telemetryEvent.Properties["random_foreseer_version"] = _modVersion;
        }

        return inner.SendAsync(applicant, events, cancellationToken);
    }

    private static string ResolveModVersion()
    {
        Entry.AssemblyMetadata.TryGetValue(ModVersionMetadataName, out var version);
        version = version?.Trim();
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        Entry.Logger.Warn("Could not resolve the Random Foreseer version for telemetry events.");
        return UnknownModVersion;
    }
}
