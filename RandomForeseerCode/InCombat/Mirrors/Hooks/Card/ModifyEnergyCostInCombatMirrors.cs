using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Mirrors.Shared;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Card;

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

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = CreateLateRegistry();

    public static decimal Invoke(AbstractModel listener, ModifyEnergyCostInCombatMirrorContext context)
    {
        return Registry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginal(listener, context);
    }

    public static decimal InvokeLate(AbstractModel listener, ModifyEnergyCostInCombatMirrorContext context)
    {
        return LateRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalLate(listener, context);
    }

    private static decimal InvokeOriginal(AbstractModel listener, ModifyEnergyCostInCombatMirrorContext context)
    {
        listener.TryModifyEnergyCostInCombat(context.Card.Preview, context.Cost, out var modifiedCost);
        return modifiedCost;
    }

    private static decimal InvokeOriginalLate(AbstractModel listener, ModifyEnergyCostInCombatMirrorContext context)
    {
        listener.TryModifyEnergyCostInCombatLate(context.Card.Preview, context.Cost, out var modifiedCost);
        return modifiedCost;
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(ModifyEnergyCostInCombat);
        registry.Register<ChoLimbs>(HandleChoLimbs);
        registry.Register<Coda>(HandleCoda);
        return registry;
    }

    private static Registry CreateLateRegistry()
    {
        var registry = new Registry(ModifyEnergyCostInCombatLate);

        registry.Register<FreeAttackPower>(HandleFreeAttackPower);
        registry.Register<FreeCardPower>(HandleFreeCardPower);
        registry.Register<FreePowerPower>(HandleFreePowerPower);
        registry.Register<FreeSkillPower>(HandleFreeSkillPower);
        registry.Register<EnsemblePower>(HandleEnsemblePower);
        registry.Register<VeilpiercerPower>(HandleVeilpiercerPower);
        registry.Register<VoidFormPower>(HandleVoidFormPower);

        registry.Register<BrilliantScarf>(HandleBrilliantScarf);

        return registry;
    }

    private static decimal HandleChoLimbs(ChoLimbs card, ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Cost > 0 && context.Card.References(card) &&
               (MelodyMirrors.PlayedThisTurn(context.Simulator, card.Owner, CardTag.Wah) ||
                MelodyMirrors.PlayedThisTurn(context.Simulator, card.Owner, CardTag.Ki))
            ? Math.Max(0, context.Cost - 2)
            : context.Cost;
    }

    private static decimal HandleCoda(Coda card, ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Cost > 0 && context.Card.References(card)
            ? Math.Max(0, context.Cost - 2 * MelodyMirrors.MelodiesThisTurn(context.Simulator, card.Owner))
            : context.Cost;
    }

    private static decimal HandleEnsemblePower(EnsemblePower power, ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature != power.Owner &&
               IsInPlayablePile(context) &&
               context.StateStore.GetPowerAmount(power).IsActive
            ? 0
            : context.Cost;
    }

    private static decimal HandleFreeAttackPower(FreeAttackPower power, ModifyEnergyCostInCombatMirrorContext context)
    {
        return HandleFreeCardPower(power, CardType.Attack, context);
    }

    private static decimal HandleFreePowerPower(FreePowerPower power, ModifyEnergyCostInCombatMirrorContext context)
    {
        return HandleFreeCardPower(power, CardType.Power, context);
    }

    private static decimal HandleFreeCardPower(FreeCardPower power, ModifyEnergyCostInCombatMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature == power.Owner &&
               IsInPlayablePile(context) &&
               context.StateStore.GetPowerAmount(power).IsActive
            ? 0
            : context.Cost;
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
               context.StateStore.Get(power, () => new VoidFormPredictionState(power)).CardsPlayedThisTurn <
               power.Amount
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
