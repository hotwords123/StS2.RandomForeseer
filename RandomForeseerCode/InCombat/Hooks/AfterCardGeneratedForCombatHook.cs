using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

// Mirrors the prediction-relevant parts of Hook.AfterCardGeneratedForCombat.
internal static class AfterCardGeneratedForCombatHook
{
    private static readonly HookSpec AfterCardGeneratedForCombat = new(
        nameof(AbstractModel.AfterCardGeneratedForCombat),
        [
            typeof(CardModel),
            typeof(Player)
        ]);

    private static readonly HookRegistry<AfterCardGeneratedForCombatHookContext> Registry = CreateRegistry();

    public static void Run(AfterCardGeneratedForCombatHookContext context)
    {
        // TODO: This mirrors vanilla Hook.AfterCardGeneratedForCombat listener traversal only
        // over the live CombatState. Cards generated only inside the simulator are not included
        // as later hook listeners until simulated hook iteration owns prediction-local cards.
        Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardGeneratedForCombatHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterCardGeneratedForCombatHookContext>(AfterCardGeneratedForCombat);

        registry.Register<Aeonglass>(HandleAeonglass);
        registry.Register<ArsenalPower>(HandleArsenalPower);
        registry.Register<Regalite>(HandleRegalite);
        registry.Register<SoulboundPower>(HandleSoulboundPower);
        registry.Register<PillarOfCreationPower>(HandlePillarOfCreationPower);
        registry.Register<SmokestackPower>(HandleSmokestackPower);
        registry.Register<TrashToTreasurePower>(HandleTrashToTreasurePower);
        registry.Register<RocketPunch>(HandleRocketPunch);

        return registry;
    }

    private static void HandleAeonglass(Aeonglass monster, AfterCardGeneratedForCombatHookContext context)
    {
        if (context.MutablePreviewCard is Wither wither)
        {
            for (var i = 0; i < monster.WitherUpgradeCount; i++)
            {
                wither.FakeUpgrade();
            }
        }
    }

    private static void HandleArsenalPower(ArsenalPower power, AfterCardGeneratedForCombatHookContext context)
    {
        // Vanilla applies Strength. Power application/removal is outside the simulator's
        // current state domain, so this remains a risk marker.
        if (context.Creator?.Creature == power.Owner)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleRegalite(Regalite relic, AfterCardGeneratedForCombatHookContext context)
    {
        if (context.Creator == relic.Owner)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }

    private static void HandleSoulboundPower(SoulboundPower power, AfterCardGeneratedForCombatHookContext context)
    {
        if (context.Creator?.Creature != power.Applier
            || context.PreviewCard is not Soul
            || power.Owner.Player is not { } player)
        {
            return;
        }

        var state = context.StateStore.Get(power, static () => new SoulboundPredictionState());
        if (state.IsAddingSoul)
        {
            return;
        }

        state.IsAddingSoul = true;
        try
        {
            context.Simulator.AddToCombat<Soul>(power.Owner, PileType.Draw, power.Amount, player);
        }
        finally
        {
            state.IsAddingSoul = false;
        }
    }

    private static void HandlePillarOfCreationPower(PillarOfCreationPower power, AfterCardGeneratedForCombatHookContext context)
    {
        if (context.Creator?.Creature == power.Owner)
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleSmokestackPower(SmokestackPower power, AfterCardGeneratedForCombatHookContext context)
    {
        if (context.PreviewCard.Type != CardType.Status ||
            context.Creator?.Creature != power.Owner)
        {
            return;
        }

        context.Simulator.Damage(context.State.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
    }

    private static void HandleTrashToTreasurePower(TrashToTreasurePower power, AfterCardGeneratedForCombatHookContext context)
    {
        if (context.PreviewCard.Type != CardType.Status ||
            context.Creator?.Creature != power.Owner ||
            power.Owner.Player is not { } player)
        {
            return;
        }

        for (var i = 0; i < power.Amount; i++)
        {
            var orb = OrbModel.GetRandomOrb(context.Rng.CombatOrbGeneration).ToMutable();
            context.Simulator.OrbChannel(player, orb);
        }
    }

    private static void HandleRocketPunch(RocketPunch card, AfterCardGeneratedForCombatHookContext context)
    {
        if (context.Creator != card.Owner ||
            context.PreviewCard.Owner != card.Owner ||
            context.PreviewCard.Type != CardType.Status)
        {
            return;
        }

        context.State.FindCard(card)?.MutablePreview.EnergyCost.SetUntilPlayed(0);
    }
}

internal sealed class SoulboundPredictionState
{
    public bool IsAddingSoul { get; set; }
}

internal sealed class AfterCardGeneratedForCombatHookContext : CombatPredictionCardHookContext
{
    public required Player? Creator { get; init; }
}
