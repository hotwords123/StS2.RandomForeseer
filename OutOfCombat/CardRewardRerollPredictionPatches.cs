using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rewards;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(CardRewardAlternative), nameof(CardRewardAlternative.Generate))]
internal static class CardRewardAlternativeRerollSourcePatch
{
    private static readonly ConditionalWeakTable<CardRewardAlternative, CardRewardBox> RerollSources = [];

    public static bool TryGetRerollSource(CardRewardAlternative alternative, [NotNullWhen(true)] out CardReward? reward)
    {
        if (RerollSources.TryGetValue(alternative, out var box))
        {
            reward = box.Reward;
            return true;
        }

        reward = null;
        return false;
    }

    private static void Postfix(CardReward cardReward, IReadOnlyList<CardRewardAlternative> __result)
    {
        foreach (var alternative in __result.Where(alternative => alternative.OptionId == "REROLL"))
        {
            RerollSources.Remove(alternative);
            RerollSources.Add(alternative, new CardRewardBox(cardReward));
        }
    }

    private sealed class CardRewardBox(CardReward reward)
    {
        public CardReward Reward { get; } = reward;
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

            var button = buttons[i];
            button.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(_ => ShowPrediction(button, reward)));
            button.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(_ => HidePrediction(button)));
            button.Connect(Node.SignalName.TreeExiting, Callable.From(() => HidePrediction(button)));
        }
    }

    private static void ShowPrediction(Control button, CardReward reward)
    {
        try
        {
            HidePrediction(button);
            var tips = CardRewardRerollPrediction.GetHoverTips(reward);
            if (tips.Count == 0)
            {
                return;
            }

            NHoverTipSet.CreateAndShow(button, tips, GetSideAlignment(button));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Driftwood reroll prediction failed: {ex}");
        }
    }

    private static void HidePrediction(Control button)
    {
        NHoverTipSet.Remove(button);
    }

    private static HoverTipAlignment GetSideAlignment(Control button)
    {
        var viewportWidth = button.GetViewport().GetVisibleRect().Size.X;
        var buttonCenterX = button.GlobalPosition.X + button.Size.X * button.Scale.X / 2f;

        // NHoverTipSet places card tips on the opposite side of the text alignment for Control owners.
        return buttonCenterX < viewportWidth / 2f
            ? HoverTipAlignment.Left
            : HoverTipAlignment.Right;
    }
}
