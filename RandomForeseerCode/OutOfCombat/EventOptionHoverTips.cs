using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace RandomForeseer.OutOfCombat;

internal static class EventOptionHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner) =>
        owner is NEventOptionButton button
            ? EventOptionPrediction.GetHoverTips(button.Option)
            : [];
}
