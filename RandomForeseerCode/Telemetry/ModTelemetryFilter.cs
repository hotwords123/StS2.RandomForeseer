using System.Text.Json.Nodes;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.Telemetry;

internal static class ModTelemetryFilter
{
    private const string StackTraceMarker = "RandomForeseer.";

    internal static bool ShouldCapture(TelemetryCaptureContext context) =>
        context.EventName switch
        {
            "exception" => ShouldCaptureException(context),
            "godot_engine_error" => ShouldCaptureGodotEngineError(context),
            _ => true
        };

    private static bool ShouldCaptureGodotEngineError(TelemetryCaptureContext context)
    {
        // RitsuLib 0.6.0 supplies the sanitized engine_error object directly as SourceData.
        // Godot logs C# exception stacks in code as well as native call stacks in script_backtrace.
        return context.SourceData is JsonObject error &&
               (ContainsModStackTrace(error["script_backtrace"]) || ContainsModStackTrace(error["code"]));
    }

    private static bool ShouldCaptureException(TelemetryCaptureContext context)
    {
        // CaptureFilter receives the source but not capture_mode; this prefix is set by CaptureException.
        if (context.Source?.StartsWith(ModTelemetry.ManualCaptureSourcePrefix, StringComparison.Ordinal) == true)
        {
            return true;
        }

        for (var exception = context.Exception; exception is not null; exception = exception.InnerException)
        {
            if (exception.StackTrace?.Contains(StackTraceMarker, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsModStackTrace(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        text.Contains(StackTraceMarker, StringComparison.Ordinal);
}
