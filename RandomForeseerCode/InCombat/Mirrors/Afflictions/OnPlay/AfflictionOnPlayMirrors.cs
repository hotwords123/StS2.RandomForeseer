using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Afflictions.OnPlay;

using Registry = MethodMirrorRegistry<AfflictionModel, AfflictionOnPlayMirrorContext>;

// Vanilla afflictions currently inherit the empty base OnPlay implementation. Keep an empty
// registry so future vanilla or gameplay-mod overrides are detected by the normal mirror policy.
internal static class AfflictionOnPlayMirrors
{
    private static readonly MirrorMethodSpec OnPlay = new(
        typeof(AfflictionModel),
        nameof(AfflictionModel.OnPlay),
        BindingFlags.Instance | BindingFlags.Public,
        [typeof(PlayerChoiceContext), typeof(Creature)]);

    private static readonly Registry Registry = new(OnPlay);

    public static MirrorDispatchResult Invoke(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        Creature? target,
        AfflictionModel affliction)
    {
        return Registry.Invoke(affliction, new AfflictionOnPlayMirrorContext
        {
            Simulator = simulator,
            Card = card,
            Target = target
        });
    }
}
