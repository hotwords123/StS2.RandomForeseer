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
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class EndTurnHooks
{
    private static readonly HookSpec AfterAutoPostPlayPhaseEntered = new(
        nameof(AbstractModel.AfterAutoPostPlayPhaseEntered),
        [
            typeof(PlayerChoiceContext),
            typeof(Player)
        ]);

    private static readonly HookSpec BeforeSideTurnEndVeryEarly = new(
        nameof(AbstractModel.BeforeSideTurnEndVeryEarly),
        [
            typeof(PlayerChoiceContext),
            typeof(CombatSide),
            typeof(IEnumerable<Creature>)
        ]);

    private static readonly HookSpec BeforeSideTurnEndEarly = new(
        nameof(AbstractModel.BeforeSideTurnEndEarly),
        [
            typeof(PlayerChoiceContext),
            typeof(CombatSide),
            typeof(IEnumerable<Creature>)
        ]);

    private static readonly HookSpec BeforeSideTurnEnd = new(
        nameof(AbstractModel.BeforeSideTurnEnd),
        [
            typeof(PlayerChoiceContext),
            typeof(CombatSide),
            typeof(IEnumerable<Creature>)
        ]);

    private static readonly HookRegistry<AfterAutoPostPlayHookContext> AfterAutoPostPlayPhaseEnteredRegistry =
        CreateAfterAutoPostPlayPhaseEnteredRegistry();

    private static readonly HookRegistry<BeforeSideTurnEndHookContext> BeforeSideTurnEndVeryEarlyRegistry =
        CreateBeforeSideTurnEndVeryEarlyRegistry();

    private static readonly HookRegistry<BeforeSideTurnEndHookContext> BeforeSideTurnEndEarlyRegistry =
        CreateBeforeSideTurnEndEarlyRegistry();

    private static readonly HookRegistry<BeforeSideTurnEndHookContext> BeforeSideTurnEndRegistry =
        CreateBeforeSideTurnEndRegistry();

    public static void RunAfterAutoPostPlayPhaseEntered(AfterAutoPostPlayHookContext context)
    {
        var listeners = context.CombatState.IterateHookListeners().ToList();
        AfterAutoPostPlayPhaseEnteredRegistry.Run(listeners, context);
    }

    public static void RunBeforeSideTurnEnd(BeforeSideTurnEndHookContext context)
    {
        var listeners = context.CombatState.IterateHookListeners().ToList();
        BeforeSideTurnEndVeryEarlyRegistry.Run(listeners, context);
        BeforeSideTurnEndEarlyRegistry.Run(listeners, context);
        BeforeSideTurnEndRegistry.Run(listeners, context);
    }

    private static HookRegistry<AfterAutoPostPlayHookContext> CreateAfterAutoPostPlayPhaseEnteredRegistry()
    {
        var registry = new HookRegistry<AfterAutoPostPlayHookContext>(AfterAutoPostPlayPhaseEntered);

        registry.Register<HowlFromBeyond>(HandleHowlFromBeyond);
        registry.Register<IAmInvincible>(HandleIAmInvincible);
        registry.Register<StampedePower>(HandleStampedePower);

        return registry;
    }

    private static HookRegistry<BeforeSideTurnEndHookContext> CreateBeforeSideTurnEndVeryEarlyRegistry()
    {
        var registry = new HookRegistry<BeforeSideTurnEndHookContext>(BeforeSideTurnEndVeryEarly);

        registry.Register<Orichalcum>(HandleOrichalcumLikeVeryEarly);
        registry.Register<FakeOrichalcum>(HandleOrichalcumLikeVeryEarly);
        registry.RegisterIgnored<AsleepPower>();

        return registry;
    }

    private static HookRegistry<BeforeSideTurnEndHookContext> CreateBeforeSideTurnEndEarlyRegistry()
    {
        var registry = new HookRegistry<BeforeSideTurnEndHookContext>(BeforeSideTurnEndEarly);

        registry.Register<PlatingPower>(HandlePlatingPower);
        registry.Register<RegenPower>(HandleRegenPower);
        registry.Register<PaelsEye>(HandlePaelsEye);

        return registry;
    }

    private static HookRegistry<BeforeSideTurnEndHookContext> CreateBeforeSideTurnEndRegistry()
    {
        var registry = new HookRegistry<BeforeSideTurnEndHookContext>(BeforeSideTurnEnd);

        registry.Register<Orichalcum>(HandleOrichalcumLike);
        registry.Register<FakeOrichalcum>(HandleOrichalcumLike);
        registry.Register<CloakClasp>(HandleCloakClasp);
        registry.Register<RippleBasin>(HandleRippleBasin);
        registry.Register<HailstormPower>(HandleHailstormPower);
        registry.Register<ScreamingFlagon>(HandleScreamingFlagon);
        registry.Register<StoneCalendar>(HandleStoneCalendar);
        registry.Register<TheBombPower>(HandleTheBombPower);
        registry.Register<DoomPower>(HandleDoomPower);
        registry.Register<ChainsOfBindingPower>(HandleChainsOfBindingPower);

        registry.RegisterIgnored<Regret>();
        registry.RegisterIgnored<DiamondDiadem>();
        registry.RegisterIgnored<PaelsTears>();
        registry.RegisterIgnored<SandpitPower>();

        return registry;
    }

    private static void HandleHowlFromBeyond(HowlFromBeyond card, AfterAutoPostPlayHookContext context)
    {
        if (context.Player == card.Owner)
        {
            var exhaustPile = context.State.GetPlayerCombatState(card.Owner).ExhaustPile;
            if (exhaustPile.Find(card) is { } predictedCard)
            {
                context.Simulator.AutoPlay(predictedCard);
            }
        }
    }

    private static void HandleIAmInvincible(IAmInvincible card, AfterAutoPostPlayHookContext context)
    {
        if (context.Player != card.Owner)
        {
            return;
        }

        var drawPile = context.State.GetPlayerCombatState(card.Owner).DrawPile;
        if (drawPile.TopCard?.References(card) is true)
        {
            context.Simulator.AutoPlayFromDrawPile(card.Owner, 1, CardPilePosition.Top, forceExhaust: false);
        }
    }

    private static void HandleStampedePower(StampedePower power, AfterAutoPostPlayHookContext context)
    {
        if (context.Player.Creature != power.Owner)
        {
            return;
        }

        var hand = context.State.GetPlayerCombatState(context.Player).Hand;

        for (var i = 0; i < power.Amount; i++)
        {
            var candidates = hand.Cards
                .Where(static card =>
                    card.Preview.Type == CardType.Attack &&
                    !card.Preview.Keywords.Contains(CardKeyword.Unplayable))
                .ToArray();
            var card = context.Rng.Shuffle.NextItem(candidates);

            if (card != null)
            {
                context.Simulator.AutoPlay(card);
            }
        }
    }

    private static void HandleOrichalcumLikeVeryEarly(RelicModel relic, BeforeSideTurnEndHookContext context)
    {
        if (!context.Participants.Contains(relic.Owner.Creature) ||
            context.State.GetCreature(relic.Owner.Creature).Block > 0)
        {
            return;
        }

        context.StateStore.Get<OrichalcumPredictionState>(relic).ShouldTrigger = true;
    }

    private static void HandlePlatingPower(PlatingPower power, BeforeSideTurnEndHookContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleRegenPower(RegenPower power, BeforeSideTurnEndHookContext context)
    {
        if (context.Participants.Contains(power.Owner) && context.State.GetCreature(power.Owner).IsAlive)
        {
            // StS2 v0.108.0 moved Regen healing to BeforeSideTurnEndEarly, before Doom's
            // normal end-turn kill check. TODO: mirror Heal and power decrement in shadow state.
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandlePaelsEye(PaelsEye relic, BeforeSideTurnEndHookContext context)
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
        context.MarkCurrentSourceRisky();
    }

    private static void HandleOrichalcumLike(RelicModel relic, BeforeSideTurnEndHookContext context)
    {
        var state = context.StateStore.Get<OrichalcumPredictionState>(relic);
        if (!state.ShouldTrigger)
        {
            return;
        }

        state.ShouldTrigger = false;
        context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
    }

    private static void HandleCloakClasp(CloakClasp relic, BeforeSideTurnEndHookContext context)
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

    private static void HandleRippleBasin(RippleBasin relic, BeforeSideTurnEndHookContext context)
    {
        if (!context.Participants.Contains(relic.Owner.Creature) ||
            HasPlayedAttackThisTurn(relic.Owner))
        {
            return;
        }

        context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
    }

    private static void HandleHailstormPower(HailstormPower power, BeforeSideTurnEndHookContext context)
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
            context.Simulator.Damage(
                context.State.GetHittableOpponentsOf(power.Owner),
                power.Amount,
                ValueProp.Unpowered,
                power.Owner);
        }
    }

    private static void HandleScreamingFlagon(ScreamingFlagon relic, BeforeSideTurnEndHookContext context)
    {
        if (context.Participants.Contains(relic.Owner.Creature) &&
            context.State.GetPlayerCombatState(relic.Owner).Hand.IsEmpty)
        {
            context.Simulator.Damage(
                context.State.GetHittableOpponentsOf(relic.Owner.Creature),
                relic.DynamicVars.Damage,
                relic.Owner.Creature);
        }
    }

    private static void HandleStoneCalendar(StoneCalendar relic, BeforeSideTurnEndHookContext context)
    {
        if (context.Participants.Contains(relic.Owner.Creature) &&
            relic.Owner.PlayerCombatState?.TurnNumber == relic.DynamicVars["DamageTurn"].IntValue)
        {
            context.Simulator.Damage(
                context.State.GetHittableOpponentsOf(relic.Owner.Creature),
                relic.DynamicVars.Damage,
                relic.Owner.Creature);
        }
    }

    private static void HandleTheBombPower(TheBombPower power, BeforeSideTurnEndHookContext context)
    {
        if (!context.Participants.Contains(power.Owner) || power.Amount > 1m)
        {
            return;
        }

        context.Simulator.Damage(
            context.State.GetHittableOpponentsOf(power.Owner),
            power.DynamicVars.Damage,
            power.Owner);
    }

    private static void HandleDoomPower(DoomPower power, BeforeSideTurnEndHookContext context)
    {
        if (power.Owner.Side != context.Side ||
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
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleChainsOfBindingPower(ChainsOfBindingPower power, BeforeSideTurnEndHookContext context)
    {
        if (!context.Participants.Contains(power.Owner) ||
            power.Owner.Player is not { } player)
        {
            return;
        }

        var playerState = context.State.GetPlayerCombatState(player);
        ClearBoundAfflictions(playerState.Hand.Cards);
        ClearBoundAfflictions(playerState.DrawPile.Cards);
        ClearBoundAfflictions(playerState.DiscardPile.Cards);
    }

    private static void ClearBoundAfflictions(IEnumerable<PredictedCard> cards)
    {
        foreach (var card in cards)
        {
            if (card.Preview.Affliction is Bound)
            {
                card.MutablePreview.ClearAfflictionInternal();
            }
        }
    }

    private static bool HasPlayedAttackThisTurn(Player owner)
    {
        return CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
            entry.HappenedThisTurn(owner.Creature.CombatState) &&
            entry.CardPlay.Card.Type == CardType.Attack &&
            entry.CardPlay.Card.Owner == owner);
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

internal sealed class OrichalcumPredictionState
{
    public bool ShouldTrigger { get; set; }
}

internal sealed class AfterAutoPostPlayHookContext : CombatPredictionHookContext
{
    public required Player Player { get; init; }
}

internal sealed class BeforeSideTurnEndHookContext : CombatPredictionHookContext
{
    public required CombatSide Side { get; init; }

    public required IReadOnlyList<Creature> Participants { get; init; }
}
