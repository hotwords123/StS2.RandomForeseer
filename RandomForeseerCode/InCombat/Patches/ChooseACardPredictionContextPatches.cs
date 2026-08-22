using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
internal static class ChooseACardPredictionContextPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        out ChooseACardPredictionContext.Registration? __state)
    {
        __state = ChooseACardPredictionContext.Register(cards, context.LastInvolvedModel);
    }

    [HarmonyPostfix]
    private static void Postfix(
        ref Task<CardModel?> __result,
        ChooseACardPredictionContext.Registration? __state)
    {
        if (__state is not null)
        {
            __result = __result.WithFinally(() => ChooseACardPredictionContext.Unregister(__state));
        }
    }
}
