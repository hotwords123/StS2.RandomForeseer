using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors.Hooks.CardCreation;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors;

// Prediction-facing facade for mirrored out-of-combat hooks. It owns context construction,
// modifier enumeration, and hook phase order while registries remain implementation details.
internal static class HookMirrors
{
    /// <summary>
    /// Mirrors <see cref="Hook.ModifyCardRewardCreationOptions"/>.
    /// </summary>
    public static CardCreationOptions ModifyCardRewardCreationOptions(
        IRunState runState,
        Player player,
        CardCreationOptions options)
    {
        foreach (var listener in IterateRunHookListeners(runState))
        {
            options = listener.ModifyCardRewardCreationOptions(player, options);
        }

        foreach (var listener in IterateRunHookListeners(runState))
        {
            options = listener.ModifyCardRewardCreationOptionsLate(player, options);
        }

        return options;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyCardRewardUpgradeOdds"/>.
    /// </summary>
    public static decimal ModifyCardRewardUpgradeOdds(
        IRunState runState,
        Player player,
        CardModel card,
        decimal odds)
    {
        foreach (var listener in IterateRunHookListeners(runState))
        {
            odds = listener.ModifyCardRewardUpgradeOdds(player, card, odds);
        }

        return odds;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyMerchantCardPool"/>.
    /// </summary>
    public static IEnumerable<CardModel> ModifyMerchantCardPool(
        IRunState runState,
        Player player,
        IEnumerable<CardModel> options)
    {
        foreach (var listener in IterateRunHookListeners(runState))
        {
            options = listener.ModifyMerchantCardPool(player, options);
        }

        return options;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyMerchantCardRarity"/>.
    /// </summary>
    public static CardRarity ModifyMerchantCardRarity(
        IRunState runState,
        Player player,
        CardRarity rarity)
    {
        foreach (var listener in IterateRunHookListeners(runState))
        {
            rarity = listener.ModifyMerchantCardRarity(player, rarity);
        }

        return rarity;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyMerchantPrice"/>.
    /// </summary>
    public static decimal ModifyMerchantPrice(
        IRunState runState,
        Player player,
        MerchantEntry entry,
        decimal price)
    {
        foreach (var listener in IterateRunHookListeners(runState))
        {
            price = listener.ModifyMerchantPrice(player, entry, price);
        }

        return price;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldRefillMerchantEntry"/>.
    /// </summary>
    public static bool ShouldRefillMerchantEntry(IRunState runState, MerchantEntry entry, Player player)
    {
        foreach (var listener in IterateRunHookListeners(runState))
        {
            if (listener.ShouldRefillMerchantEntry(entry, player))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyMerchantCardCreationResults"/>.
    /// </summary>
    public static void ModifyMerchantCardCreationResults(
        RunPredictionContext runContext,
        List<CardCreationResult> results)
    {
        var context = new ModifyMerchantCardCreationResultsMirrorContext
        {
            RunContext = runContext,
            Results = results
        };

        foreach (var listener in IterateRunHookListeners(runContext.RunState))
        {
            ModifyMerchantCardCreationResultsMirrors.Invoke(listener, context);
        }
    }

    /// <summary>
    /// Mirrors <see cref="Hook.TryModifyCardRewardOptions"/> followed by its Late phase.
    /// </summary>
    public static bool TryModifyCardRewardOptions(
        RunPredictionContext runContext,
        List<CardCreationResult> results,
        CardCreationOptions options,
        out List<AbstractModel> modifiers,
        IEnumerable<AbstractModel>? extraListeners = null)
    {
        var context = new TryModifyCardRewardOptionsMirrorContext
        {
            RunContext = runContext,
            Results = results,
            Options = options
        };
        var filteredExtraListeners = CompatibilityUtils.FilterHookListeners(extraListeners ?? []).ToArray();
        modifiers = [];

        foreach (var modifier in IterateRunHookListeners(runContext.RunState).Concat(filteredExtraListeners))
        {
            if (TryModifyCardRewardOptionsMirrors.Invoke(modifier, context))
            {
                modifiers.Add(modifier);
            }
        }

        foreach (var modifier in IterateRunHookListeners(runContext.RunState).Concat(filteredExtraListeners))
        {
            if (TryModifyCardRewardOptionsMirrors.InvokeLate(modifier, context))
            {
                modifiers.Add(modifier);
            }
        }

        return modifiers.Count > 0;
    }

    private static IEnumerable<AbstractModel> IterateRunHookListeners(IRunState runState)
    {
        return CompatibilityUtils.FilterHookListeners(runState.IterateHookListeners(null));
    }
}
