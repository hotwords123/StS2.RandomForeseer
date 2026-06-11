using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class TinkerTimePrediction
{
    private static readonly Dictionary<string, TinkerTime.RiderEffect[]> RidersByOptionKey = new()
    {
        ["TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.ATTACK"] =
        [
            TinkerTime.RiderEffect.Sapping,
            TinkerTime.RiderEffect.Violence,
            TinkerTime.RiderEffect.Choking
        ],
        ["TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.SKILL"] =
        [
            TinkerTime.RiderEffect.Energized,
            TinkerTime.RiderEffect.Wisdom,
            TinkerTime.RiderEffect.Chaos
        ],
        ["TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.POWER"] =
        [
            TinkerTime.RiderEffect.Expertise,
            TinkerTime.RiderEffect.Curious,
            TinkerTime.RiderEffect.Improvement
        ]
    };

    public static IReadOnlyList<IHoverTip> GetHoverTips(TinkerTime tinkerTime, EventOption option)
    {
        if (!RidersByOptionKey.TryGetValue(option.TextKey, out var riders))
        {
            return [];
        }

        var rng = PredictionUtils.CloneRng(tinkerTime.Rng);
        var chosen = riders.ToList().UnstableShuffle(rng).Take(2);
        var cardType = option.TextKey switch
        {
            "TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.ATTACK" => CardType.Attack,
            "TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.SKILL" => CardType.Skill,
            _ => CardType.Power
        };
        var cards = chosen.Select(rider =>
        {
            var card = (MadScience)ModelDb.Card<MadScience>().ToMutable();
            card.TinkerTimeType = cardType;
            card.TinkerTimeRider = rider;
            return (CardModel)card;
        });

        return PredictionHoverTips.Cards(cards);
    }
}
