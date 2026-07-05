using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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
    Creature? target,
    List<IHoverTip> extraTips)
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card, target: null).ToHoverTips();
    }

    public static OrbPredictionResult Predict(CardModel card, Creature? target)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) ||
            card.Owner.Creature.CombatState is not { } combatState)
        {
            return OrbPredictionResult.Empty;
        }

        var simulator = new CombatPredictionSimulator(combatState);

        var playerCombatState = simulator.State.GetPlayerCombatState(card.Owner);
        var predictedCard = playerCombatState.FindCard(card) ?? new PredictedCard(card);

        var extraTips = new List<IHoverTip>();
        var predictor = new OrbPrediction(simulator, playerCombatState, predictedCard, target, extraTips);

        using (simulator.PushSource(card))
        {
            if (!predictor.Simulate())
            {
                return OrbPredictionResult.Empty;
            }
        }

        return new(DamagePredictionResult.FromDamageHistory(simulator), extraTips);
    }

    private bool Simulate()
    {
        return source.Preview switch
        {
            BallLightning => SimulateBallLightning(),
            Chaos => SimulateChaos(),
            Chill => SimulateChill(),
            ColdSnap => SimulateColdSnap(),
            ConsumingShadow => SimulateConsumingShadow(),
            Coolheaded => SimulateCoolheaded(),
            Darkness => SimulateDarkness(),
            Dualcast => SimulateDualcast(),
            Fusion => SimulateFusion(),
            Glacier => SimulateGlacier(),
            Glasswork => SimulateGlasswork(),
            IceLance => SimulateIceLance(),
            Ignition => SimulateIgnition(),
            MeteorStrike => SimulateMeteorStrike(),
            MultiCast => SimulateMultiCast(),
            Null => SimulateNull(),
            Quadcast => SimulateQuadcast(),
            Rainbow => SimulateRainbow(),
            Refract => SimulateRefract(),
            ShadowShield => SimulateShadowShield(),
            Shatter => SimulateShatter(),
            Spinner { IsUpgraded: true } => SimulateSpinner(),
            Tempest => SimulateTempest(),
            Voltaic => SimulateVoltaic(),
            Zap => SimulateZap(),
            _ => false
        };
    }

    private int GetXValue()
    {
        // TODO: Track player energy in SimPlayerCombatState
        var capturedXValue = source.Preview.Owner.PlayerCombatState?.Energy ?? 0;
        return Hook.ModifyXValue(simulator.State.CombatState, source.Preview, capturedXValue);
    }

    private bool TrySimulateTargetedAttack(int hitCount = 1)
    {
        return simulator.TrySimulateTargetedAttack(source, target, hitCount);
    }

    private bool SimulateBallLightning()
    {
        if (!TrySimulateTargetedAttack())
        {
            return false;
        }

        simulator.OrbChannel<LightningOrb>(source.Preview.Owner);
        return true;
    }

    private bool SimulateChaos()
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
        return true;
    }

    private bool SimulateChill()
    {
        simulator.OrbChannel<FrostOrb>(
            source.Preview.Owner,
            simulator.State.GetHittableOpponentsOf(source.Preview.Owner.Creature).Count);
        return true;
    }

    private bool SimulateColdSnap()
    {
        if (!TrySimulateTargetedAttack())
        {
            return false;
        }

        simulator.OrbChannel<FrostOrb>(source.Preview.Owner);
        return true;
    }

    private bool SimulateConsumingShadow()
    {
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner, source.Preview.DynamicVars.Repeat.IntValue);
        // Vanilla applies ConsumingShadowPower after channeling, which is not simulated here.
        return true;
    }

    private bool SimulateCoolheaded()
    {
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner);
        simulator.Draw(source.Preview.Owner, source.Preview.DynamicVars.Cards.IntValue);
        return true;
    }

    private bool SimulateDarkness()
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

        return true;
    }

    private bool SimulateDualcast()
    {
        simulator.OrbEvokeNext(source.Preview.Owner, repeat: 2);
        return true;
    }

    private bool SimulateFusion()
    {
        simulator.OrbChannel<PlasmaOrb>(source.Preview.Owner);
        return true;
    }

    private bool SimulateGlacier()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner, 2);
        return true;
    }

    private bool SimulateGlasswork()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);
        simulator.OrbChannel<GlassOrb>(source.Preview.Owner);
        return true;
    }

    private bool SimulateIceLance()
    {
        if (!TrySimulateTargetedAttack())
        {
            return false;
        }

        simulator.OrbChannel<FrostOrb>(source.Preview.Owner, source.Preview.DynamicVars.Repeat.IntValue);
        return true;
    }

    private bool SimulateIgnition()
    {
        if (target?.Player is not { } ignitionTarget || !source.Preview.CanPlayTargeting(target))
        {
            return false;
        }

        simulator.OrbChannel<PlasmaOrb>(ignitionTarget);
        return true;
    }

    private bool SimulateMeteorStrike()
    {
        if (!TrySimulateTargetedAttack())
        {
            return false;
        }

        simulator.OrbChannel<PlasmaOrb>(source.Preview.Owner, 3);
        return true;
    }

    private bool SimulateMultiCast()
    {
        simulator.OrbEvokeNext(source.Preview.Owner, repeat: GetXValue() + (source.Preview.IsUpgraded ? 1 : 0));
        return true;
    }

    private bool SimulateNull()
    {
        if (!TrySimulateTargetedAttack())
        {
            return false;
        }

        // Vanilla applies Weak before channeling; Weak does not affect this prediction's orb damage.
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner);
        return true;
    }

    private bool SimulateQuadcast()
    {
        simulator.OrbEvokeNext(source.Preview.Owner, repeat: source.Preview.DynamicVars.Repeat.IntValue);
        return true;
    }

    private bool SimulateRainbow()
    {
        simulator.OrbChannel<LightningOrb>(source.Preview.Owner);
        simulator.OrbChannel<FrostOrb>(source.Preview.Owner);
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner);
        return true;
    }

    private bool SimulateRefract()
    {
        if (!TrySimulateTargetedAttack(hitCount: 2))
        {
            return false;
        }

        simulator.OrbChannel<GlassOrb>(source.Preview.Owner, source.Preview.DynamicVars.Repeat.IntValue);
        return true;
    }

    private bool SimulateShadowShield()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);
        simulator.OrbChannel<DarkOrb>(source.Preview.Owner);
        return true;
    }

    private bool SimulateShatter()
    {
        DamageCmd.Attack(source.Preview.DynamicVars.Damage.BaseValue)
            .FromCard(source.Preview, null)
            .TargetingAllOpponents(simulator.State.CombatState)
            .Simulate(simulator);

        var orbCount = playerCombatState.OrbQueue.Orbs.Count;
        for (var i = 0; i < orbCount; i++)
        {
            simulator.OrbEvokeNext(source.Preview.Owner, repeat: 2);
        }

        return true;
    }

    private bool SimulateSpinner()
    {
        simulator.OrbChannel<GlassOrb>(source.Preview.Owner);
        // Vanilla applies SpinnerPower after channeling, which is not simulated here.
        return true;
    }

    private bool SimulateTempest()
    {
        simulator.OrbChannel<LightningOrb>(
            source.Preview.Owner,
            GetXValue() + (source.Preview.IsUpgraded ? 1 : 0));
        return true;
    }

    private bool SimulateVoltaic()
    {
        if (source.Preview.DynamicVars["CalculatedChannels"] is CalculatedVar calculatedVar)
        {
            simulator.OrbChannel<LightningOrb>(source.Preview.Owner, (int)calculatedVar.Calculate(null));
        }
        return true;
    }

    private bool SimulateZap()
    {
        simulator.OrbChannel<LightningOrb>(source.Preview.Owner);
        return true;
    }
}

internal sealed record OrbPredictionResult(
    DamagePredictionResult DamagePrediction,
    IReadOnlyList<IHoverTip> ExtraHoverTips)
{
    public static OrbPredictionResult Empty { get; } = new(DamagePredictionResult.Empty, []);

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
