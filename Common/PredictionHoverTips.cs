using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal static class PredictionHoverTips
{
    private const string PredictionTextHoverTipIdPrefix = $"{Entry.ModId}:PredictionText";
    private const string PredictionWarningHoverTipIdPrefix = $"{Entry.ModId}:PredictionWarning";

    public static IReadOnlyList<IHoverTip> Cards(IEnumerable<CardModel> cards)
    {
        return cards.Select(card => (IHoverTip)new PredictionCardHoverTip(card)).ToList();
    }

    public static IReadOnlyList<IHoverTip> CardBundles(
        IEnumerable<IReadOnlyList<CardModel>> bundles,
        bool isVanillaCardBundle = false)
    {
        var bundleList = bundles
            .Where(bundle => bundle.Count > 0)
            .Select(bundle => (IReadOnlyList<CardModel>)(isVanillaCardBundle
                ? bundle.ToList()
                : bundle.Reverse().ToList()))
            .ToList();

        return bundleList.Count == 0
            ? []
            : [(IHoverTip)new PredictionCardBundleHoverTip(bundleList)];
    }

    // Relics passed here must already be mutable previews; event options and predicted relic rewards
    // create mutable relic instances at their source.
    public static IReadOnlyList<IHoverTip> Relics(IEnumerable<RelicModel> relics)
    {
        return relics.Select(relic => (IHoverTip)CreatePredictionTextHoverTip(relic.HoverTip)).ToList();
    }

    // Potions passed here must already be mutable previews; PotionFactory returns canonical models,
    // so callers should mirror real obtain/reward paths and convert with ToMutable() before calling.
    public static IReadOnlyList<IHoverTip> Potions(IEnumerable<PotionModel> potions)
    {
        return potions.Select(potion => (IHoverTip)CreatePredictionTextHoverTip(potion.HoverTip)).ToList();
    }

    public static IHoverTip CardSelectionDriftWarning()
    {
        var tip = new HoverTip(
            PredictionLocalization.Text("card_selection_drift_warning.title"),
            PredictionLocalization.Text("card_selection_drift_warning.description"))
        {
            Id = $"{PredictionWarningHoverTipIdPrefix}:CardSelectionDrift",
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

    private static HoverTip CreatePredictionTextHoverTip(HoverTip tip)
    {
        tip.Id = $"{PredictionTextHoverTipIdPrefix}:{tip.Id}";
        tip.IsInstanced = true;
        return tip;
    }
}

internal class PredictionCardHoverTip(CardModel card) : CardHoverTip(card), IHoverTip
{
    bool IHoverTip.IsInstanced => true;
}

internal class PredictionCardBundleHoverTip(IReadOnlyList<IReadOnlyList<CardModel>> bundles)
    : CardHoverTip(bundles.First().First()), IHoverTip
{
    public IReadOnlyList<IReadOnlyList<CardModel>> Bundles { get; } = bundles;

    string IHoverTip.Id => string.Empty;

    bool IHoverTip.IsInstanced => true;
}
