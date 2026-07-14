using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class OrbCardMirrors
{
    public static void BallLightningOnPlay(BallLightning card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);
        context.Simulator.OrbChannel<LightningOrb>(card.Owner);
    }

    public static void ChaosOnPlay(Chaos card, CardOnPlayMirrorContext context)
    {
        for (var i = 0; i < card.DynamicVars.Repeat.IntValue; i++)
        {
            var orb = OrbModel.GetRandomOrb(context.Rng.CombatOrbGeneration).ToMutable();
            context.Simulator.OrbChannel(card.Owner, orb);
        }
    }

    public static void ChillOnPlay(Chill card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbChannel<FrostOrb>(card.Owner, context.State.HittableEnemies.Count);
    }

    public static void ColdSnapOnPlay(ColdSnap card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);
        context.Simulator.OrbChannel<FrostOrb>(card.Owner);
    }

    public static void ConsumingShadowOnPlay(ConsumingShadow card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbChannel<DarkOrb>(card.Owner, card.DynamicVars.Repeat.IntValue);
        // Vanilla applies ConsumingShadowPower after channeling, which is not simulated here.
    }

    public static void CoolheadedOnPlay(Coolheaded card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbChannel<FrostOrb>(card.Owner);
        context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
    }

    public static void DarknessOnPlay(Darkness card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbChannel<DarkOrb>(card.Owner);

        var triggerCount = card.IsUpgraded ? 2 : 1;
        var darkOrbs = context.OwnerState.OrbQueue.Orbs.OfType<DarkOrb>().ToArray();
        foreach (var darkOrb in darkOrbs)
        {
            for (var i = 0; i < triggerCount; i++)
            {
                context.Simulator.OrbPassive(darkOrb);
            }
        }
    }

    public static void DualcastOnPlay(Dualcast card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbEvokeNext(card.Owner, repeat: 2);
    }

    public static void FusionOnPlay(Fusion card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbChannel<PlasmaOrb>(card.Owner);
    }

    public static void GlacierOnPlay(Glacier card, CardOnPlayMirrorContext context)
    {
        context.Simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, context.Card);
        context.Simulator.OrbChannel<FrostOrb>(card.Owner, 2);
    }

    public static void GlassworkOnPlay(Glasswork card, CardOnPlayMirrorContext context)
    {
        context.Simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, context.Card);
        context.Simulator.OrbChannel<GlassOrb>(card.Owner);
    }

    public static void IceLanceOnPlay(IceLance card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);
        context.Simulator.OrbChannel<FrostOrb>(card.Owner, card.DynamicVars.Repeat.IntValue);
    }

    public static void IgnitionOnPlay(Ignition card, CardOnPlayMirrorContext context)
    {
        if (context.CardPlay.Target?.Player is { } player)
        {
            context.Simulator.OrbChannel<PlasmaOrb>(player);
        }
    }

    public static void MeteorStrikeOnPlay(MeteorStrike card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);
        context.Simulator.OrbChannel<PlasmaOrb>(card.Owner, 3);
    }

    public static void MultiCastOnPlay(MultiCast card, CardOnPlayMirrorContext context)
    {
        var repeat = context.Card.ResolveEnergyXValue(context.State) + (card.IsUpgraded ? 1 : 0);
        context.Simulator.OrbEvokeNext(card.Owner, repeat);
    }

    public static void NullOnPlay(Null card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);
        // Vanilla applies Weak before channeling; Weak does not affect this prediction's orb damage.
        context.Simulator.OrbChannel<DarkOrb>(card.Owner);
    }

    public static void QuadcastOnPlay(Quadcast card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbEvokeNext(card.Owner, repeat: card.DynamicVars.Repeat.IntValue);
    }

    public static void RainbowOnPlay(Rainbow card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbChannel<LightningOrb>(card.Owner);
        context.Simulator.OrbChannel<FrostOrb>(card.Owner);
        context.Simulator.OrbChannel<DarkOrb>(card.Owner);
    }

    public static void RefractOnPlay(Refract card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context, hitCount: 2);
        context.Simulator.OrbChannel<GlassOrb>(card.Owner, card.DynamicVars.Repeat.IntValue);
    }

    public static void ShadowShieldOnPlay(ShadowShield card, CardOnPlayMirrorContext context)
    {
        context.Simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, context.Card);
        context.Simulator.OrbChannel<DarkOrb>(card.Owner);
    }

    public static void ShatterOnPlay(Shatter card, CardOnPlayMirrorContext context)
    {
        DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
            .FromCard(card, context.CardPlay)
            .TargetingAllOpponents(context.CombatState)
            .Simulate(context.Simulator);

        var orbCount = context.OwnerState.OrbQueue.Orbs.Count;
        for (var i = 0; i < orbCount; i++)
        {
            context.Simulator.OrbEvokeNext(card.Owner, repeat: 2);
        }
    }

    public static void SpinnerOnPlay(Spinner card, CardOnPlayMirrorContext context)
    {
        if (card.IsUpgraded)
        {
            context.Simulator.OrbChannel<GlassOrb>(card.Owner);
        }

        // Vanilla applies SpinnerPower after optional channeling, which is not simulated here.
    }

    public static void TempestOnPlay(Tempest card, CardOnPlayMirrorContext context)
    {
        var count = context.Card.ResolveEnergyXValue(context.State) + (card.IsUpgraded ? 1 : 0);
        context.Simulator.OrbChannel<LightningOrb>(card.Owner, count);
    }

    public static void TeslaCoilOnPlay(TeslaCoil card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);

        var triggerCount = card.IsUpgraded ? 2 : 1;
        var lightningOrbs = context.OwnerState.OrbQueue.Orbs.OfType<LightningOrb>().ToArray();
        foreach (var lightningOrb in lightningOrbs)
        {
            for (var i = 0; i < triggerCount; i++)
            {
                context.Simulator.OrbPassive(lightningOrb, context.CardPlay.Target);
            }
        }
    }

    public static void VoltaicOnPlay(Voltaic card, CardOnPlayMirrorContext context)
    {
        var count = CombatManager.Instance.History.Entries
            .OfType<OrbChanneledEntry>()
            .Count(entry => entry.Actor.Player == card.Owner && entry.Orb is LightningOrb);

        count += context.Simulator.History
            .OfType<CombatPredictionOrbChanneledEntry>()
            .Count(entry => entry.Orb.Owner == card.Owner && entry.Orb is LightningOrb);

        context.Simulator.OrbChannel<LightningOrb>(card.Owner, count);
    }

    public static void ZapOnPlay(Zap card, CardOnPlayMirrorContext context)
    {
        context.Simulator.OrbChannel<LightningOrb>(card.Owner);
    }

    private static void SimulateTargetedAttack(
        CardOnPlayMirrorContext context,
        int hitCount = 1)
    {
        context.Simulator.SimulateTargetedAttack(context.Card, context.CardPlay, hitCount);
    }
}
