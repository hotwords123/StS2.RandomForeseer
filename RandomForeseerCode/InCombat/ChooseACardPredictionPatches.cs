using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class ChooseACardPredictionContext
{
    private static readonly Dictionary<NChooseACardSelectionScreen, HashSet<CardModel>> ScreenCards = [];

    public static bool Contains(CardModel card)
    {
        return ScreenCards.Values.Any(cards => cards.Contains(card));
    }

    public static void Register(NChooseACardSelectionScreen? screen, IReadOnlyList<CardModel> cards)
    {
        if (screen == null || cards.Count == 0)
        {
            return;
        }

        ScreenCards[screen] = cards.ToHashSet();
    }

    public static void Unregister(NChooseACardSelectionScreen screen)
    {
        ScreenCards.Remove(screen);
    }
}

[HarmonyPatch(typeof(NChooseACardSelectionScreen))]
internal static class ChooseACardPredictionScreenPatches
{
    [HarmonyPatch(nameof(NChooseACardSelectionScreen.ShowScreen))]
    [HarmonyPostfix]
    private static void Postfix(
        IReadOnlyList<CardModel> cards,
        NChooseACardSelectionScreen? __result)
    {
        ChooseACardPredictionContext.Register(__result, cards);
    }

    [HarmonyPatch(nameof(NChooseACardSelectionScreen._ExitTree))]
    [HarmonyPostfix]
    private static void PostfixExitTree(NChooseACardSelectionScreen __instance)
    {
        ChooseACardPredictionContext.Unregister(__instance);
    }
}
