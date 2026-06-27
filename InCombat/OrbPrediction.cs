using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

internal static class OrbPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card).ExtraHoverTips;
    }

    public static OrbPredictionResult Predict(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) ||
            card.Owner.Creature.CombatState is not { } combatState)
        {
            return OrbPredictionResult.Empty;
        }

        var simulator = new CombatPredictionSimulator(combatState);
        var extraTips = new List<IHoverTip>();

        switch (card)
        {
            // TODO: Add support for direct Channel/Evoke cards:
            // BallLightning, ColdSnap, IceLance, Ignition, MeteorStrike, Null, Refract, Shatter.

            case Chaos chaos:
                SimulateChaos(chaos, simulator, extraTips);
                break;
            case Chill:
                SimulateRepeatedChannel<FrostOrb>(card, simulator, simulator.State.GetHittableOpponentsOf(card.Owner.Creature).Count);
                break;
            case ConsumingShadow:
                SimulateRepeatedChannel<DarkOrb>(card, simulator, card.DynamicVars.Repeat.IntValue);
                // Vanilla applies ConsumingShadowPower after channeling, which is not simulated here.
                break;
            case Coolheaded:
                simulator.OrbChannel<FrostOrb>(card.Owner);
                simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
                break;
            case Darkness darkness:
                SimulateDarkness(darkness, simulator);
                break;
            case Dualcast:
                SimulateRepeatedEvokeNext(card, simulator, 2);
                break;
            case Fusion:
                simulator.OrbChannel<PlasmaOrb>(card.Owner);
                break;
            case Glacier:
                simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, card);
                SimulateRepeatedChannel<FrostOrb>(card, simulator, 2);
                break;
            case Glasswork:
                simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, card);
                simulator.OrbChannel<GlassOrb>(card.Owner);
                break;
            case MultiCast multiCast:
                SimulateRepeatedEvokeNext(multiCast, simulator, GetXValue(multiCast) + (multiCast.IsUpgraded ? 1 : 0));
                break;
            case Quadcast quadcast:
                SimulateRepeatedEvokeNext(quadcast, simulator, quadcast.DynamicVars.Repeat.IntValue);
                break;
            case Rainbow:
                simulator.OrbChannel<LightningOrb>(card.Owner);
                simulator.OrbChannel<FrostOrb>(card.Owner);
                simulator.OrbChannel<DarkOrb>(card.Owner);
                break;
            case ShadowShield:
                simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, card);
                simulator.OrbChannel<DarkOrb>(card.Owner);
                break;
            case Spinner:
                if (card.IsUpgraded)
                {
                    simulator.OrbChannel<GlassOrb>(card.Owner);
                }
                // Vanilla applies SpinnerPower after channeling, which is not simulated here.
                break;
            case Tempest tempest:
                SimulateRepeatedChannel<LightningOrb>(tempest, simulator, GetXValue(tempest) + (tempest.IsUpgraded ? 1 : 0));
                break;
            case Voltaic voltaic:
                SimulateRepeatedChannel<LightningOrb>(voltaic, simulator, GetVoltaicChannelCount(voltaic));
                break;
            case Zap:
                simulator.OrbChannel<LightningOrb>(card.Owner);
                break;
            default:
                return OrbPredictionResult.Empty;
        }

        var indicatorHoverTips = new List<IHoverTip>();
        var risk = simulator.Snapshot();
        if (risk.HasRisk && RandomForeseerSettings.EnableDriftWarnings)
        {
            indicatorHoverTips.Add(PredictionHoverTips.DriftWarning("orb", risk));
        }

        return new OrbPredictionResult(
            CombatPredictionOverlayContentFactory.FromDamageHistory(simulator, indicatorHoverTips),
            extraTips);
    }

    private static void SimulateChaos(
        Chaos chaos,
        CombatPredictionSimulator simulator,
        List<IHoverTip> extraTips)
    {
        var generatedOrbs = new List<OrbModel>();
        for (var i = 0; i < chaos.DynamicVars.Repeat.IntValue; i++)
        {
            if (simulator.OrbChannelRandom(chaos.Owner) is { } orb)
            {
                generatedOrbs.Add(orb);
            }
        }

        extraTips.AddRange(PredictionHoverTips.Orbs(generatedOrbs));
    }

    private static void SimulateDarkness(
        Darkness darkness,
        CombatPredictionSimulator simulator)
    {
        simulator.OrbChannel<DarkOrb>(darkness.Owner);

        var triggerCount = darkness.IsUpgraded ? 2 : 1;
        var darkOrbs = simulator.State
            .GetPlayerCombatState(darkness.Owner)
            .OrbQueue
            .Orbs
            .OfType<DarkOrb>()
            .ToList();

        foreach (var darkOrb in darkOrbs)
        {
            for (var i = 0; i < triggerCount; i++)
            {
                simulator.OrbPassive(darkOrb);
            }
        }
    }

    private static void SimulateRepeatedEvokeNext(
        CardModel card,
        CombatPredictionSimulator simulator,
        int count)
    {
        if (card.Owner.PlayerCombatState?.OrbQueue.Orbs.Count <= 0 || count <= 0)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            simulator.OrbEvokeNext(card.Owner, dequeue: i == count - 1);
        }
    }

    private static void SimulateRepeatedChannel<T>(
        CardModel card,
        CombatPredictionSimulator simulator,
        int count)
        where T : OrbModel
    {
        for (var i = 0; i < count; i++)
        {
            simulator.OrbChannel<T>(card.Owner);
        }
    }

    private static int GetXValue(CardModel card)
    {
        if (card.Owner.Creature.CombatState is not { } combatState)
        {
            return 0;
        }

        var capturedXValue = card.Owner.PlayerCombatState?.Energy ?? 0;
        return Hook.ModifyXValue(combatState, card, capturedXValue);
    }

    private static int GetVoltaicChannelCount(Voltaic voltaic)
    {
        return (int)((CalculatedVar)voltaic.DynamicVars["CalculatedChannels"]).Calculate(null);
    }
}

internal sealed record OrbPredictionResult(
    CombatPredictionOverlayContent OverlayContent,
    IReadOnlyList<IHoverTip> ExtraHoverTips)
{
    public static OrbPredictionResult Empty { get; } = new(CombatPredictionOverlayContent.Empty, []);
}
