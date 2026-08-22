using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

/// <summary>Owns prediction context and HoverTip presentation for card-reward alternative buttons.</summary>
internal static class CardRewardAlternativeButtonHoverTips
{
    private const string RerollKey = "REROLL";
    private const string SacrificeKey = PaelsWing.sacrificeAlternativeKey;

    private sealed record PredictionContext(CardReward Reward, CardRewardAlternative Alternative);

    private static readonly ConditionalWeakTable<CardRewardAlternative, CardReward> Sources = [];
    private static readonly ConditionalWeakTable<Control, PredictionContext> Contexts = [];

    public static void RegisterReward(CardReward reward, IEnumerable<CardRewardAlternative> alternatives)
    {
        foreach (var alternative in alternatives)
        {
            if (alternative.OptionId is RerollKey or SacrificeKey)
            {
                Sources.AddOrUpdate(alternative, reward);
            }
        }
    }

    public static void RegisterButton(Control button, CardRewardAlternative alternative)
    {
        if (!Sources.TryGetValue(alternative, out var reward) ||
            !Contexts.TryAdd(button, new PredictionContext(reward, alternative)))
        {
            return;
        }

        button.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(ShowPrediction));
        button.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(HidePrediction));
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        if (!Contexts.TryGetValue(owner, out var context))
        {
            return [];
        }

        return context.Alternative.OptionId switch
        {
            "REROLL" =>
                CardRewardRerollPrediction.GetHoverTips(context.Reward),
            PaelsWing.sacrificeAlternativeKey =>
                PaelsWingSacrificePrediction.GetHoverTips(context.Reward, context.Alternative),
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
