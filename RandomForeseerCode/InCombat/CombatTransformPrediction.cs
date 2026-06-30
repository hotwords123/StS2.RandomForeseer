using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatTransformPrediction
{
    private static CombatTransformPredictionSession? _session;

    public static void BeginSession(NPlayerHand hand, AbstractModel? source)
    {
        _session = null;

        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatTransformPrediction))
        {
            return;
        }

        var realRng = source switch
        {
            EntropyPower entropyPower => entropyPower.Owner?.Player?.RunState.Rng.CombatCardSelection,
            _ => null
        };

        if (realRng != null)
        {
            _session = new CombatTransformPredictionSession(hand, realRng);
        }
    }

    public static void EndSession(NPlayerHand hand)
    {
        if (_session?.Hand == hand)
        {
            _session = null;
        }
    }

    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!card.IsMutable)
        {
            return [];
        }

        var hand = NPlayerHand.Instance;
        if (hand == null ||
            _session is not { } session ||
            session.Hand != hand ||
            hand.GetCardHolder(card) == null)
        {
            return [];
        }

        return session.GetHoverTips(card);
    }

    private sealed class CombatTransformPredictionSession(NPlayerHand hand, Rng realRng)
    {
        public NPlayerHand Hand { get; } = hand;

        public IReadOnlyList<IHoverTip> GetHoverTips(CardModel hoveredCard)
        {
            return TransformPrediction.GetHoverTips(
                hoveredCard,
                Hand._selectedCards,
                Hand._prefs.MaxSelect,
                realRng,
                isInCombat: true);
        }
    }
}

internal static class CombatTransformSelectedHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        if (owner is not NSelectedHandCardHolder { CardModel: { } card })
        {
            return [];
        }

        return CombatTransformPrediction.GetCardHoverTips(card);
    }

    public static void RefreshHoverTips(CardModel? card)
    {
        if (card == null ||
            NPlayerHand.Instance?.GetCardHolder(card) is not { } holder ||
            !holder._isHovered)
        {
            return;
        }

        NHoverTipSet.Remove(holder);
        holder.Call(NCardHolder.MethodName.CreateHoverTips);
    }
}

[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand.SelectCards))]
internal static class CombatTransformPredictionSessionPatch
{
    private static void Prefix(NPlayerHand __instance, AbstractModel? source)
    {
        CombatTransformPrediction.BeginSession(__instance, source);
    }
}

[HarmonyPatch(typeof(NPlayerHand), "AfterCardsSelected")]
internal static class CombatTransformPredictionSessionCleanupPatch
{
    private static void Prefix(NPlayerHand __instance)
    {
        CombatTransformPrediction.EndSession(__instance);
    }
}

[HarmonyPatch(typeof(NSelectedHandCardHolder), "CreateHoverTips")]
internal static class CombatTransformPredictionSelectedHoverTipsPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NSelectedHandCardHolder __instance)
    {
        PredictionHoverTipSetHelper.EnsureHoverTipSet(__instance)?.SetAlignmentForCardHolder(__instance);
    }
}

[HarmonyPatch(typeof(NPlayerHand), "SelectCardInSimpleMode")]
internal static class CombatTransformPredictionSelectRefreshPatch
{
    private static void Prefix(NHandCardHolder holder, out CardModel? __state)
    {
        __state = holder.CardModel;
    }

    private static void Postfix(CardModel? __state)
    {
        CombatTransformSelectedHoverTips.RefreshHoverTips(__state);
    }
}

[HarmonyPatch(typeof(NSelectedHandCardContainer), "DeselectHolder")]
internal static class CombatTransformPredictionDeselectRefreshPatch
{
    private static void Postfix(NCardHolder holder)
    {
        CombatTransformSelectedHoverTips.RefreshHoverTips(holder.CardModel);
    }
}
