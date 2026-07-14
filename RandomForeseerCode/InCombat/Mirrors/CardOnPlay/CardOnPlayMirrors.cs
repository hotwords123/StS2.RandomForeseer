using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

using Registry = ModelMethodMirrorRegistry<CardModel, CardOnPlayMirrorContext>;

// Simulation-facing facade and central registration index for mirrored CardModel.OnPlay behavior.
internal static class CardOnPlayMirrors
{
    private static readonly MirrorMethodSpec OnPlay = new(
        typeof(CardModel),
        "OnPlay",
        BindingFlags.Instance | BindingFlags.NonPublic,
        [typeof(PlayerChoiceContext), typeof(CardPlay)]);

    private static readonly Registry Registry = CreateRegistry();

    public static MirrorDispatchResult Invoke(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        CardPlay cardPlay)
    {
        // The mutable preview is the receiver because OnPlay handlers may mutate the played card.
        // CardOnPlayMirrorContext maps its source back to the original card and exposes that same
        // original model as the StateStore key.
        return Registry.Invoke(card.MutablePreview, new()
        {
            Simulator = simulator,
            Card = card,
            CardPlay = cardPlay
        });
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(OnPlay);

        registry.Register<FlakCannon>(RandomTargetAttackCardMirrors.FlakCannonOnPlay);
        registry.Register<Ricochet>(RandomTargetAttackCardMirrors.RicochetOnPlay);
        registry.Register<RipAndTear>(RandomTargetAttackCardMirrors.RipAndTearOnPlay);
        registry.Register<Stardust>(RandomTargetAttackCardMirrors.StardustOnPlay);
        registry.Register<SweepingGaze>(RandomTargetAttackCardMirrors.SweepingGazeOnPlay);
        registry.Register<SwordBoomerang>(RandomTargetAttackCardMirrors.SwordBoomerangOnPlay);
        registry.Register<Volley>(RandomTargetAttackCardMirrors.VolleyOnPlay);

        return registry;
    }
}
