using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class OrbPrediction(
    CombatPredictionSimulator simulator,
    SimPlayerCombatState playerCombatState,
    PredictedCard source,
    CardPlay cardPlay,
    List<IHoverTip> extraTips)
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card, target: null)?.ToHoverTips() ?? [];
    }

    public static OrbPredictionResult? Predict(CardModel card, Creature? target)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) ||
            !IsSupported(card) ||
            !card.TryResolveTarget(ref target) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return null;
        }

        var playerCombatState = simulator.State.GetPlayerCombatState(card.Owner);
        var predictedCard = playerCombatState.FindCard(card) ?? new PredictedCard(card);

        var extraTips = new List<IHoverTip>();

        simulator.ManualPlay(predictedCard, target, (_, cardPlay) =>
        {
            new OrbPrediction(simulator, playerCombatState, predictedCard, cardPlay, extraTips)
                .Simulate();
        });

        return new(DamagePredictionResult.FromDamageHistory(simulator), extraTips);
    }

    private static bool IsSupported(CardModel card)
    {
        return card is
            BallLightning or
            Chaos or
            Chill or
            ColdSnap or
            ConsumingShadow or
            Coolheaded or
            Darkness or
            Dualcast or
            Fusion or
            Glacier or
            Glasswork or
            IceLance or
            Ignition or
            MeteorStrike or
            MultiCast or
            Null or
            Quadcast or
            Rainbow or
            Refract or
            ShadowShield or
            Shatter or
            Spinner { IsUpgraded: true } or
            Tempest or
            TeslaCoil or
            Voltaic or
            Zap;
    }

    private void Simulate()
    {
        switch (source.Preview)
        {
            case BallLightning:
                SimulateBallLightning();
                break;
            case Chaos:
                SimulateChaos();
                break;
            case Chill:
                SimulateChill();
                break;
            case ColdSnap:
                SimulateColdSnap();
                break;
            case ConsumingShadow:
                SimulateConsumingShadow();
                break;
            case Coolheaded:
                SimulateCoolheaded();
                break;
            case Darkness:
                SimulateDarkness();
                break;
            case Dualcast:
                SimulateDualcast();
                break;
            case Fusion:
                SimulateFusion();
                break;
            case Glacier:
                SimulateGlacier();
                break;
            case Glasswork:
                SimulateGlasswork();
                break;
            case IceLance:
                SimulateIceLance();
                break;
            case Ignition:
                SimulateIgnition();
                break;
            case MeteorStrike:
                SimulateMeteorStrike();
                break;
            case MultiCast:
                SimulateMultiCast();
                break;
            case Null:
                SimulateNull();
                break;
            case Quadcast:
                SimulateQuadcast();
                break;
            case Rainbow:
                SimulateRainbow();
                break;
            case Refract:
                SimulateRefract();
                break;
            case ShadowShield:
                SimulateShadowShield();
                break;
            case Shatter:
                SimulateShatter();
                break;
            case Spinner { IsUpgraded: true }:
                SimulateSpinner();
                break;
            case Tempest:
                SimulateTempest();
                break;
            case TeslaCoil:
                SimulateTeslaCoil();
                break;
            case Voltaic:
                SimulateVoltaic();
                break;
            case Zap:
                SimulateZap();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported card type for orb prediction: {source.Preview.Id}");
        }
    }

    private void SimulateTargetedAttack(int hitCount = 1)
    {
        simulator.SimulateTargetedAttack(source, cardPlay, hitCount);
    }

    private void SimulateBallLightning()
    {
        SimulateTargetedAttack();
        simulator.OrbChannel<LightningOrb>(source.Preview.Owner);
    }

    private void SimulateChaos()
    {
        var generatedOrbs = new List<OrbModel>();
        for (var i = 0; i < source.Preview.DynamicVars.Repeat.IntValue; i++)
        {
            var orb = OrbModel.GetRandomOrb(simulator.Rng.CombatOrbGeneration).ToMutable();
            if (simulator.OrbChannel(source.Preview.Owner, orb))
            {
                generatedOrbs.Add(orb);
            }
        }

        extraTips.AddRange(PredictionHoverTips.Orbs(generatedOrbs));
    }

    private void SimulateChill()
    {
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner, simulator.State.HittableEnemies.Count);
    }

    private void SimulateColdSnap()
    {
        SimulateTargetedAttack();
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner);
    }

    private void SimulateConsumingShadow()
    {
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner, source.Preview.DynamicVars.Repeat.IntValue);
        // Vanilla applies ConsumingShadowPower after channeling, which is not simulated here.
    }

    private void SimulateCoolheaded()
    {
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner);
        simulator.Draw(source.Preview.Owner, source.Preview.DynamicVars.Cards.IntValue);
    }

    private void SimulateDarkness()
    {
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner);

        var triggerCount = source.Preview.IsUpgraded ? 2 : 1;
        var darkOrbs = playerCombatState.OrbQueue.Orbs.OfType<DarkOrb>().ToArray();

        foreach (var darkOrb in darkOrbs)
        {
            for (var i = 0; i < triggerCount; i++)
            {
                simulator.OrbPassive(darkOrb);
            }
        }
    }

    private void SimulateDualcast()
    {
        simulator.OrbEvokeNext(source.Preview.Owner, repeat: 2);
    }

    private void SimulateFusion()
    {
        simulator.OrbChannel<PlasmaOrb>(source.Preview.Owner);
    }

    private void SimulateGlacier()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner, 2);
    }

    private void SimulateGlasswork()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);
        simulator.OrbChannel<GlassOrb>(source.Preview.Owner);
    }

    private void SimulateIceLance()
    {
        SimulateTargetedAttack();
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner, source.Preview.DynamicVars.Repeat.IntValue);
    }

    private void SimulateIgnition()
    {
        if (cardPlay.Target?.Player is { } player)
        {
            simulator.OrbChannel<PlasmaOrb>(player);
        }
    }

    private void SimulateMeteorStrike()
    {
        SimulateTargetedAttack();
        simulator.OrbChannel<PlasmaOrb>(source.Preview.Owner, 3);
    }

    private void SimulateMultiCast()
    {
        var repeat = source.ResolveEnergyXValue(simulator.State) + (source.Preview.IsUpgraded ? 1 : 0);
        simulator.OrbEvokeNext(source.Preview.Owner, repeat);
    }

    private void SimulateNull()
    {
        SimulateTargetedAttack();
        // Vanilla applies Weak before channeling; Weak does not affect this prediction's orb damage.
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner);
    }

    private void SimulateQuadcast()
    {
        simulator.OrbEvokeNext(source.Preview.Owner, repeat: source.Preview.DynamicVars.Repeat.IntValue);
    }

    private void SimulateRainbow()
    {
        simulator.OrbChannel<LightningOrb>(source.Preview.Owner);
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner);
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner);
    }

    private void SimulateRefract()
    {
        SimulateTargetedAttack(hitCount: 2);
        simulator.OrbChannel<GlassOrb>(source.Preview.Owner, source.Preview.DynamicVars.Repeat.IntValue);
    }

    private void SimulateShadowShield()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner);
    }

    private void SimulateShatter()
    {
        DamageCmd.Attack(source.Preview.DynamicVars.Damage.BaseValue)
            .FromCard(source.Preview, cardPlay)
            .TargetingAllOpponents(simulator.State.CombatState)
            .Simulate(simulator);

        var orbCount = playerCombatState.OrbQueue.Orbs.Count;
        for (var i = 0; i < orbCount; i++)
        {
            simulator.OrbEvokeNext(source.Preview.Owner, repeat: 2);
        }
    }

    private void SimulateSpinner()
    {
        simulator.OrbChannel<GlassOrb>(source.Preview.Owner);
        // Vanilla applies SpinnerPower after channeling, which is not simulated here.
    }

    private void SimulateTempest()
    {
        var count = source.ResolveEnergyXValue(simulator.State) + (source.Preview.IsUpgraded ? 1 : 0);
        simulator.OrbChannel<LightningOrb>(source.Preview.Owner, count);
    }

    private void SimulateTeslaCoil()
    {
        SimulateTargetedAttack();

        var triggerCount = source.Preview.IsUpgraded ? 2 : 1;
        var lightningOrbs = playerCombatState.OrbQueue.Orbs.OfType<LightningOrb>().ToArray();

        foreach (var lightningOrb in lightningOrbs)
        {
            for (var i = 0; i < triggerCount; i++)
            {
                simulator.OrbPassive(lightningOrb, cardPlay.Target);
            }
        }
    }

    private void SimulateVoltaic()
    {
        var count = CombatManager.Instance.History.Entries
            .OfType<OrbChanneledEntry>()
            .Count(entry => entry.Actor.Player == source.Preview.Owner && entry.Orb is LightningOrb);

        count += simulator.History
            .OfType<CombatPredictionOrbChanneledEntry>()
            .Count(entry => entry.Orb.Owner == source.Preview.Owner && entry.Orb is LightningOrb);

        simulator.OrbChannel<LightningOrb>(source.Preview.Owner, count);
    }

    private void SimulateZap()
    {
        simulator.OrbChannel<LightningOrb>(source.Preview.Owner);
    }
}

internal sealed record OrbPredictionResult(
    DamagePredictionResult DamagePrediction,
    IReadOnlyList<IHoverTip> ExtraHoverTips)
{
    public bool IsEmpty => !DamagePrediction.HasTargets && ExtraHoverTips.Count == 0;

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        if (IsEmpty)
        {
            return [];
        }

        var hoverTips = ExtraHoverTips.ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(hoverTips, "orb", DamagePrediction.Risk);
        return hoverTips;
    }
}
