using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.addons.mega_text;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(NCardPileScreen), "OnPileContentsChanged")]
internal static class FrozenEyeCardPileScreenPatch
{
    private static void Postfix(NCardPileScreen __instance)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye) ||
            __instance.Pile.Type != PileType.Draw)
        {
            return;
        }

        if (__instance._bottomLabel is MegaRichTextLabel bottomLabel)
        {
            bottomLabel.Text = "[center]" + GetFrozenEyeDrawPileInfoText();
        }
    }

    private static bool Prefix(NCardPileScreen __instance)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye) ||
            __instance.Pile.Type != PileType.Draw)
        {
            return true;
        }

        if (__instance._grid is not NCardGrid grid)
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
        var orderInfo = PredictionLocalization.Text("frozen_eye.draw_pile_info_order").GetFormattedText();

        return $"{firstLine}\n{orderInfo}";
    }
}

[HarmonyPatch(typeof(NCombatCardPile), "OnFocus")]
internal static class FrozenEyeDrawPileHoverTipPatch
{
    private static void Prefix(NCombatCardPile __instance)
    {
        if (__instance is not NDrawPileButton)
        {
            return;
        }

        __instance._hoverTip = RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye)
            ? CreateFrozenEyeDrawPileHoverTip()
            : CreateVanillaDrawPileHoverTip();
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
        var viewDescription = PredictionLocalization.Text("frozen_eye.draw_pile_hover_view").GetFormattedText();

        return $"{mainDescription}\n\n{viewDescription}";
    }
}
