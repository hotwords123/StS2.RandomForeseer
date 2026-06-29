using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal static class PredictionHoverTips
{
    private const int MaxDriftWarningModelNames = 3;
    private const string PredictionHoverTipIdPrefix = $"{Entry.ModId}:Prediction";

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
            ? tips.Prepend(Text("transform_bundle_explanation")).ToList()
            : tips;
    }

    public static HoverTip Text(string keyPrefix, Action<LocString>? configureDescription = null)
    {
        var title = PredictionLocalization.Text($"{keyPrefix}.title");
        var description = PredictionLocalization.Text($"{keyPrefix}.description");
        configureDescription?.Invoke(description);

        var tip = new HoverTip(title, description)
        {
            Id = $"{PredictionHoverTipIdPrefix}:{keyPrefix}",
            IsInstanced = true
        };
        return tip;
    }

    public static IReadOnlyList<IHoverTip> Relics(IEnumerable<RelicModel> relics)
    {
        return relics.Select(relic => (IHoverTip)ToPredictionHoverTip(relic.HoverTip)).ToList();
    }

    public static IReadOnlyList<IHoverTip> Potions(IEnumerable<PotionModel> potions)
    {
        return potions.Select(potion => (IHoverTip)ToPredictionHoverTip(potion.HoverTip)).ToList();
    }

    public static IReadOnlyList<IHoverTip> Orbs(IEnumerable<OrbModel> orbs)
    {
        return orbs.Select(orb => (IHoverTip)ToPredictionHoverTip(orb.DumbHoverTip)).ToList();
    }

    public static HoverTip? DriftWarning(string key, PredictionRisk risk)
    {
        if (!risk.HasRisk || !RandomForeseerSettings.EnableDriftWarnings)
        {
            return null;
        }

        var tip = Text($"drift_warning.{key}", description =>
        {
            var modelNames = risk.Models.Select(GetModelName).Distinct().ToList();
            var shownModelNames = modelNames.Take(MaxDriftWarningModelNames).ToList();
            var extraModelCount = modelNames.Count - shownModelNames.Count;

            description.Add("HasModels", shownModelNames.Count > 0);
            description.Add("Models", shownModelNames);
            description.Add("ExtraModelCount", extraModelCount);
        });
        return tip;
    }

    public static void AddDriftWarningIfNeeded(List<IHoverTip> tips, string key, PredictionRisk risk)
    {
        if (DriftWarning(key, risk) is { } tip)
        {
            tips.Add(tip);
        }
    }

    public static bool IsPredictionHoverTip(IHoverTip tip)
    {
        return tip.Id.StartsWith(PredictionHoverTipIdPrefix, StringComparison.Ordinal);
    }

    public static string GetModelName(AbstractModel model)
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
                MonsterModel monster => monster.Title.GetFormattedText(),
                _ => model.Id.Entry
            };
        }
        catch
        {
            return model.Id.Entry;
        }
    }

    private static HoverTip ToPredictionHoverTip(HoverTip tip)
    {
        tip.Id = $"{PredictionHoverTipIdPrefix}:{tip.Id}";
        tip.IsInstanced = true;
        // Vanilla records hover tips with canonical models as discovered progress.
        // Prediction tips are informational only, so they must not reveal cards/relics/potions in the save.
        tip.CanonicalModel = null;
        return tip;
    }
}

internal class PredictionCardHoverTip(CardModel card, bool isDimmed = false) : CardHoverTip(card), IHoverTip
{
    public bool IsDimmed { get; } = isDimmed;

    bool IHoverTip.IsInstanced => true;

    // Hide the canonical card from NHoverTipSet so predicted cards do not mark progress as discovered.
    AbstractModel? IHoverTip.CanonicalModel => null;
}

internal class PredictionCardBundleHoverTip(IReadOnlyList<IReadOnlyList<CardModel>> bundles)
    : CardHoverTip(bundles.First().First()), IHoverTip
{
    public IReadOnlyList<IReadOnlyList<CardModel>> Bundles { get; } = bundles;

    string IHoverTip.Id => string.Empty;

    bool IHoverTip.IsInstanced => true;

    // Hide the canonical card from NHoverTipSet so predicted bundled cards do not mark progress as discovered.
    AbstractModel? IHoverTip.CanonicalModel => null;
}
