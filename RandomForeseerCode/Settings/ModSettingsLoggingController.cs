using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RandomForeseer.RandomForeseerCode.Data;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.Settings;

internal static class ModSettingsLoggingController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static JsonObject? _snapshot;
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        _snapshot = CaptureSnapshot();
        Entry.Logger.Info($"Settings loaded: {_snapshot.ToJsonString(JsonOptions)}");
        ModSettingsBindingWriteEvents.ValueWritten += OnSettingsValueWritten;
        _isRegistered = true;
    }

    private static void OnSettingsValueWritten(IModSettingsBinding binding)
    {
        if (binding is not { ModId: Entry.ModId, DataKey: ModData.SettingsKey })
        {
            return;
        }

        var previous = _snapshot!;
        var current = CaptureSnapshot();
        _snapshot = current;

        foreach (var propertyName in previous.Select(property => property.Key)
                     .Concat(current.Select(property => property.Key))
                     .Distinct(StringComparer.Ordinal))
        {
            previous.TryGetPropertyValue(propertyName, out var previousValue);
            current.TryGetPropertyValue(propertyName, out var currentValue);
            if (JsonNode.DeepEquals(previousValue, currentValue))
            {
                continue;
            }

            Entry.Logger.Info(
                $"Setting changed: {propertyName}: {FormatValue(previousValue)} -> {FormatValue(currentValue)}");
        }
    }

    private static JsonObject CaptureSnapshot()
    {
        return JsonSerializer.SerializeToNode(ModData.Settings, JsonOptions)!.AsObject();
    }

    private static string FormatValue(JsonNode? value)
    {
        return value?.ToJsonString(JsonOptions) ?? "null";
    }
}
