using System.Text.Json.Nodes;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.Telemetry;

internal sealed class ModVersionTelemetryContribution(TelemetryDataCategory category)
    : ITelemetryContributionProvider
{
    public const string IdPrefix = "mod_version/";

    private static readonly string? ModVersion = ResolveModVersion();

    public string ContributorModId => Entry.ModId;

    public string ContributionId => GetIdForCategory(category);

    public TelemetryDataCategory Category => category;

    public TelemetryContributionVisibility Visibility => TelemetryContributionVisibility.PrivateToApplicant;

    public JsonNode? Build(TelemetryContributionContext context) => JsonValue.Create(ModVersion);

    public static string GetIdForCategory(TelemetryDataCategory category) => $"{IdPrefix}{category}";

    private static string? ResolveModVersion()
    {
        Entry.AssemblyMetadata.TryGetValue("RandomForeseerModVersion", out var version);
        version = version?.Trim();
        if (!string.IsNullOrEmpty(version))
        {
            return version;
        }

        Entry.Logger.Warn("Could not resolve the mod version for telemetry events.");
        return null;
    }
}
