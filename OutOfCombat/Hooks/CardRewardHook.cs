using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;

namespace RandomForeseer.OutOfCombat.Hooks;

// Mirrors the card reward result-modifier half of the original Hook.TryModifyCardRewardOptions chain:
// first AbstractModel.TryModifyCardRewardOptions, then AbstractModel.TryModifyCardRewardOptionsLate.
// Card reward creation-option hooks and upgrade-odds hooks are pure enough for prediction callers to
// keep using the original Hook.ModifyCardRewardCreationOptions and Hook.ModifyCardRewardUpgradeOdds.
internal static class CardRewardHook
{
    private static readonly HookSpec TryModifyCardRewardOptions = new(
        nameof(AbstractModel.TryModifyCardRewardOptions),
        [
            typeof(Player),
            typeof(List<CardCreationResult>),
            typeof(CardCreationOptions)
        ]);

    private static readonly HookSpec TryModifyCardRewardOptionsLate = new(
        nameof(AbstractModel.TryModifyCardRewardOptionsLate),
        [
            typeof(Player),
            typeof(List<CardCreationResult>),
            typeof(CardCreationOptions)
        ]);

    private static readonly HookRegistry<CardRewardHookContext> Early = CreateEarly();

    private static readonly HookRegistry<CardRewardHookContext> Late = CreateLate();

    public static IReadOnlyList<HookResult> RunEarly(CardRewardHookContext context)
    {
        return Early.Run(context.IterateModifiers(), context);
    }

    public static IReadOnlyList<HookResult> RunLate(CardRewardHookContext context)
    {
        return Late.Run(context.IterateModifiers(), context);
    }

    private static HookRegistry<CardRewardHookContext> CreateEarly()
    {
        var registry = new HookRegistry<CardRewardHookContext>(TryModifyCardRewardOptions);

        registry.Register<LastingCandy>(HandleLastingCandy);

        return registry;
    }

    private static HookRegistry<CardRewardHookContext> CreateLate()
    {
        var registry = new HookRegistry<CardRewardHookContext>(TryModifyCardRewardOptionsLate);

        registry.Register<FrozenEgg>(HandleFrozenEgg);
        registry.Register<MoltenEgg>(HandleMoltenEgg);
        registry.Register<ToxicEgg>(HandleToxicEgg);
        registry.Register<SilverCrucible>(HandleSilverCrucible);
        registry.Register<LavaLamp>(HandleLavaLamp);
        registry.Register<Glitter>(HandleGlitter);
        registry.Register<FresnelLens>(HandleFresnelLens);
        registry.Register<SilkenTress>(HandleSilkenTress);
        registry.Register<WingCharm>(HandleWingCharm);

        return registry;
    }

    private static HookResultKind HandleLastingCandy(LastingCandy relic, CardRewardHookContext context)
    {
        if (relic.Owner != context.Player ||
            context.Options.Source != CardCreationSource.Encounter ||
            relic.CombatsSeen <= 0 ||
            relic.CombatsSeen % 2 != 0)
        {
            return HookResultKind.Ignored;
        }

        var candidates = context.Options.GetPossibleCards(context.Player)
            .Where(card => card.Type == CardType.Power && context.Results.TrueForAll(result => result.originalCard.Id != card.Id))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = context.Options.GetPossibleCards(context.Player)
                .Where(card => card.Type == CardType.Power)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return HookResultKind.Ignored;
        }

        var candyOptions = new CardCreationOptions(candidates, CardCreationSource.Other, context.Options.RarityOdds)
            .WithFlags(CardCreationFlags.NoModifyHooks | CardCreationFlags.NoCardPoolModifications);
        var card = CardRewardPrediction.CreateBaseRewards(
            context.Player,
            1,
            candyOptions,
            context.RewardRng,
            context.RarityOdds).FirstOrDefault()?.Card;
        if (card == null)
        {
            return HookResultKind.Ignored;
        }

        var result = new CardCreationResult(card);
        result.ModifyCard(card, relic);
        context.Results.Add(result);
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleFrozenEgg(FrozenEgg relic, CardRewardHookContext context)
    {
        return UpgradeCardsByType(relic, context, CardType.Power);
    }

    private static HookResultKind HandleMoltenEgg(MoltenEgg relic, CardRewardHookContext context)
    {
        return UpgradeCardsByType(relic, context, CardType.Attack);
    }

    private static HookResultKind HandleToxicEgg(ToxicEgg relic, CardRewardHookContext context)
    {
        return UpgradeCardsByType(relic, context, CardType.Skill);
    }

    private static HookResultKind HandleSilverCrucible(SilverCrucible relic, CardRewardHookContext context)
    {
        if (relic.Owner != context.Player ||
            relic.TimesUsed >= relic.DynamicVars.Cards.IntValue ||
            !context.Options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            return HookResultKind.Ignored;
        }

        return UpgradeAllValidCards(relic, context.Results)
            ? HookResultKind.Applied
            : HookResultKind.Ignored;
    }

