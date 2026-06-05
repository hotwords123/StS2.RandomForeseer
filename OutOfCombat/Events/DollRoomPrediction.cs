using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class DollRoomPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<DollRoom>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(DollRoom dollRoom, EventOption option)
    {
        var count = option.TextKey switch
        {
            "DOLL_ROOM.pages.INITIAL.options.RANDOM" => 1,
            "DOLL_ROOM.pages.INITIAL.options.TAKE_SOME_TIME" => 2,
            "DOLL_ROOM.pages.INITIAL.options.EXAMINE" => 3,
            _ => 0
        };

        return count > 0
            ? OutOfCombatPredictionUtils.RelicTipsWithPickup(dollRoom.Owner!, PredictDoll(dollRoom.Rng, count))
            : [];
    }

    private static IReadOnlyList<RelicModel> PredictDoll(Rng realRng, int count)
    {
        var rng = PredictionUtils.CloneRng(realRng);
        var dolls = DollRoom._dolls.Select(doll => doll.relic).ToArray();

        return count == 1
            ? [rng.NextItem(dolls)!]
            : dolls.ToList().StableShuffle(rng).Take(count).ToList();
    }
}
