using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatTransformPrediction
{
    private static CombatTransformPredictionSession? _session;

    public static void BeginSession(NPlayerHand hand, CardSelectorPrefs prefs, AbstractModel? source)
    {
        _session = null;

        var realRng = source switch
        {
            EntropyPower entropyPower => entropyPower.Owner.Player?.RunState.Rng.CombatCardSelection,
            _ => null
        };

        if (realRng != null)
        {
            _session = new CombatTransformPredictionSession(hand, prefs.MaxSelect, realRng);
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
        if (_session is not { } session)
        {
            return [];
        }

        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.CombatTransformPredictionEnabled)
        {
            return [];
        }

        return session.GetHoverTips(card);
    }

    private sealed class CombatTransformPredictionSession(NPlayerHand hand, int maxSelect, Rng realRng)
    {
        private readonly TransformPrediction _prediction = new(maxSelect, isInCombat: true);

        public NPlayerHand Hand { get; } = hand;

        public IReadOnlyList<IHoverTip> GetHoverTips(CardModel hoveredCard)
        {
            return Hand.GetCardHolder(hoveredCard) is not null
                ? _prediction.GetHoverTips(hoveredCard, Hand._selectedCards, realRng.Clone())
                : [];
        }
    }
}

internal static class CombatTransformSelectedHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        return owner is NSelectedHandCardHolder { CardModel: { } card }
            ? CombatTransformPrediction.GetCardHoverTips(card)
            : [];
    }
}
