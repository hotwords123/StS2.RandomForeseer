using System.Text.Json;
using System.Text.Json.Nodes;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Telemetry;

internal sealed class ModTelemetryFilter(ITelemetryAdapter inner) : ITelemetryAdapter
{
    private const string StackTraceMarker = "RandomForeseer.";

    public string AdapterId => inner.AdapterId;

    public string EndpointDescription => inner.EndpointDescription;

    public ValueTask<TelemetrySendResult> SendAsync(
        TelemetryApplicant applicant,
        IReadOnlyList<TelemetryEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        var filteredEvents = events.Where(ShouldSend).ToArray();
        return filteredEvents.Length == 0
            ? ValueTask.FromResult(TelemetrySendResult.Ok())
            : inner.SendAsync(applicant, filteredEvents, cancellationToken);
    }

    internal static bool ShouldSend(TelemetryEnvelope telemetryEvent)
    {
        if (!string.Equals(telemetryEvent.EventName, "exception", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGetPropertyString(telemetryEvent.Properties, "capture_mode", out var captureMode) &&
            string.Equals(captureMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ContainsModStackTrace(telemetryEvent.Payload);
    }

    private static bool ContainsModStackTrace(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["stack_trace"] is JsonValue stackTraceValue &&
                    stackTraceValue.TryGetValue<string>(out var stackTrace) &&
                    stackTrace.Contains(StackTraceMarker, StringComparison.Ordinal))
                {
                    return true;
                }

                return obj.Any(property => ContainsModStackTrace(property.Value));

            case JsonArray array:
                return array.Any(ContainsModStackTrace);

            default:
                return false;
        }
    }

    private static bool TryGetPropertyString(
        IReadOnlyDictionary<string, object?> properties,
        string propertyName,
        out string? value)
    {
        if (!properties.TryGetValue(propertyName, out var propertyValue))
        {
            value = null;
            return false;
        }

        value = propertyValue switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            _ => null
        };
        return value is not null;
    }
}