    private static HookResultKind HandleLavaLamp(LavaLamp relic, CardRewardHookContext context)
    {
        if (relic.Owner != context.Player ||
            context.Player.RunState.CurrentRoom is not MegaCrit.Sts2.Core.Rooms.CombatRoom ||
            relic.TookDamageThisCombat)
        {
            return HookResultKind.Ignored;
        }

        return UpgradeAllValidCards(relic, context.Results)
            ? HookResultKind.Applied
            : HookResultKind.Ignored;
    }

    private static HookResultKind HandleGlitter(Glitter relic, CardRewardHookContext context)
    {
        return EnchantAllValid<Glam>(relic, context, 1m);
    }

    private static HookResultKind HandleFresnelLens(FresnelLens relic, CardRewardHookContext context)
    {
        return EnchantAllValid<Nimble>(relic, context, relic.DynamicVars["NimbleAmount"].BaseValue);
    }

    private static HookResultKind HandleSilkenTress(SilkenTress relic, CardRewardHookContext context)
    {
        if (relic.Owner != context.Player ||
            relic.IsUsedUp ||
            !context.Options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            return HookResultKind.Ignored;
        }

        return EnchantAllValid<Glam>(relic, context, 1m);
    }

    private static HookResultKind HandleWingCharm(WingCharm relic, CardRewardHookContext context)
    {
        if (relic.Owner != context.Player)
        {
            return HookResultKind.Ignored;
        }

        var swift = ModelDb.Enchantment<Swift>();
        var validResults = context.Results.Where(result => swift.CanEnchant(result.Card)).ToList();
        var selected = context.NicheRng.NextItem(validResults);
        if (selected == null)
        {
            return HookResultKind.Ignored;
        }

        selected.ModifyCard(
            EnchantPreview<Swift>(selected.Card, relic.DynamicVars["SwiftAmount"].BaseValue),
            relic);
        return HookResultKind.Applied;
    }

    private static HookResultKind UpgradeCardsByType(RelicModel relic, CardRewardHookContext context, CardType type)
    {
        if (relic.Owner != context.Player || context.Options.Flags.HasFlag(CardCreationFlags.NoHookUpgrades))
        {
            return HookResultKind.Ignored;
        }

        var changed = false;
        foreach (var result in context.Results)
        {
            if (result.Card.Type == type && result.Card.IsUpgradable)
            {
                result.ModifyCard(PredictionUtils.ToUpgradedCard(result.Card), relic);
                changed = true;
            }
        }

        return changed
            ? HookResultKind.Applied
            : HookResultKind.Ignored;
    }

    private static bool UpgradeAllValidCards(RelicModel relic, List<CardCreationResult> results)
    {
        var changed = false;
        foreach (var result in results)
        {
            if (result.Card.IsUpgradable)
            {
                result.ModifyCard(PredictionUtils.ToUpgradedCard(result.Card), relic);
                changed = true;
            }
        }

        return changed;
    }

    private static HookResultKind EnchantAllValid<T>(RelicModel relic, CardRewardHookContext context, decimal amount)
        where T : EnchantmentModel
    {
        if (relic.Owner != context.Player)
        {
            return HookResultKind.Ignored;
        }

        var changed = false;
        var enchantment = ModelDb.Enchantment<T>();
        foreach (var result in context.Results)
        {
            if (enchantment.CanEnchant(result.Card))
            {
                result.ModifyCard(EnchantPreview<T>(result.Card, amount), relic);
                changed = true;
            }
        }

        return changed
            ? HookResultKind.Applied
            : HookResultKind.Ignored;
    }

    private static CardModel EnchantPreview<T>(CardModel card, decimal amount)
        where T : EnchantmentModel
    {
        var preview = (CardModel)card.MutableClone();
        var enchantment = ModelDb.Enchantment<T>().ToMutable();
        if (preview.Enchantment == null)
        {
            preview.EnchantInternal(enchantment, amount);
            enchantment.ModifyCard();
        }
        else if (preview.Enchantment.GetType() == enchantment.GetType())
        {
            preview.Enchantment.Amount += (int)amount;
        }

        preview.FinalizeUpgradeInternal();
        return preview;
    }
}

internal sealed class CardRewardHookContext
{
    public required Player Player { get; init; }

    public required List<CardCreationResult> Results { get; init; }

    public required CardCreationOptions Options { get; init; }

    public required Rng RewardRng { get; init; }

    public required Rng NicheRng { get; init; }

    public required CardRarityOdds RarityOdds { get; init; }

    public IReadOnlyList<AbstractModel> ExtraModifiers { get; init; } = [];

    public IEnumerable<AbstractModel> IterateModifiers()
    {
        return Player.RunState.IterateHookListeners(null).Concat(ExtraModifiers);
    }
}
