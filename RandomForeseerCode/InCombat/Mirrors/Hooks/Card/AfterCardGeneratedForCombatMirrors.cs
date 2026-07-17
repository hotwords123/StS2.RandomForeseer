using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterCardGeneratedForCombatMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterCardGeneratedForCombat.
internal static class AfterCardGeneratedForCombatMirrors
{
    private static readonly MirrorMethodSpec AfterCardGeneratedForCombat = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterCardGeneratedForCombat),
        [typeof(CardModel), typeof(Player)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterCardGeneratedForCombatMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterCardGeneratedForCombat);

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

    private static void HandleAeonglass(Aeonglass monster, AfterCardGeneratedForCombatMirrorContext context)
    {
        if (context.MutablePreviewCard is Wither wither)
        {
            for (var i = 0; i < monster.WitherUpgradeCount; i++)
            {
                wither.FakeUpgrade();
            }
        }
    }

    private static void HandleArsenalPower(ArsenalPower power, AfterCardGeneratedForCombatMirrorContext context)
    {
        // Vanilla applies Strength. Power application/removal is outside the simulator's
        // current state domain, so this remains a risk marker.
        if (context.Creator?.Creature == power.Owner)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleRegalite(Regalite relic, AfterCardGeneratedForCombatMirrorContext context)
    {
        if (context.Creator == relic.Owner)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }

    private static void HandleSoulboundPower(SoulboundPower power, AfterCardGeneratedForCombatMirrorContext context)
    {
        if (context.Creator?.Creature != power.Applier ||
            context.PreviewCard is not Soul ||
            power.Owner.Player is not { } player)
        {
            return;
        }

        var state = context.StateStore.Get(power, () => new SoulboundPredictionState(power));
        if (state.IsAddingSoul)
        {
            return;
        }

        state.IsAddingSoul = true;
        try
        {
            context.Simulator.AddToCombat<Soul>(
                power.Owner,
                PileType.Draw,
                power.Amount,
                player,
                CardPilePosition.Random);
        }
        finally
        {
            state.IsAddingSoul = false;
        }
    }

    private static void HandlePillarOfCreationPower(PillarOfCreationPower power, AfterCardGeneratedForCombatMirrorContext context)
    {
        if (context.Creator?.Creature != power.Owner)
        {
            return;
        }

        var state = context.StateStore.Get(power, () => new PillarOfCreationPredictionState(power));
        if (!state.HasTriggeredThisTurn)
        {
            state.HasTriggeredThisTurn = true;
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleSmokestackPower(SmokestackPower power, AfterCardGeneratedForCombatMirrorContext context)
    {
        if (context.PreviewCard.Type == CardType.Status &&
            context.Creator?.Creature == power.Owner)
        {
            context.Simulator.Damage(context.State.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleTrashToTreasurePower(TrashToTreasurePower power, AfterCardGeneratedForCombatMirrorContext context)
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

    private static void HandleRocketPunch(RocketPunch card, AfterCardGeneratedForCombatMirrorContext context)
    {
        if (context.Creator == card.Owner &&
            context.PreviewCard.Owner == card.Owner &&
            context.PreviewCard.Type == CardType.Status)
        {
            context.State.FindCard(card)?.MutablePreview.EnergyCost.SetUntilPlayed(0);
        }
    }
}

internal sealed class AfterCardGeneratedForCombatMirrorContext : CombatPredictionCardMirrorContext
{
    public required Player? Creator { get; init; }
}

internal sealed class SoulboundPredictionState(SoulboundPower power)
{
    public bool IsAddingSoul { get; set; } = power._isAddingSoul;
}

internal sealed class PillarOfCreationPredictionState(PillarOfCreationPower power)
{
    public bool HasTriggeredThisTurn { get; set; } = CombatManager.Instance.History.Entries
        .OfType<CardGeneratedEntry>()
        .Any(entry =>
            entry.Creator?.Creature == power.Owner &&
            entry.HappenedThisTurn(power.CombatState));
}
