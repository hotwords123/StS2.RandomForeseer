using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Blitz;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.OutOfCombat.Mirrors.Hooks.CardCreation;

using Registry = MethodMirrorRegistry<AbstractModel, TryModifyCardRewardOptionsMirrorContext, bool>;

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

        registry.Register<FrozenEgg>((relic, context) => HandleEggRelic(relic, context, CardType.Power));
        registry.Register<MoltenEgg>((relic, context) => HandleEggRelic(relic, context, CardType.Attack));
        registry.Register<ToxicEgg>((relic, context) => HandleEggRelic(relic, context, CardType.Skill));
        registry.Register<SilverCrucible>(HandleSilverCrucible);
        registry.Register<LavaLamp>(HandleLavaLamp);
        registry.Register<Glitter>(HandleGlitter);
        registry.Register<FresnelLens>(HandleFresnelLens);
        registry.Register<SilkenTress>(HandleSilkenTress);
        registry.Register<WingCharm>(HandleWingCharm);
        registry.Register<BlitzRunModel>(HandleBlitzRunModel);

        return registry;
    }

    private static bool HandleBlitzRunModel(BlitzRunModel model, TryModifyCardRewardOptionsMirrorContext context)
    {
        var run = context.Player.RunState;
        if (run.CurrentActIndex == 0 ||
            !context.Options.Flags.HasFlag(CardCreationFlags.IsFromCombat) ||
            run.CurrentRoom?.RoomType == RoomType.Boss)
        {
            return false;
        }

        // BlitzRunModel.TryModifyCardRewardOptionsLate uses RunState.CloneCard, which registers a live card.
        // Select with the cloned Rewards stream and upgrade detached previews instead.
        var selected = run.CurrentActIndex switch
        {
            1 => [context.Rng.Rewards.NextItem(context.Results)!],
            2 => context.Results.UnstableShuffle(context.Rng.Rewards).Take(2),
            _ => []
        };
        foreach (var result in selected)
        {
            if (result.Card.IsUpgradable)
            {
                result.ModifyCard(PredictionUtils.CreateUpgradedCard(result.Card));
            }
        }

        return true;
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

    private static bool HandleSilverCrucible(
        SilverCrucible relic,
        TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner != context.Player ||
            relic.TimesUsed >= relic.DynamicVars.Cards.IntValue ||
            !context.Options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            return false;
        }

        CardCreationResultUtils.UpgradeValidCards(context.Results, relic);
        return true;
    }

    private static bool HandleLavaLamp(LavaLamp relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner != context.Player ||
            context.Player.RunState.CurrentRoom is not CombatRoom ||
            relic.TookDamageThisCombat)
        {
            return false;
        }

        CardCreationResultUtils.UpgradeValidCards(context.Results, relic);
        return true;
    }

    private static bool HandleGlitter(Glitter relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner != context.Player)
        {
            return false;
        }

        CardCreationResultUtils.EnchantValidCards<Glam>(context.Results, relic, 1m);
        return true;
    }

    private static bool HandleFresnelLens(FresnelLens relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner != context.Player)
        {
            return false;
        }

        var amount = relic.DynamicVars[FresnelLens._nimbleAmountKey].BaseValue;
        CardCreationResultUtils.EnchantValidCards<Nimble>(context.Results, relic, amount);
        return true;
    }

    private static bool HandleSilkenTress(SilkenTress relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner != context.Player ||
            relic.IsUsedUp ||
            !context.Options.Flags.HasFlag(CardCreationFlags.IsCardReward))
        {
            return false;
        }

        CardCreationResultUtils.EnchantValidCards<Glam>(context.Results, relic, 1m);
        return true;
    }

    private static bool HandleWingCharm(WingCharm relic, TryModifyCardRewardOptionsMirrorContext context)
    {
        if (relic.Owner != context.Player)
        {
            return false;
        }

        var swift = ModelDb.Enchantment<Swift>();
        var validResults = context.Results.Where(result => swift.CanEnchant(result.Card)).ToList();
        var selected = context.Rng.Rewards.NextItem(validResults);
        if (selected is null)
        {
            return false;
        }

        var amount = relic.DynamicVars[WingCharm._swiftAmountKey].BaseValue;
        selected.ModifyCard(PredictionUtils.CreateEnchantedCard(swift.ToMutable(), selected.Card, amount), relic);
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

        CardCreationResultUtils.UpgradeCardsOfType(context.Results, relic, type);
        return true;
    }
}

internal sealed class TryModifyCardRewardOptionsMirrorContext : CardCreationMirrorContext
{
    public required CardCreationOptions Options { get; init; }

    public RunPredictionPlayerRngSet Rng => RunContext.Rng;

    public RunPredictionSharedRngSet SharedRng => RunContext.SharedRng;

    public CardRarityOdds RarityOdds => RunContext.CardRarityOdds;
}
