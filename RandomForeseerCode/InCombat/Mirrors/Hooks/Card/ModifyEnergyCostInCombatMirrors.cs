using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = MethodMirrorRegistry<AbstractModel, ModifyEnergyCostInCombatMirrorContext, decimal>;

// Mirrors the early and late passes of Hook.ModifyEnergyCostInCombat.
internal static class ModifyEnergyCostInCombatMirrors
{
    private static readonly MirrorMethodSpec ModifyEnergyCostInCombat = MirrorMethodSpec.Hook(
        nameof(AbstractModel.TryModifyEnergyCostInCombat),
        [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()]);

    private static readonly MirrorMethodSpec ModifyEnergyCostInCombatLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.TryModifyEnergyCostInCombatLate),
        [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()]);

    private static readonly Registry Registry = new(ModifyEnergyCostInCombat);
    private static readonly Registry LateRegistry = CreateLateRegistry();

    public static decimal Invoke(AbstractModel listener, ModifyEnergyCostInCombatMirrorContext context)
    {
        if (Registry.TryInvokeRegistered(listener, context, out var result))
        {
            return result.Value;
        }

        listener.TryModifyEnergyCostInCombat(context.Card.Preview, context.Cost, out var modifiedCost);
        return modifiedCost;
    }

    public static decimal InvokeLate(AbstractModel listener, ModifyEnergyCostInCombatMirrorContext context)
    {
        if (LateRegistry.TryInvokeRegistered(listener, context, out var result))
        {
            return result.Value;
        }

        listener.TryModifyEnergyCostInCombatLate(context.Card.Preview, context.Cost, out var modifiedCost);
        return modifiedCost;
    }

    private static Registry CreateLateRegistry()
    {
        var registry = new Registry(ModifyEnergyCostInCombatLate);

        registry.Register<FreeAttackPower>(HandleFreeAttackPower);
        registry.Register<FreePowerPower>(HandleFreePowerPower);
        registry.Register<FreeSkillPower>(HandleFreeSkillPower);
        registry.Register<VeilpiercerPower>(HandleVeilpiercerPower);
        registry.Register<VoidFormPower>(HandleVoidFormPower);

        registry.Register<BrilliantScarf>(HandleBrilliantScarf);

        return registry;
    }

    private static decimal HandleFreeAttackPower(FreeAttackPower power, ModifyEnergyCostInCombatMirrorContext context)
    {
        return HandleFreeCardPower(power, CardType.Attack, context);
    }

    private static decimal HandleFreePowerPower(FreePowerPower power, ModifyEnergyCostInCombatMirrorContext context)
    {
        return HandleFreeCardPower(power, CardType.Power, context);
    }

    private static decimal HandleFreeSkillPower(FreeSkillPower power, ModifyEnergyCostInCombatMirrorContext context)
    {
        return HandleFreeCardPower(power, CardType.Skill, context);
    }

    private static decimal HandleVeilpiercerPower(
        VeilpiercerPower power,
        ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature == power.Owner &&
            context.Card.GetKeywords(context.Simulator).Contains(CardKeyword.Ethereal) &&
            IsInPlayablePile(context) &&
            context.StateStore.GetPowerAmount(power).IsActive
                ? 0
                : context.Cost;
    }

    private static decimal HandleVoidFormPower(
        VoidFormPower power,
        ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature == power.Owner &&
            IsInPlayablePile(context) &&
            context.StateStore.Get(power, () => new VoidFormPredictionState(power)).CardsPlayedThisTurn < power.Amount
                ? 0
                : context.Cost;
    }

    private static decimal HandleBrilliantScarf(
        BrilliantScarf relic,
        ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Card.Preview.Owner == relic.Owner &&
            IsInPlayablePile(context) &&
            context.StateStore.Get(relic, () => new CounterPredictionState(relic._cardsPlayedThisTurn)).Value ==
            relic.DynamicVars.Cards.IntValue - 1
                ? 0
                : context.Cost;
    }

    private static decimal HandleFreeCardPower(
        PowerModel power,
        CardType type,
        ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature == power.Owner &&
            context.Card.Preview.Type == type &&
            IsInPlayablePile(context) &&
            context.StateStore.GetPowerAmount(power).IsActive
                ? 0
                : context.Cost;
    }

    private static bool IsInPlayablePile(ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Card.GetPile(context.State)?.Type is PileType.Hand or PileType.Play;
    }
}

internal sealed class ModifyEnergyCostInCombatMirrorContext : CombatMirrorContext
{
    public required PredictedCard Card { get; init; }

    public required decimal Cost { get; set; }
}
