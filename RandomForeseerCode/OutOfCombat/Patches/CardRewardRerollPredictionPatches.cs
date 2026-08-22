using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rewards;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(CardRewardAlternative), nameof(CardRewardAlternative.Generate))]
internal static class CardRewardAlternativeSourcePatch
{
    private static void Postfix(CardReward cardReward, IReadOnlyList<CardRewardAlternative> __result)
    {
        CardRewardAlternativeButtonHoverTips.RegisterReward(cardReward, __result);
    }
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
internal static class CardRewardAlternativeButtonHoverTipPatch
{
    private static void Postfix(IReadOnlyList<CardRewardAlternative> extraOptions, NCardRewardSelectionScreen __instance)
    {
        var buttons = __instance
            .GetNode<Control>("UI/RewardAlternatives")
            .GetChildren()
            .OfType<NCardRewardAlternativeButton>();

        foreach (var (button, alternative) in buttons.Zip(extraOptions))
        {
            CardRewardAlternativeButtonHoverTips.RegisterButton(button, alternative);
        }
    }
}
