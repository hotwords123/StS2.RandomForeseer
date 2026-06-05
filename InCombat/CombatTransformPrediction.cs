using HarmonyLib;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

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

    public static IReadOnlyList<IHoverTip> GetSelectedHolderHoverTips(NSelectedHandCardHolder holder)
    {
        var card = holder.CardModel;
        if (card == null)
        {
            return [];
        }

        return GetCardHoverTips(card);
    }

    public static void RefreshHoverTips(NCardHolder holder)
    {
        if (!holder._isHovered)
        {
            return;
        }

        NHoverTipSet.Remove(holder);
        holder.Call(NCardHolder.MethodName.CreateHoverTips);
    }

    private sealed class CombatTransformPredictionSession(NPlayerHand hand, Rng realRng)
    {
        public NPlayerHand Hand { get; } = hand;

        public IReadOnlyList<IHoverTip> GetHoverTips(CardModel hoveredCard)
        {
            var replacement = PredictReplacement(hoveredCard);
            if (replacement == null)
            {
                return [];
            }

            var tipKey = Hand._selectedCards.Contains(hoveredCard)
                ? "transform_selection_selected"
                : "transform_selection_unselected";
            var textTips = PredictionHoverTips.Text(
                tipKey,
                description => description.Add("TransformedCard", replacement.Title));

            return textTips
                .Concat(PredictionHoverTips.Cards([replacement]))
                .ToList();
        }

        private CardModel? PredictReplacement(CardModel hoveredCard)
        {
            var selectedCards = Hand._selectedCards.ToList();
            var hoveredSelectionIndex = selectedCards.IndexOf(hoveredCard);
            var predictionSequence = hoveredSelectionIndex >= 0
                ? selectedCards.Take(hoveredSelectionIndex + 1)
                : selectedCards.Append(hoveredCard);

            var previewRng = PredictionUtils.CloneRng(realRng);
            CardModel? hoveredReplacement = null;
            foreach (var card in predictionSequence)
            {
                var replacement = PredictTransformResult(card, previewRng);
                if (card == hoveredCard)
                {
                    hoveredReplacement = replacement;
                }
            }

            return hoveredReplacement;
        }

        private static CardModel? PredictTransformResult(CardModel original, Rng rng)
        {
            var options = CardFactory.GetDefaultTransformationOptions(original, isInCombat: true);
            var canonical = rng.NextItem(options);
            return canonical == null
                ? null
                : PredictionUtils.CreatePreviewCard(canonical, original.Owner);
        }
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
    private static bool Prefix(NSelectedHandCardHolder __instance)
    {
        IReadOnlyList<IHoverTip> predictionTips;
        try
        {
            predictionTips = CombatTransformPrediction.GetSelectedHolderHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform selected-card prediction failed: {ex}");
            return true;
        }

        if (predictionTips.Count == 0 || __instance.CardModel == null)
        {
            return true;
        }

        var tips = __instance.CardModel.HoverTips.ToList();
        NHoverTipSet.CreateAndShow(__instance, tips)?.SetAlignmentForCardHolder(__instance);
        return false;
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
        if (__state is not { } card ||
            NPlayerHand.Instance?.GetCardHolder(card) is not { } currentHolder)
        {
            return;
        }

        CombatTransformPrediction.RefreshHoverTips(currentHolder);
    }
}

[HarmonyPatch(typeof(NSelectedHandCardContainer), "DeselectHolder")]
internal static class CombatTransformPredictionDeselectRefreshPatch
{
    private static void Postfix(NCardHolder holder)
    {
        if (holder.CardModel is not { } card ||
            NPlayerHand.Instance?.GetCardHolder(card) is not { } currentHolder)
        {
            return;
        }

        CombatTransformPrediction.RefreshHoverTips(currentHolder);
    }
}
