using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer.OutOfCombat;

internal static class TreasureRoomRelicHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control control)
    {
        if (control is not NTreasureRoomRelicHolder { Relic.Model: { } relic } ||
            LocalContext.GetMe(RunManager.Instance.State) is not { } player)
        {
            return [];
        }

        return RelicPickupPrediction.GetHoverTips(player, relic);
    }
}
