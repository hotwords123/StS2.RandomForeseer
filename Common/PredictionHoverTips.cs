using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal static class PredictionHoverTips
{
    private const int MaxDriftWarningModelNames = 3;
    private const string PredictionTextHoverTipIdPrefix = $"{Entry.ModId}:PredictionText";
    private const string PredictionWarningHoverTipIdPrefix = $"{Entry.ModId}:PredictionWarning";

    public static IReadOnlyList<IHoverTip> Cards(IEnumerable<CardModel> cards)
    {
        return cards.Select(card => (IHoverTip)new PredictionCardHoverTip(card)).ToList();
    }

    public static IReadOnlyList<IHoverTip> CardBundles(
        IEnumerable<IReadOnlyList<CardModel>> bundles,
        bool isVanillaCardBundle = false,
        bool isTransform = false)
    {
        var bundleList = bundles.Where(bundle => bundle.Count > 0).ToList();

        var tips = bundleList.Count switch
        {
            0 => [],
            1 => Cards(bundleList[0]),
            _ => [(IHoverTip)new PredictionCardBundleHoverTip(isVanillaCardBundle
                ? bundleList
                : bundleList.Select(bundle => bundle.Reverse().ToList()).ToList())]
        };

        return isTransform && bundleList.Count > 1
            ? Text("transform_bundle_explanation").Concat(tips).ToList()
            : tips;
    }

    public static IReadOnlyList<IHoverTip> Text(
        string keyPrefix,
        Action<LocString>? configureDescription = null)
    {
        var title = PredictionLocalization.Text($"{keyPrefix}.title");
        var description = PredictionLocalization.Text($"{keyPrefix}.description");
        configureDescription?.Invoke(description);

        var tip = new HoverTip(title, description)
        {
            Id = $"{PredictionTextHoverTipIdPrefix}:{keyPrefix}",
            IsInstanced = true
        };
        return [tip];
    }

    public static IReadOnlyList<IHoverTip> Relics(IEnumerable<RelicModel> relics)
    {
        return relics.Select(relic => (IHoverTip)CreatePredictionTextHoverTip(relic.HoverTip)).ToList();
    }

    public static IReadOnlyList<IHoverTip> Potions(IEnumerable<PotionModel> potions)
    {
        return potions.Select(potion => (IHoverTip)CreatePredictionTextHoverTip(potion.HoverTip)).ToList();
    }

    public static IHoverTip DriftWarning(string key, PredictionRisk risk)
    {
        var description = PredictionLocalization.Text($"drift_warning.{key}.description");
        ConfigureDriftWarningDescription(description, risk);

        var tip = new HoverTip(
            PredictionLocalization.Text($"drift_warning.{key}.title"),
            description)
        {
            Id = $"{PredictionWarningHoverTipIdPrefix}:Drift:{key}",
            IsInstanced = true
        };
        return tip;
    }

    public static bool IsPredictionTextHoverTip(IHoverTip tip)
    {
        return tip.Id.StartsWith(PredictionTextHoverTipIdPrefix, StringComparison.Ordinal);
    }

    public static bool IsPredictionWarningHoverTip(IHoverTip tip)
    {
        return tip.Id.StartsWith(PredictionWarningHoverTipIdPrefix, StringComparison.Ordinal);
    }

    public static bool IsPredictionHoverTip(IHoverTip tip)
    {
        return IsPredictionTextHoverTip(tip) || IsPredictionWarningHoverTip(tip);
    }

    private static void ConfigureDriftWarningDescription(LocString description, PredictionRisk risk)
    {
        var modelNames = risk.Models.Select(GetModelName).Distinct().ToList();
        var shownModelNames = modelNames.Take(MaxDriftWarningModelNames).ToList();
        var extraModelCount = modelNames.Count - shownModelNames.Count;

        description.Add("HasModels", shownModelNames.Count > 0);
        description.Add("Models", shownModelNames);
        description.Add("ExtraModelCount", extraModelCount);
    }

    private static string GetModelName(AbstractModel model)
    {
        try
        {
            return model switch
            {
                CardModel card => card.Title,
                RelicModel relic => relic.Title.GetFormattedText(),
                PowerModel power => power.Title.GetFormattedText(),
                PotionModel potion => potion.Title.GetFormattedText(),
                ModifierModel modifier => modifier.Title.GetFormattedText(),
                AfflictionModel affliction => affliction.Title.GetFormattedText(),
                EnchantmentModel enchantment => enchantment.Title.GetFormattedText(),
                OrbModel orb => orb.Title.GetFormattedText(),
                _ => model.Id.Entry
            };
        }
        catch
        {
            return model.Id.Entry;
        }
    }

    private static HoverTip CreatePredictionTextHoverTip(HoverTip tip)
    {
        tip.Id = $"{PredictionTextHoverTipIdPrefix}:{tip.Id}";
        tip.IsInstanced = true;
        return tip;
    }
}

internal class PredictionCardHoverTip(CardModel card, bool isDimmed = false) : CardHoverTip(card), IHoverTip
{
    public bool IsDimmed { get; } = isDimmed;

    bool IHoverTip.IsInstanced => true;
}

internal class PredictionCardBundleHoverTip(IReadOnlyList<IReadOnlyList<CardModel>> bundles)
    : CardHoverTip(bundles.First().First()), IHoverTip
{
    public IReadOnlyList<IReadOnlyList<CardModel>> Bundles { get; } = bundles;

    string IHoverTip.Id => string.Empty;

    bool IHoverTip.IsInstanced => true;
}
