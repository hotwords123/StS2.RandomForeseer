using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer;

internal static class PredictionHoverTips
{
    public static IReadOnlyList<IHoverTip> Cards(IEnumerable<CardModel> cards)
    {
        return cards.Select(card => (IHoverTip)new PredictionCardHoverTip(card)).ToList();
    }

    // Relics passed here must already be mutable previews; event options and predicted relic rewards
    // create mutable relic instances at their source.
    public static IReadOnlyList<IHoverTip> Relics(IEnumerable<RelicModel> relics)
    {
        return relics.Select(relic => (IHoverTip)relic.HoverTip).ToList();
    }

    // Potions passed here must already be mutable previews; PotionFactory returns canonical models,
    // so callers should mirror real obtain/reward paths and convert with ToMutable() before calling.
    public static IReadOnlyList<IHoverTip> Potions(IEnumerable<PotionModel> potions)
    {
        return potions.Select(potion => (IHoverTip)potion.HoverTip).ToList();
    }
}

internal class PredictionCardHoverTip(CardModel card) : CardHoverTip(card), IHoverTip
{
    bool IHoverTip.IsInstanced => true;
}
