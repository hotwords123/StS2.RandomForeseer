using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using Cards = MegaCrit.Sts2.Core.Models.Cards;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

// Mirrors the prediction-relevant parts of Hook.AfterCardDrawn.
internal static class AfterCardDrawnHook
{
    private static readonly HookSpec AfterCardDrawnEarly = new(
        nameof(AbstractModel.AfterCardDrawnEarly),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel),
            typeof(bool)
        ]);

    private static readonly HookSpec AfterCardDrawn = new(
        nameof(AbstractModel.AfterCardDrawn),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel),
            typeof(bool)
        ]);

    private static readonly HookRegistry<AfterCardDrawnHookContext> Early = CreateEarly();
    private static readonly HookRegistry<AfterCardDrawnHookContext> Normal = CreateNormal();

    public static void Run(AfterCardDrawnHookContext context)
    {
        Early.Run(context.CombatState.IterateHookListeners(), context);
        Normal.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardDrawnHookContext> CreateEarly()
    {
        var registry = new HookRegistry<AfterCardDrawnHookContext>(AfterCardDrawnEarly);

        registry.Register<HellraiserPower>(HandleHellraiserPower);

        return registry;
    }

    private static HookRegistry<AfterCardDrawnHookContext> CreateNormal()
    {
        var registry = new HookRegistry<AfterCardDrawnHookContext>(AfterCardDrawn);

        registry.Register<ConfusedPower>(HandleConfusedPower);
        registry.Register<Slither>(HandleSlither);
        registry.Register<IterationPower>(HandleIterationPower);
        registry.Register<PagestormPower>(HandlePagestormPower);
        registry.Register<ChainsOfBindingPower>(HandleChainsOfBindingPower);
        registry.Register<CorrosiveWavePower>(HandleDriftRiskIfOwner);
        registry.Register<SpeedsterPower>(HandleSpeedsterPower);
        registry.Register<CacophonyPower>(HandleCacophonyPower);
        registry.Register<KinglyKick>(HandleKinglyKick);
        registry.Register<KinglyPunch>(HandleKinglyPunch);
        registry.Register<AutomationPower>(HandleAutomationPower);
        registry.Register<Cards.Void>(HandleVoid);

        return registry;
    }

    private static void HandleHellraiserPower(HellraiserPower power, AfterCardDrawnHookContext context)
    {
        // Hellraiser auto-plays the drawn Strike; arbitrary auto-play command effects are
        // not mirrored here.
        if (context.PreviewCard.Owner.Creature == power.Owner &&
            context.PreviewCard.Tags.Contains(CardTag.Strike))
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleConfusedPower(ConfusedPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner != power.Owner?.Player ||
            context.PreviewCard.EnergyCost.Canonical < 0)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.Rng.CombatEnergyCosts.NextInt(4));
    }

    private static void HandleSlither(Slither slither, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != slither.Card)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.Rng.CombatEnergyCosts.NextInt(4));
    }

    private static void HandleIterationPower(IterationPower power, AfterCardDrawnHookContext context)
    {
        if (power.Owner.Player is { } player &&
            context.PreviewCard.Owner == player &&
            context.PreviewCard.Type == CardType.Status &&
            CountStatusCardsDrawnThisTurn(context.Simulator, player) <= 1)
        {
            context.Simulator.Draw(player, power.Amount);
        }
    }

    private static void HandlePagestormPower(PagestormPower power, AfterCardDrawnHookContext context)
    {
        if (power.Owner.Player is { } player &&
            context.PreviewCard.Owner == player &&
            context.Card.GetKeywords(context.State).Contains(CardKeyword.Ethereal))
        {
            context.Simulator.Draw(player, power.Amount);
        }
    }

    private static void HandleChainsOfBindingPower(ChainsOfBindingPower power, AfterCardDrawnHookContext context)
    {
        if (power.Owner.Player is { } player &&
            context.PreviewCard.Owner == player &&
            context.CombatState.CurrentSide == power.Owner.Side &&
            // CanAfflict only checks card type, existing affliction, and Unplayable.
            // Vanilla currently has no global hook that adds Unplayable, so preview keywords are sufficient here.
            ModelDb.Affliction<Bound>().CanAfflict(context.PreviewCard) &&
            CountBoundCardsAfflictedThisTurn(context.Simulator, player) < power.Amount)
        {
            context.Simulator.Afflict<Bound>(context.Card, power.Amount);
        }
    }

    private static void HandleSpeedsterPower(SpeedsterPower power, AfterCardDrawnHookContext context)
    {
        if (context.FromHandDraw ||
            context.PreviewCard.Owner.Creature != power.Owner ||
            context.PreviewCard.Owner.Creature.Side != context.CombatState.CurrentSide)
        {
            return;
        }

        context.Simulator.Damage(context.State.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
    }

    private static void HandleCacophonyPower(CacophonyPower power, AfterCardDrawnHookContext context)
    {
        var state = context.StateStore.Get(power, () => new CacophonyPredictionState
        {
            CardsDrawn = power.DynamicVars.Cards.IntValue
        });
        state.CardsDrawn--;

        if (state.CardsDrawn <= 0)
        {
            var target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);

            if (target != null)
            {
                context.Simulator.Damage(target, power.Amount, ValueProp.Unpowered, power.Owner);
            }

            // Use the canonical value to avoid hard-coding the number threshold in case it changes in the future.
            state.CardsDrawn = power.CanonicalInstance.DynamicVars.Cards.IntValue;
        }
    }

    private static void HandleKinglyKick(KinglyKick card, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != card)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.AddThisCombat(-1);
    }

    private static void HandleKinglyPunch(KinglyPunch card, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != card)
        {
            return;
        }

        var previewCard = (KinglyPunch)context.MutablePreviewCard;
        var damageIncrease = previewCard.DynamicVars["Increase"].BaseValue;
        previewCard.DynamicVars.Damage.BaseValue += damageIncrease;
        previewCard.ExtraDamage += damageIncrease;
    }

    private static void HandleAutomationPower(AutomationPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner != power.Owner?.Player)
        {
            return;
        }

        var state = context.StateStore.Get(power, () => new AutomationPredictionState
        {
            CardsLeft = power.DisplayAmount
        });
        state.CardsLeft--;

        if (state.CardsLeft > 0)
        {
            return;
        }

        context.Simulator.GainEnergy(context.PreviewCard.Owner, power.Amount);
        state.CardsLeft = power.DynamicVars["BaseCards"].IntValue;
    }

    private static void HandleVoid(Cards.Void card, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard == card)
        {
            context.Simulator.LoseEnergy(card.Owner, card.DynamicVars.Energy.IntValue);
        }
    }

    private static void HandleDriftRiskIfOwner(PowerModel power, AfterCardDrawnHookContext context)
    {
        // CorrosiveWavePower applies Poison to all hittable enemies; power application
        // side effects are not mirrored here.
        if (context.PreviewCard.Owner.Creature == power.Owner)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static int CountStatusCardsDrawnThisTurn(CombatPredictionSimulator simulator, Player player)
    {
        var count = CombatManager.Instance.History.Entries
            .OfType<CardDrawnEntry>()
            .Count(entry =>
                entry.HappenedThisTurn(simulator.State.CombatState) &&
                entry.Actor == player.Creature &&
                entry.Card.Type == CardType.Status);
        count += simulator.CardDrawnHistory
            .Count(entry => entry.Card.Preview.Owner == player && entry.Card.Preview.Type == CardType.Status);
        return count;
    }

    private static int CountBoundCardsAfflictedThisTurn(CombatPredictionSimulator simulator, Player player)
    {
        var count = CombatManager.Instance.History.Entries
            .OfType<CardAfflictedEntry>()
            .Count(entry =>
                entry.HappenedThisTurn(simulator.State.CombatState) &&
                entry.Actor == player.Creature &&
                entry.Affliction is Bound);
        count += simulator.CardAfflictedHistory
            .Count(entry => entry.Card.Preview.Owner == player && entry.Affliction is Bound);
        return count;
    }
}

internal sealed class CacophonyPredictionState
{
    public int CardsDrawn { get; set; }
}

internal sealed class AutomationPredictionState
{
    public int CardsLeft { get; set; }
}

internal sealed class AfterCardDrawnHookContext : CombatPredictionCardHookContext
{
    public required bool FromHandDraw { get; init; }

}
