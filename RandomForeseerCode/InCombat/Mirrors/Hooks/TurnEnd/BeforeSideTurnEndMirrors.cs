using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

using Registry = ModelMethodMirrorRegistry<AbstractModel, BeforeSideTurnEndMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.BeforeSideTurnEnd.
internal static class BeforeSideTurnEndMirrors
{
    private static readonly MirrorMethodSpec BeforeSideTurnEndVeryEarly = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeSideTurnEndVeryEarly),
        [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>)]);

    private static readonly MirrorMethodSpec BeforeSideTurnEndEarly = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeSideTurnEndEarly),
        [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>)]);

    private static readonly MirrorMethodSpec BeforeSideTurnEnd = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeSideTurnEnd),
        [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>)]);

    private static readonly Registry VeryEarlyRegistry = CreateVeryEarlyRegistry();
    private static readonly Registry EarlyRegistry = CreateEarlyRegistry();
    private static readonly Registry Registry = CreateRegistry();

    public static void InvokeVeryEarly(AbstractModel listener, BeforeSideTurnEndMirrorContext context)
    {
        VeryEarlyRegistry.Invoke(listener, context);
    }

    public static void InvokeEarly(AbstractModel listener, BeforeSideTurnEndMirrorContext context)
    {
        EarlyRegistry.Invoke(listener, context);
    }

    public static void Invoke(AbstractModel listener, BeforeSideTurnEndMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateVeryEarlyRegistry()
    {
        var registry = new Registry(BeforeSideTurnEndVeryEarly);

        registry.Register<Orichalcum>(OrichalcumMirrors.BeforeSideTurnEndVeryEarly);
        registry.Register<FakeOrichalcum>(OrichalcumMirrors.BeforeSideTurnEndVeryEarly);
        registry.RegisterIgnored<AsleepPower>();

        return registry;
    }

    private static Registry CreateEarlyRegistry()
    {
        var registry = new Registry(BeforeSideTurnEndEarly);

        registry.Register<PlatingPower>(HandlePlatingPower);
        registry.Register<RegenPower>(HandleRegenPower);
        registry.Register<PaelsEye>(HandlePaelsEye);

        return registry;
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(BeforeSideTurnEnd);

        registry.Register<Orichalcum>(OrichalcumMirrors.BeforeSideTurnEnd);
        registry.Register<FakeOrichalcum>(OrichalcumMirrors.BeforeSideTurnEnd);
        registry.Register<CloakClasp>(HandleCloakClasp);
        registry.Register<RippleBasin>(HandleRippleBasin);
        registry.Register<HailstormPower>(HandleHailstormPower);
        registry.Register<ScreamingFlagon>(HandleScreamingFlagon);
        registry.Register<StoneCalendar>(HandleStoneCalendar);
        registry.Register<TheBombPower>(HandleTheBombPower);
        registry.Register<DoomPower>(HandleDoomPower);
        registry.Register<ChainsOfBindingPower>(HandleChainsOfBindingPower);

        registry.RegisterIgnored<Regret>();
        registry.RegisterIgnored<PaelsTears>();
        registry.RegisterIgnored<SandpitPower>();

        return registry;
    }

    private static void HandlePlatingPower(PlatingPower power, BeforeSideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleRegenPower(RegenPower power, BeforeSideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner) && context.State.GetCreature(power.Owner).IsAlive)
        {
            // Mirrors RegenPower.BeforeSideTurnEndEarly's CreatureCmd.Heal call before Doom's
            // normal end-turn kill check. PowerCmd.Decrement is not persisted in prediction state
            // because no later hook in this simulation consumes Regen's decremented amount.
            context.Simulator.Heal(power.Owner, power.Amount);
        }
    }

    private static void HandlePaelsEye(PaelsEye relic, BeforeSideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(relic.Owner.Creature) ||
            relic._usedThisCombat ||
            AnyCardsPlayedThisTurn(relic.Owner) ||
            !relic._wasOwnerPartOfLastPlayerTurn)
        {
            return;
        }

        context.Simulator.ExhaustHand(relic.Owner);
        // The extra-turn scheduling after Pael's Eye resolves is outside the current
        // end-turn simulator, but the immediate hand exhaust side effects are mirrored.
    }

    private static void HandleCloakClasp(CloakClasp relic, BeforeSideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(relic.Owner.Creature))
        {
            return;
        }

        var cardsInHand = context.State.GetPlayerCombatState(relic.Owner).Hand.Cards.Count;
        if (cardsInHand <= 0)
        {
            return;
        }

        context.Simulator.GainBlock(
            relic.Owner.Creature,
            cardsInHand * relic.DynamicVars.Block.BaseValue,
            ValueProp.Unpowered);
    }

    private static void HandleRippleBasin(RippleBasin relic, BeforeSideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(relic.Owner.Creature) ||
            HasPlayedAttackThisTurn(relic.Owner))
        {
            return;
        }

        context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
    }

    private static void HandleHailstormPower(HailstormPower power, BeforeSideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(power.Owner) ||
            power.Owner.Player is not { } player)
        {
            return;
        }

        var frostCount = context.State.GetPlayerCombatState(player).OrbQueue.Orbs
            .Count(static orb => orb is FrostOrb);
        if (frostCount >= power.DynamicVars[HailstormPower.frostOrbKey].IntValue)
        {
            context.Simulator.Damage(context.State.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleScreamingFlagon(ScreamingFlagon relic, BeforeSideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(relic.Owner.Creature) &&
            context.State.GetPlayerCombatState(relic.Owner).Hand.IsEmpty)
        {
            context.Simulator.Damage(context.State.HittableEnemies, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }

    private static void HandleStoneCalendar(StoneCalendar relic, BeforeSideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(relic.Owner.Creature) &&
            relic.Owner.PlayerCombatState?.TurnNumber == relic.DynamicVars[StoneCalendar._damageTurnKey].IntValue)
        {
            context.Simulator.Damage(context.State.HittableEnemies, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }

    private static void HandleTheBombPower(TheBombPower power, BeforeSideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(power.Owner) || power.Amount > 1m)
        {
            return;
        }

        context.Simulator.Damage(context.State.HittableEnemies, power.DynamicVars.Damage, power.Owner);
    }

    private static void HandleDoomPower(DoomPower power, BeforeSideTurnEndMirrorContext context)
    {
        if (context.Side == CombatSide.Player ||
            !context.Participants.Contains(power.Owner) ||
            !context.State.GetCreature(power.Owner).IsAlive)
        {
            return;
        }

        var doomedCreatures = context.State.GetCreaturesOnSide(context.Side)
            .Where(creature =>
                creature.GetPower<DoomPower>() is { } doomPower &&
                context.State.GetCreature(creature).CurrentHp <= doomPower.Amount)
            .ToList();
        if (doomedCreatures.Count > 0)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleChainsOfBindingPower(
        ChainsOfBindingPower power,
        BeforeSideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(power.Owner) ||
            power.Owner.Player is not { } player)
        {
            return;
        }

        foreach (var card in context.State.GetPlayerCombatState(player).AllCards)
        {
            if (card.Preview.Affliction is Bound)
            {
                card.ClearAffliction();
            }
        }
    }

    private static bool HasPlayedAttackThisTurn(Player owner)
    {
        return CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
            entry.HappenedThisTurn(owner.Creature.CombatState) &&
            entry.CardPlay.Card.Type == CardType.Attack &&
            entry.CardPlay.Player == owner);
    }

    private static bool AnyCardsPlayedThisTurn(Player owner)
    {
        if (owner.PlayerCombatState is { TurnNumber: 1 } &&
            owner.Relics.Any(static relic => relic is WhisperingEarring))
        {
            return true;
        }

        return CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
            entry.Actor == owner.Creature &&
            entry.HappenedThisTurn(owner.Creature.CombatState) &&
            !entry.CardPlay.IsAutoPlay);
    }
}

internal sealed class BeforeSideTurnEndMirrorContext : CombatPredictionMirrorContext
{
    public required CombatSide Side { get; init; }

    public required IReadOnlyList<Creature> Participants { get; init; }
}
