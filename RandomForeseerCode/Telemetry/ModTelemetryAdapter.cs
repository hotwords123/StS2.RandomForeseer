using System.Text.Json.Nodes;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.Telemetry;

internal sealed class ModTelemetryAdapter(ITelemetryAdapter inner) : ITelemetryAdapter
{
    private const string VersionProperty = "random_foreseer_version";

    public string AdapterId => inner.AdapterId;

    public string EndpointDescription => inner.EndpointDescription;

    public ValueTask<TelemetrySendResult> SendAsync(
        TelemetryApplicant applicant,
        IReadOnlyList<TelemetryEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var telemetryEvent in events)
        {
            if (ReadCapturedVersion(telemetryEvent.Payload) is { } version)
            {
                telemetryEvent.Properties[VersionProperty] = version;
            }
            else
            {
                telemetryEvent.Properties.Remove(VersionProperty);
            }
        }

        return inner.SendAsync(applicant, events, cancellationToken);
    }

    private static string? ReadCapturedVersion(JsonNode? payload)
    {
        if (payload is not JsonObject root ||
            root["private_contributions"] is not JsonObject privateContributions ||
            privateContributions[Entry.ModId] is not JsonObject contributions)
        {
            return null;
        }

        foreach (var (id, value) in contributions)
        {
            if (id.StartsWith(ModVersionTelemetryContribution.IdPrefix, StringComparison.Ordinal) &&
                value is JsonValue node && node.TryGetValue<string>(out var version) &&
                !string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        return null;
    }
}
