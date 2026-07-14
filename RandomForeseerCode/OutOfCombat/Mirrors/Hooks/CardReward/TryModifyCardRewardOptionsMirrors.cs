using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors.Hooks.CardReward;

using Registry = ModelMethodMirrorRegistry<AbstractModel, TryModifyCardRewardOptionsMirrorContext, bool>;

// Mirrors the card reward result-modifier half of the original Hook.TryModifyCardRewardOptions chain:
// first AbstractModel.TryModifyCardRewardOptions, then AbstractModel.TryModifyCardRewardOptionsLate.
// Card reward creation-option hooks and upgrade-odds hooks are pure enough for prediction callers to
// keep using the original Hook.ModifyCardRewardCreationOptions and Hook.ModifyCardRewardUpgradeOdds.
internal static class TryModifyCardRewardOptionsMirrors
{
    private static readonly MirrorMethodSpec TryModifyCardRewardOptions = MirrorMethodSpec.Hook(
        nameof(AbstractModel.TryModifyCardRewardOptions),
        [
            typeof(Player),
            typeof(List<CardCreationResult>),
            typeof(CardCreationOptions)
        ]);

    private static readonly MirrorMethodSpec TryModifyCardRewardOptionsLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.TryModifyCardRewardOptionsLate),
        [
            typeof(Player),
            typeof(List<CardCreationResult>),
            typeof(CardCreationOptions)
        ]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = CreateLateRegistry();

    public static bool Invoke(AbstractModel listener, TryModifyCardRewardOptionsMirrorContext context)
    {
        return Registry.Invoke(listener, context, false).Value;
    }

    public static bool InvokeLate(AbstractModel listener, TryModifyCardRewardOptionsMirrorContext context)
    {
        return LateRegistry.Invoke(listener, context, false).Value;
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(TryModifyCardRewardOptions);

        registry.Register<LastingCandy>(HandleLastingCandy);

        return registry;
    }

    private static Registry CreateLateRegistry()
    {
        var registry = new Registry(TryModifyCardRewardOptionsLate);

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

    private static bool HandleLastingCandy(LastingCandy relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        // StS2 v0.108.0 changed Lasting Candy from counting ended combats to counting combat
        // card rewards seen, and it now requires the IsFromCombat card-creation flag.
        if (relic.Owner != context.Player ||
            context.Options.Source != CardCreationSource.Encounter ||
            !relic.IsInTriggeringCombat ||
            !context.Options.Flags.HasFlag(CardCreationFlags.IsCardReward) ||
            !context.Options.Flags.HasFlag(CardCreationFlags.IsFromCombat))
        {
            return false;
        }


        var possibleCards = context.Options.GetPossibleCards(context.Player).ToList();
        var allowDupes = false;

        bool IsLastingCandyCandidate(CardModel card) =>
            card.Type == CardType.Power &&
            (allowDupes || context.Results.All(result => result.originalCard.Id != card.Id));

        if (!possibleCards.Any(IsLastingCandyCandidate))
        {
            allowDupes = true;

            if (!possibleCards.Any(IsLastingCandyCandidate))
            {
                return false;
            }
        }

        var parentFilter = context.Options.CardPoolFilter;
        var candyOptions = new CardCreationOptions(
                context.Options.CardPools,
                CardCreationSource.Other,
                context.Options.RarityOdds,
                card => (parentFilter is null || parentFilter(card)) && IsLastingCandyCandidate(card))
            .WithFlags(CardCreationFlags.NoModifyHooks | CardCreationFlags.NoCardPoolModifications);
        var card = CardRewardPrediction.CreateBaseRewards(
            context.Player,
            1,
            candyOptions,
            context.Rng.Rewards,
            context.RarityOdds).FirstOrDefault()?.Card;
        if (card is null)
        {
            return false;
        }

        var result = new CardCreationResult(card);
        result.ModifyCard(card, relic);
        context.Results.Add(result);
        return true;
    }

    private static bool HandleFrozenEgg(FrozenEgg relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        return HandleEggRelic(relic, context, CardType.Power);
    }

    private static bool HandleMoltenEgg(MoltenEgg relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        return HandleEggRelic(relic, context, CardType.Attack);
    }

    private static bool HandleToxicEgg(ToxicEgg relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        return HandleEggRelic(relic, context, CardType.Skill);
    }

    private static bool HandleSilverCrucible(
        SilverCrucible relic,
        TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner == context.Player &&
            relic.TimesUsed < relic.DynamicVars.Cards.IntValue &&
            context.Options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            UpgradeAllCards(relic, context.Results);
            return true;
        }

        return false;
    }

    private static bool HandleLavaLamp(LavaLamp relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner == context.Player &&
            context.Player.RunState.CurrentRoom is CombatRoom &&
            !relic.TookDamageThisCombat)
        {
            UpgradeAllCards(relic, context.Results);
            return true;
        }

        return false;
    }

    private static bool HandleGlitter(Glitter relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner == context.Player)
        {
            EnchantAllCards<Glam>(relic, context, 1m);
            return true;
        }

        return false;
    }

    private static bool HandleFresnelLens(FresnelLens relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner == context.Player)
        {
            EnchantAllCards<Nimble>(relic, context, relic.DynamicVars[FresnelLens._nimbleAmountKey].BaseValue);
            return true;
        }

        return false;
    }

    private static bool HandleSilkenTress(SilkenTress relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner == context.Player &&
            !relic.IsUsedUp &&
            context.Options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            EnchantAllCards<Glam>(relic, context, 1m);
            return true;
        }

        return false;
    }

    private static bool HandleWingCharm(WingCharm relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner != context.Player)
        {
            return false;
        }

        var swift = ModelDb.Enchantment<Swift>();
        var validResults = context.Results.Where(result => swift.CanEnchant(result.Card)).ToList();
        var selected = context.SharedRng.Niche.NextItem(validResults);
        if (selected is null)
        {
            return false;
        }

        var modified = EnchantPreview<Swift>(selected.Card, relic.DynamicVars[WingCharm._swiftAmountKey].BaseValue);
        selected.ModifyCard(modified, relic);
        return true;
    }

    private static bool HandleEggRelic(
        RelicModel relic,
        TryModifyCardRewardOptionsMirrorContext context,
        CardType type)
    {
        if (relic.Owner != context.Player || context.Options.Flags.HasFlag(CardCreationFlags.NoHookUpgrades))
        {
            return false;
        }

        foreach (var result in context.Results)
        {
            if (result.Card.Type == type && result.Card.IsUpgradable)
            {
                result.ModifyCard(PredictionUtils.ToUpgradedCard(result.Card), relic);
            }
        }

        return true;
    }

    private static void UpgradeAllCards(RelicModel relic, List<CardCreationResult> results)
    {
        foreach (var result in results)
        {
            if (result.Card.IsUpgradable)
            {
                result.ModifyCard(PredictionUtils.ToUpgradedCard(result.Card), relic);
            }
        }
    }

    private static void EnchantAllCards<T>(
        RelicModel relic,
        TryModifyCardRewardOptionsMirrorContext context,
        decimal amount)
        where T : EnchantmentModel
    {
        var enchantment = ModelDb.Enchantment<T>();
        foreach (var result in context.Results)
        {
            if (enchantment.CanEnchant(result.Card))
            {
                result.ModifyCard(EnchantPreview<T>(result.Card, amount), relic);
            }
        }
    }

    private static CardModel EnchantPreview<T>(CardModel card, decimal amount)
        where T : EnchantmentModel
    {
        var preview = (CardModel)card.MutableClone();
        var enchantment = ModelDb.Enchantment<T>().ToMutable();
        if (preview.Enchantment is null)
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

internal sealed class TryModifyCardRewardOptionsMirrorContext : IPredictionMirrorContext<AbstractModel>
{
    private readonly PredictionSourceStack _sourceStack = new();
    private readonly PredictionRiskTracker _riskTracker = new();

    public required RunPredictionContext RunContext { get; init; }

    public required List<CardCreationResult> Results { get; init; }

    public required CardCreationOptions Options { get; init; }

    public Player Player => RunContext.Player;

    public RunPredictionPlayerRngSet Rng => RunContext.Rng;

    public RunPredictionSharedRngSet SharedRng => RunContext.SharedRng;

    public CardRarityOdds RarityOdds => RunContext.CardRarityOdds;

    public IDisposable PushSource(AbstractModel model)
    {
        return _sourceStack.Push(model);
    }

    public void MarkCurrentSourceRisky()
    {
        _riskTracker.AddCurrentSources(_sourceStack);
    }
}
