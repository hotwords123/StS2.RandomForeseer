using MegaCrit.Sts2.Core.HoverTips;

namespace RandomForeseer.Common;

internal sealed class PredictionHoverTipRegistry<TInput>
{
    private readonly List<Provider> _providers = [];

    public void Register(string name, Func<TInput, IReadOnlyList<IHoverTip>> provider)
    {
        if (_providers.Any(existingProvider => existingProvider.Name == name))
        {
            Entry.Logger.Warn(
                $"Duplicate hover tip prediction provider registration ignored: {typeof(TInput)} '{name}'");
            return;
        }

        _providers.Add(new Provider(name, provider));
    }

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
            }
        }

        return predictionTips;
    }

    private static string Describe(TInput input)
    {
        return input?.GetType().ToString() ?? typeof(TInput).ToString();
    }

    private sealed class Provider(string name, Func<TInput, IReadOnlyList<IHoverTip>> getHoverTips)
    {
        public string Name { get; } = name;

        public IReadOnlyList<IHoverTip> GetHoverTips(TInput input)
        {
            return getHoverTips(input);
        }
    }
}
