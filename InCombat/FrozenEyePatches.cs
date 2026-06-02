using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.addons.mega_text;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(NCardPileScreen), "OnPileContentsChanged")]
internal static class FrozenEyeCardPileScreenPatch
{
    private static readonly FieldInfo GridField =
        AccessTools.Field(typeof(NCardPileScreen), "_grid")
        ?? throw new MissingFieldException(nameof(NCardPileScreen), "_grid");

    private static readonly FieldInfo BottomLabelField =
        AccessTools.Field(typeof(NCardPileScreen), "_bottomLabel")
        ?? throw new MissingFieldException(nameof(NCardPileScreen), "_bottomLabel");

    private static void Postfix(NCardPileScreen __instance)
    {
        if (!RandomForeseerSettings.EnableFrozenEye || __instance.Pile.Type != PileType.Draw)
        {
            return;
        }

        if (BottomLabelField.GetValue(__instance) is MegaRichTextLabel bottomLabel)
        {
            bottomLabel.Text = "[center]" + GetFrozenEyeDrawPileInfoText();
        }
    }

    private static bool Prefix(NCardPileScreen __instance)
    {
        if (!RandomForeseerSettings.EnableFrozenEye || __instance.Pile.Type != PileType.Draw)
        {
            return true;
        }

        if (GridField.GetValue(__instance) is not NCardGrid grid)
        {
            return true;
        }

        grid.SetCards(__instance.Pile.Cards.ToList(), PileType.Draw, [SortingOrders.Ascending]);
        return false;
    }

    private static string GetFrozenEyeDrawPileInfoText()
    {
        var originalInfo = new LocString("gameplay_ui", "DRAW_PILE_INFO").GetFormattedText();
        var firstLine = originalInfo.Split('\n', 2)[0];
        var orderInfo = LocManager.Instance.Language switch
        {
            "zhs" => "[gold]（点击查看时卡牌会按实际抽牌顺序显示）[/gold]",
            _ => "[gold](Cards shown are in draw order)[/gold]"
        };

        return $"{firstLine}\n{orderInfo}";
    }
}

[HarmonyPatch(typeof(NCombatCardPile), "OnFocus")]
internal static class FrozenEyeDrawPileHoverTipPatch
{
    private static readonly FieldInfo HoverTipField =
        AccessTools.Field(typeof(NCombatCardPile), "_hoverTip")
        ?? throw new MissingFieldException(nameof(NCombatCardPile), "_hoverTip");

    private static void Prefix(NCombatCardPile __instance)
    {
        if (__instance is not NDrawPileButton)
        {
            return;
        }

        HoverTipField.SetValue(
            __instance,
            RandomForeseerSettings.EnableFrozenEye
                ? CreateFrozenEyeDrawPileHoverTip()
                : CreateVanillaDrawPileHoverTip());
    }

    private static HoverTip CreateVanillaDrawPileHoverTip()
    {
        return new HoverTip(
            new LocString("static_hover_tips", "DRAW_PILE.title"),
            new LocString("static_hover_tips", "DRAW_PILE.description"));
    }

    private static HoverTip CreateFrozenEyeDrawPileHoverTip()
    {
        return new HoverTip(
            new LocString("static_hover_tips", "DRAW_PILE.title"),
            GetFrozenEyeDrawPileHoverTipDescription());
    }

    private static string GetFrozenEyeDrawPileHoverTipDescription()
    {
        var originalDescription = new LocString("static_hover_tips", "DRAW_PILE.description").GetFormattedText();
        var mainDescription = originalDescription.Split("\n\n", 2)[0];
        var viewDescription = LocManager.Instance.Language switch
        {
            "zhs" => "点击这里查看你抽牌堆中的卡牌（按实际抽牌顺序）。",
            _ => "Click to view the cards in your draw pile (in draw order)."
        };

        return $"{mainDescription}\n\n{viewDescription}";
    }
}
