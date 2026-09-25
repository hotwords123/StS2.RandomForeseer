using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class RelicChoiceSelectionHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control control)
    {
        if (control is not NRelicBasicHolder { Relic.Model: { } relic } ||
            control.GetAncestorOfType<NChooseARelicSelection>() is null ||
            LocalContext.GetMe(RunManager.Instance.State) is not { } player)
        {
            return [];
        }

        return RelicPickupPrediction.GetHoverTips(player, relic);
    }
}
