using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class CardOnPlayMirrorContextExtensions
{
    // Convenience extension method to simulate a single-targeted attack command.
    // Callers should ensure that the card play has a target before calling this method.
    public static void AttackSingle(this CardOnPlayMirrorContext context, int hitCount = 1)
    {
        DamageCmd.Attack(context.PreviewCard.DynamicVars.Damage.BaseValue)
            .FromCard(context.PreviewCard, context.CardPlay)
            .WithHitCount(hitCount)
            .Targeting(context.Target)
            .Simulate(context.Simulator);
    }

    // Convenience extension method to simulate an attack command targeting all opponents.
    public static void AttackAllOpponents(this CardOnPlayMirrorContext context, int hitCount = 1)
    {
        DamageCmd.Attack(context.PreviewCard.DynamicVars.Damage.BaseValue)
            .FromCard(context.PreviewCard, context.CardPlay)
            .WithHitCount(hitCount)
            .TargetingAllOpponents(context.CombatState)
            .Simulate(context.Simulator);
    }

    // Convenience extension method to simulate a random-targeted attack command.
    public static void AttackRandomOpponents(this CardOnPlayMirrorContext context, int hitCount = 1)
    {
        DamageCmd.Attack(context.PreviewCard.DynamicVars.Damage.BaseValue)
            .FromCard(context.PreviewCard, context.CardPlay)
            .WithHitCount(hitCount)
            .TargetingRandomOpponents(context.CombatState)
            .Simulate(context.Simulator);
    }

    /// <summary>
    /// See <see cref="CombatPredictionSimulator.GainBlock(Creature, BlockVar, PredictedCard?, CardPlay?)"/>.
    /// </summary>
    public static decimal GainBlock(this CardOnPlayMirrorContext context, Creature target)
    {
        return context.Simulator.GainBlock(
            target,
            context.PreviewCard.DynamicVars.Block,
            context.Card,
            context.CardPlay);
    }

    /// <summary>
    /// See <see cref="CombatPredictionSimulator.GainBlock(Creature, decimal, ValueProp, PredictedCard?, CardPlay?)"/>.
    /// </summary>
    public static decimal GainBlock(this CardOnPlayMirrorContext context, Creature target, decimal amount, ValueProp props)
    {
        return context.Simulator.GainBlock(
            target,
            amount,
            props,
            context.Card,
            context.CardPlay);
    }

    /// <summary>
    /// See <see cref="CombatPredictionDynamicVarExtensions.InvokeCalculate(DynamicVar, CombatPredictionSimulator, PredictedCard, Creature?)"/>.
    /// </summary>
    public static decimal Calculate(this CardOnPlayMirrorContext context, DynamicVar dynamicVar)
    {
        // We use context.CardPlay.Target here instead of context.Target because the card play may not have a target,
        // and context.Target will throw in that case.
        return dynamicVar.InvokeCalculate(context.Simulator, context.Card, context.CardPlay.Target);
    }

    /// <summary>
    /// Mirrors <see cref="CardModel.ResolveEnergyXValue"/>.
    /// </summary>
    public static int ResolveEnergyXValue(this CardOnPlayMirrorContext context)
    {
        return HookMirrors.ModifyXValue(
            context.Simulator,
            context.PreviewCard,
            context.PreviewCard.EnergyCost.CapturedXValue);
    }

    /// <summary>
    /// Mirrors <see cref="CardModel.ResolveStarXValue"/>.
    /// </summary>
    public static int ResolveStarXValue(this CardOnPlayMirrorContext context)
    {
        return HookMirrors.ModifyXValue(
            context.Simulator,
            context.PreviewCard,
            context.PreviewCard.LastStarsSpent);
    }
}
