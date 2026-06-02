using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;

namespace RandomForeseer;

internal static class DollRoomPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<DollRoom>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(DollRoom dollRoom, EventOption option)
    {
        return option.TextKey switch
        {
            "DOLL_ROOM.pages.INITIAL.options.RANDOM" => OutOfCombatPredictionUtils.RelicTipsWithPickup(
                dollRoom.Owner!,
                [PredictDoll(dollRoom.Rng, 1).First()]),
            "DOLL_ROOM.pages.INITIAL.options.TAKE_SOME_TIME" => OutOfCombatPredictionUtils.RelicTipsWithPickup(dollRoom.Owner!, PredictDoll(dollRoom.Rng, 2)),
            "DOLL_ROOM.pages.INITIAL.options.EXAMINE" => OutOfCombatPredictionUtils.RelicTipsWithPickup(dollRoom.Owner!, PredictDoll(dollRoom.Rng, 3)),
            _ => []
        };
    }

    private static IReadOnlyList<RelicModel> PredictDoll(Rng realRng, int count)
    {
        var rng = PredictionUtils.CloneRng(realRng);
        var dolls = GetDolls();

        return count == 1
            ? [rng.NextItem(dolls)!.ToMutable()]
            : dolls.ToList().StableShuffle(rng).Take(count).Select(relic => relic.ToMutable()).ToList();
    }

    private static RelicModel[] GetDolls()
    {
        return DollRoom._dolls
            .Select(doll => doll.relic)
            .ToArray();
    }
}
