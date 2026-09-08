using Godot;
using Godot.Collections;
using System.Text.Json.Nodes;

namespace RandomForeseer.RandomForeseerCode.Telemetry;

/// <summary>Captures engine errors that return a failure value instead of throwing a managed exception.</summary>
internal sealed partial class GodotErrorTelemetryListener : Godot.Logger
{
    private const int MaxPendingErrors = 32;
    private const int MaxErrorsPerSession = 128;
    private static GodotErrorTelemetryListener? _instance;

    [ThreadStatic] private static bool _suppressCapture;

    private readonly Lock _sync = new();
    private readonly Queue<(JsonObject Payload, string ExceptionType)> _pending = new();
    private readonly HashSet<string> _fingerprints = new(StringComparer.Ordinal);
    private bool _flushQueued;

    internal static void Register()
    {
        if (_instance is not null)
        {
            return;
        }

        try
        {
            var listener = new GodotErrorTelemetryListener();
            OS.AddLogger(listener);
            _instance = listener;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Godot error telemetry registration failed: {ex.Message}");
        }
    }

    public override void _LogError(
        string function, string file, int line, string code, string rationale,
        bool editorNotify, int errorType, Array<ScriptBacktrace> scriptBacktraces)
    {
        if (_suppressCapture || errorType == (int)ErrorType.Warning)
        {
            return;
        }

        _suppressCapture = true;
        try
        {
            if (!ModTelemetry.IsDiagnosticsEnabled)
            {
                return;
            }

            // Godot owns these backtraces. Copy strings now; never retain engine objects or inspect the scene tree here.
            var stackTrace = string.Join("\n", scriptBacktraces.Take(8)
                .Where(trace => !trace.IsEmpty()).Select(trace => trace.Format()));
            function = LimitLength(function, 1024);
            file = LimitLength(file, 1024);
            code = LimitLength(code, 4096);
            rationale = LimitLength(rationale, 4096);
            stackTrace = LimitLength(stackTrace, 16384);
            if (!ModTelemetryAdapter.ContainsModName(stackTrace) &&
                !ModTelemetryAdapter.ContainsModName(code) &&
                !ModTelemetryAdapter.ContainsModName(rationale) &&
                !ModTelemetryAdapter.ContainsModName(file) &&
                !ModTelemetryAdapter.ContainsModName(function))
            {
                return;
            }

            var exceptionType = $"Godot.{(ErrorType)errorType}";
            var fingerprint = string.Join('\n', exceptionType, function, file, line.ToString(), code, rationale,
                stackTrace.Split('\n').FirstOrDefault(ModTelemetryAdapter.ContainsModName));
            var payload = new JsonObject
            {
                ["exception"] = new JsonObject
                {
                    ["type"] = exceptionType,
                    ["message"] = string.IsNullOrWhiteSpace(rationale) ? code : rationale,
                    ["stack_trace"] = stackTrace
                },
                ["godot_error"] = new JsonObject
                {
                    ["function"] = function,
                    ["file"] = file,
                    ["line"] = line,
                    ["code"] = code,
                    ["rationale"] = rationale,
                    ["error_type"] = errorType,
                    ["captured_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["managed_thread_id"] = System.Environment.CurrentManagedThreadId
                }
            };

            bool scheduleFlush;
            lock (_sync)
            {
                if (_pending.Count >= MaxPendingErrors || _fingerprints.Count >= MaxErrorsPerSession ||
                    !_fingerprints.Add(fingerprint))
                {
                    return;
                }

                _pending.Enqueue((payload, exceptionType));
                scheduleFlush = !_flushQueued;
                _flushQueued = true;
            }

            if (scheduleFlush)
            {
                Callable.From(Flush).CallDeferred();
            }
        }
        catch
        {
            // Logging from a logger callback can recurse into the same failing capture path.
            lock (_sync)
            {
                _pending.Clear();
                _flushQueued = false;
            }
        }
        finally
        {
            _suppressCapture = false;
        }
    }

    private void Flush()
    {
        (JsonObject Payload, string ExceptionType)[] errors;
        lock (_sync)
        {
            errors = [.. _pending];
            _pending.Clear();
            _flushQueued = false;
        }

        // Capture on the main thread after Godot's logging callback has returned. Suppress failures of capture itself.
        _suppressCapture = true;
        try
        {
            foreach (var (payload, exceptionType) in errors)
            {
                try
                {
                    ModTelemetry.CaptureGodotError(payload, exceptionType);
                }
                catch
                {
                    // Diagnostics must not break the game or recursively report a telemetry failure.
                }
            }
        }
        finally
        {
            _suppressCapture = false;
        }
    }

    private static string LimitLength(string text, int maxLength)
    {
        // Preserve diagnostic paths like RitsuLib's DiagnosticsTelemetryCollector (f13e906d); only limit field length.
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    private static void OnProcessExit(object? sender, EventArgs args)
    {
        try
        {
            if (_instance is { } listener)
            {
                OS.RemoveLogger(listener);
                _instance = null;
            }
        }
        catch
        {
            // The engine may already be shutting down.
        }
    }
}
