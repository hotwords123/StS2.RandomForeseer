using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RandomForeseer.RandomForeseerCode.Data;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Telemetry;

internal sealed class SettingsTelemetryContribution : ITelemetryContributionProvider
{
    public const string Id = "settings_snapshot";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ContributorModId => Entry.ModId;

    public string ContributionId => Id;

    public TelemetryDataCategory Category => TelemetryDataCategory.BasicUsage;

    public TelemetryContributionVisibility Visibility => TelemetryContributionVisibility.PrivateToApplicant;

    public JsonNode Build(TelemetryContributionContext context)
    {
        return JsonSerializer.SerializeToNode(ModData.Settings, JsonOptions)!;
    }
}
