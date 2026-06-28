using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(CardRewardAlternative), nameof(CardRewardAlternative.Generate))]
internal static class CardRewardAlternativeSourcePatch
{
    private static readonly ConditionalWeakTable<CardRewardAlternative, CardReward> Sources = [];

    public static bool TryGetSource(CardRewardAlternative alternative, [NotNullWhen(true)] out CardReward? reward)
    {
        return Sources.TryGetValue(alternative, out reward);
    }

    private static void Postfix(CardReward cardReward, IReadOnlyList<CardRewardAlternative> __result)
    {
        foreach (var alternative in __result)
        {
            if (alternative.OptionId is "REROLL" or PaelsWing.sacrificeAlternativeKey)
            {
                Sources.AddOrUpdate(alternative, cardReward);
            }
        }
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
            if (CardRewardAlternativeSourcePatch.TryGetSource(alternative, out var reward))
            {
                CardRewardAlternativeButtonHoverTips.Register(button, reward, alternative);
            }
        }
    }
}

internal static class CardRewardAlternativeButtonHoverTips
{
    private sealed record PredictionContext(CardReward Reward, CardRewardAlternative Alternative);

    private static readonly ConditionalWeakTable<Control, PredictionContext> Contexts = [];

    public static void Register(Control button, CardReward reward, CardRewardAlternative alternative)
    {
        if (!Contexts.TryAdd(button, new PredictionContext(reward, alternative)))
        {
            return;
        }

        button.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(_ => ShowPrediction(button)));
        button.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(_ => HidePrediction(button)));
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        if (!Contexts.TryGetValue(owner, out var context))
        {
            return [];
        }

        return context.Alternative.OptionId switch
        {
            "REROLL" => CardRewardRerollPrediction.GetHoverTips(context.Reward),
            PaelsWing.sacrificeAlternativeKey => PaelsWingSacrificePrediction.GetHoverTips(context.Reward, context.Alternative),
            _ => []
        };
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
