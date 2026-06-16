using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(CardRewardAlternative), nameof(CardRewardAlternative.Generate))]
internal static class CardRewardAlternativeRerollSourcePatch
{
    private static readonly ConditionalWeakTable<CardRewardAlternative, CardReward> RerollSources = [];

    public static bool TryGetRerollSource(CardRewardAlternative alternative, [NotNullWhen(true)] out CardReward? reward)
    {
        return RerollSources.TryGetValue(alternative, out reward);
    }

    private static void Postfix(CardReward cardReward, IReadOnlyList<CardRewardAlternative> __result)
    {
        foreach (var alternative in __result.Where(alternative => alternative.OptionId == "REROLL"))
        {
            RerollSources.AddOrUpdate(alternative, cardReward);
        }
    }
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
internal static class CardRewardRerollButtonHoverTipPatch
{
    private static void Postfix(IReadOnlyList<CardRewardAlternative> extraOptions, NCardRewardSelectionScreen __instance)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableDriftwoodRerollPrediction))
        {
            return;
        }

        var buttons = __instance
            .GetNode<Control>("UI/RewardAlternatives")
            .GetChildren()
            .OfType<NCardRewardAlternativeButton>()
            .ToList();
        var count = Math.Min(buttons.Count, extraOptions.Count);

        for (var i = 0; i < count; i++)
        {
            var alternative = extraOptions[i];
            if (!CardRewardAlternativeRerollSourcePatch.TryGetRerollSource(alternative, out var reward))
            {
                continue;
            }

            CardRewardRerollButtonHoverTips.Register(buttons[i], reward);
        }
    }
}

internal static class CardRewardRerollButtonHoverTips
{
    private static readonly ConditionalWeakTable<Control, CardReward> RerollRewards = [];

    public static void Register(Control button, CardReward reward)
    {
        if (!RerollRewards.TryAdd(button, reward))
        {
            return;
        }

        button.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(_ => ShowPrediction(button)));
        button.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(_ => HidePrediction(button)));
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        return RerollRewards.TryGetValue(owner, out var reward)
            ? CardRewardRerollPrediction.GetHoverTips(reward)
            : [];
    }

    private static void ShowPrediction(Control button)
    {
        PredictionHoverTipSetHelper.EnsureHoverTipSet(button, HoverTip.GetHoverTipAlignment(button));
    }

    private static void HidePrediction(Control button)
    {
        PredictionHoverTipSetHelper.RemoveOwnedHoverTipSet(button);
    }
}
