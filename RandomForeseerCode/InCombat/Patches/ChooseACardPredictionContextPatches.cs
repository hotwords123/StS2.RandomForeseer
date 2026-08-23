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
    private static void Prefix(PlayerChoiceContext context, IReadOnlyList<CardModel> cards, out IDisposable? __state)
    {
        __state = ChooseACardPredictionContext.Enter(cards, context.LastInvolvedModel);
    }

    [HarmonyPostfix]
    private static void Postfix(ref Task<CardModel?> __result, IDisposable? __state)
    {
        if (__state is not null)
        {
            __result = __result.WithFinally(__state.Dispose);
        }
    }
}
