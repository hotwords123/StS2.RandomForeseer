using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(NCardPileScreen), "OnPileContentsChanged")]
internal static class FrozenEyeCardPileScreenPatch
{
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
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class FrozenEyeDrawPileRawTextPatch
{
    private static void Postfix(LocString __instance, ref string __result)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye))
        {
            return;
        }

        switch (__instance)
        {
            case { LocTable: "static_hover_tips", LocEntryKey: "DRAW_PILE.description" }:
            {
                var mainDescription = __result.Split("\n\n", 2)[0];
                var viewDescription = PredictionLocalization.Text("frozen_eye.draw_pile_hover_view").GetRawText();

                __result = $"{mainDescription}\n\n{viewDescription}";
                break;
            }
            case { LocTable: "gameplay_ui", LocEntryKey: "DRAW_PILE_INFO" }:
            {
                var firstLine = __result.Split('\n', 2)[0];
                var orderInfo = PredictionLocalization.Text("frozen_eye.draw_pile_info_order").GetRawText();

                __result = $"{firstLine}\n{orderInfo}";
                break;
            }
        }
    }
}
