using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Orb;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

// Simulation-facing facade for mirrored combat hooks, analogous to vanilla Hook. Callers pass
// ordinary hook arguments; this class owns mirror context construction, listener enumeration, and
// hook-level ordering while method-specific registries and contexts remain implementation details.
internal static class HookMirrors
{
    // Mirrors Hook.ShouldDraw with listener short-circuiting.
    public static bool ShouldDraw(
        CombatPredictionSimulator simulator,
        Player player,
        bool fromHandDraw,
        [NotNullWhen(false)] out AbstractModel? modifier)
    {
        var context = new ShouldDrawMirrorContext
        {
            Simulator = simulator,
            Player = player,
            FromHandDraw = fromHandDraw
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            if (!ShouldDrawMirrors.Invoke(listener, context))
            {
                modifier = listener;
                return false;
            }
        }

        modifier = null;
        return true;
    }

    // Mirrors Hook.AfterCardDrawnEarly followed by Hook.AfterCardDrawn.
    public static void AfterCardDrawn(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        bool fromHandDraw)
    {
        var context = new AfterCardDrawnMirrorContext
        {
            Simulator = simulator,
            Card = card,
            FromHandDraw = fromHandDraw
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterCardDrawnMirrors.InvokeEarly(listener, context);
        }

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterCardDrawnMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCardExhausted.
    public static void AfterCardExhausted(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        bool causedByEthereal)
    {
        var context = new AfterCardExhaustedMirrorContext
        {
            Simulator = simulator,
            Card = card,
            CausedByEthereal = causedByEthereal
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterCardExhaustedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.ModifyShuffleOrder.
    public static void ModifyShuffleOrder(
        CombatPredictionSimulator simulator,
        Player player,
        List<PredictedCard> cards,
        bool isInitialShuffle)
    {
        var context = new ModifyShuffleOrderMirrorContext
        {
            Simulator = simulator,
            Player = player,
            Cards = cards,
            IsInitialShuffle = isInitialShuffle
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            ModifyShuffleOrderMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterShuffle.
    public static void AfterShuffle(CombatPredictionSimulator simulator, Player player)
    {
        var context = new AfterShuffleMirrorContext { Simulator = simulator, Player = player };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterShuffleMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCardDiscarded.
    public static void AfterCardDiscarded(CombatPredictionSimulator simulator, PredictedCard card)
    {
        var context = new AfterCardDiscardedMirrorContext { Simulator = simulator, Card = card };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterCardDiscardedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCardGeneratedForCombat.
    public static void AfterCardGeneratedForCombat(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        Player? creator)
    {
        var context = new AfterCardGeneratedForCombatMirrorContext
        {
            Simulator = simulator,
            Card = card,
            Creator = creator
        };

        // Prediction-local generated cards are not included as later listeners until simulated
        // hook iteration owns prediction-local card listeners.
        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterCardGeneratedForCombatMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCurrentHpChanged.
    public static void AfterCurrentHpChanged(
        CombatPredictionSimulator simulator,
        Creature creature,
        decimal delta)
    {
        var context = new AfterCurrentHpChangedMirrorContext
        {
            Simulator = simulator,
            Creature = creature,
            Delta = delta
        };

        foreach (var listener in context.RunState.IterateHookListeners(context.CombatState))
        {
            AfterCurrentHpChangedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterDamageGiven.
    public static void AfterDamageGiven(
        CombatPredictionSimulator simulator,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        PredictedCard? source)
    {
        var context = new AfterDamageGivenMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Result = result,
            Props = props,
            Dealer = dealer,
            Source = source
        };

        foreach (var listener in context.RunState.IterateHookListeners(context.CombatState))
        {
            AfterDamageGivenMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterModifyingHpLostAfterOsty for the modifiers selected by the value hook.
    public static void AfterModifyingHpLostAfterOsty(
        CombatPredictionSimulator simulator,
        IEnumerable<AbstractModel> modifiers)
    {
        var context = new AfterModifyingHpLostMirrorContext { Simulator = simulator };

        foreach (var modifier in context.RunState.IterateHookListeners(context.CombatState))
        {
            if (modifiers.Contains(modifier))
            {
                AfterModifyingHpLostAfterOstyMirrors.Invoke(modifier, context);
            }
        }
    }

    // Mirrors Hook.ModifyOrbPassiveTriggerCount.
    public static int ModifyOrbPassiveTriggerCount(
        CombatPredictionSimulator simulator,
        OrbModel orb,
        int triggerCount,
        out List<AbstractModel> modifiers)
    {
        var context = new ModifyOrbPassiveTriggerCountMirrorContext
        {
            Simulator = simulator,
            Orb = orb,
            TriggerCount = triggerCount
        };
        modifiers = [];

        foreach (var listener in context.State.IterateHookListeners())
        {
            var newTriggerCount = ModifyOrbPassiveTriggerCountMirrors.Invoke(listener, context);
            if (newTriggerCount != context.TriggerCount)
            {
                context.TriggerCount = newTriggerCount;
                modifiers.Add(listener);
            }
        }

        return context.TriggerCount;
    }

    // Mirrors Hook.AfterOrbChanneled.
    public static void AfterOrbChanneled(CombatPredictionSimulator simulator, Player player, OrbModel orb)
    {
        var context = new AfterOrbChanneledMirrorContext
        {
            Simulator = simulator,
            Player = player,
            Orb = orb
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterOrbChanneledMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterOrbEvoked.
    public static void AfterOrbEvoked(
        CombatPredictionSimulator simulator,
        OrbModel orb,
        IReadOnlyList<Creature> targets)
    {
        var context = new AfterOrbEvokedMirrorContext
        {
            Simulator = simulator,
            Orb = orb,
            Targets = targets
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterOrbEvokedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterAutoPostPlayPhaseEntered.
    public static void AfterAutoPostPlayPhaseEntered(CombatPredictionSimulator simulator, Player player)
    {
        var context = new AfterAutoPostPlayMirrorContext { Simulator = simulator, Player = player };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterAutoPostPlayPhaseEnteredMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.BeforeSideTurnEnd.
    public static void BeforeSideTurnEnd(
        CombatPredictionSimulator simulator,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        var context = new BeforeSideTurnEndMirrorContext
        {
            Simulator = simulator,
            Side = side,
            Participants = participants
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            BeforeSideTurnEndMirrors.InvokeVeryEarly(listener, context);
        }

        foreach (var listener in context.State.IterateHookListeners())
        {
            BeforeSideTurnEndMirrors.InvokeEarly(listener, context);
        }

        foreach (var listener in context.State.IterateHookListeners())
        {
            BeforeSideTurnEndMirrors.Invoke(listener, context);
        }
    }
}
