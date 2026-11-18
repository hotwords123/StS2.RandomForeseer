using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.InCombat;

internal static class EndTurnButtonHoverTips
{
    private const float TooltipGap = 8f;

    private static Control? _owner;

    public static void Show(NEndTurnButton button, IReadOnlyList<IHoverTip> hoverTips)
    {
        if (_owner is not { } owner ||
            !GodotObject.IsInstanceValid(owner) ||
            !ReferenceEquals(owner.GetParent(), button))
        {
            Clear();
            owner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            button.AddChild(owner);
            _owner = owner;
        }

        var tipSet = NHoverTipSet.CreateAndShow(owner, hoverTips);
        if (tipSet is null)
        {
            return;
        }

        var anchor = NHoverTipSet._activeHoverTips.TryGetValue(button, out var vanilla)
            ? vanilla._textHoverTipContainer.GlobalPosition
            : button.GlobalPosition;
        anchor.X -= NHoverTipSet.hoverTipWidth + TooltipGap;
        tipSet.SetAlignment(new Rect2(anchor, Vector2.Zero), HoverTipAlignment.Right);
    }

    public static void Clear()
    {
        if (_owner is { } owner && GodotObject.IsInstanceValid(owner))
        {
            NHoverTipSet.Remove(owner);
            owner.QueueFree();
        }

        _owner = null;
    }
}
