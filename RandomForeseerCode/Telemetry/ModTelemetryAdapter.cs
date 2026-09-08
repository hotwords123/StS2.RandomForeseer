using System.Text.Json;
using System.Text.Json.Nodes;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Telemetry;

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
        var filteredEvents = events.Where(ShouldSend).ToArray();
        foreach (var telemetryEvent in filteredEvents)
        {
            telemetryEvent.Properties["random_foreseer_version"] = _modVersion;
        }

        return filteredEvents.Length == 0
            ? ValueTask.FromResult(TelemetrySendResult.Ok())
            : inner.SendAsync(applicant, filteredEvents, cancellationToken);
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

        return ContainsModDiagnostic(telemetryEvent.Payload);
    }

    internal static bool ContainsModName(string text) => text.Contains(Entry.ModId, StringComparison.Ordinal);

    private static bool ContainsModDiagnostic(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["stack_trace"] is JsonValue stackTraceValue &&
                    stackTraceValue.TryGetValue<string>(out var stackTrace) &&
                    ContainsModName(stackTrace))
                {
                    return true;
                }

                if (obj["godot_error"] is JsonObject godotError &&
                    godotError.Any(property =>
                        property.Value is JsonValue value &&
                        value.TryGetValue<string>(out var text) && ContainsModName(text)))
                {
                    return true;
                }

                return obj.Any(property => ContainsModDiagnostic(property.Value));

            case JsonArray array:
                return array.Any(ContainsModDiagnostic);

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
