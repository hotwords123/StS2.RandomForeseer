using MegaCrit.Sts2.Core.HoverTips;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips;

/// <summary>
/// Maintains an ordered set of named prediction HoverTip providers for one input type.
/// </summary>
/// <remarks>
/// Providers are invoked in registration order. A provider failure is logged and isolated, so other providers still
/// contribute results; callers should therefore treat the returned list as best-effort prediction output.
/// </remarks>
internal sealed class PredictionHoverTipRegistry<TInput>
{
    private readonly List<Provider> _providers = [];

    /// <summary>
    /// Registers a provider under a unique diagnostic name.
    /// </summary>
    /// <remarks>
    /// A duplicate name is ignored. Exceptions thrown while invoking the provider are caught by
    /// <see cref="GetHoverTips"/> and do not abort other providers.
    /// </remarks>
    /// <param name="name">Stable name used for duplicate detection and failure logging.</param>
    /// <param name="provider">Function that produces prediction tips for one input.</param>
    public void Register(string name, Func<TInput, IEnumerable<IHoverTip>> provider)
    {
        if (_providers.Any(existingProvider => existingProvider.Name == name))
        {
            Entry.Logger.Warn(
                $"Duplicate hover tip prediction provider registration ignored: {typeof(TInput)} '{name}'");
            return;
        }

        _providers.Add(new Provider(name, provider));
    }

    /// <summary>
    /// Invokes all registered providers and concatenates their successful results in registration order.
    /// </summary>
    /// <remarks>
    /// Provider exceptions are logged and skipped. This method does not deduplicate or otherwise normalize the
    /// returned tips; the eventual HoverTip presentation pipeline owns those responsibilities.
    /// </remarks>
    /// <param name="input">Input passed to every registered provider.</param>
    /// <returns>Tips returned by providers that completed successfully.</returns>
    public IReadOnlyList<IHoverTip> GetHoverTips(TInput input)
    {
        var predictionTips = new List<IHoverTip>();
        foreach (var provider in _providers)
        {
            try
            {
                predictionTips.AddRange(provider.GetHoverTips(input));
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn(
                    $"Hover tip prediction provider '{provider.Name}' failed for {Describe(input)}: {ex}");
                ModTelemetry.CaptureException(ex, "prediction_hover_tip_registry", "build_hover_tips");
            }
        }

        return predictionTips;
    }

    private static string Describe(TInput input)
    {
        return (input?.GetType() ?? typeof(TInput)).ToString();
    }

    private readonly record struct Provider(string Name, Func<TInput, IEnumerable<IHoverTip>> GetHoverTips);
}
