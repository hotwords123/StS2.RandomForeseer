using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Block;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Death;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Orb;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Power;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

// Simulation-facing facade for mirrored combat hooks, analogous to vanilla Hook. Callers pass
// ordinary hook arguments; this class owns mirror context construction, listener enumeration, and
// hook-level ordering while method-specific registries and contexts remain implementation details.
internal static class HookMirrors
{
    public static void BeforePowerAmountChanged(
        CombatPredictionSimulator simulator,
        PowerModel power,
        decimal amount,
        Creature target,
        Creature? applier,
        PredictedCard? cardSource)
    {
        var context = new PowerAmountChangedMirrorContext
        {
            Simulator = simulator,
            Power = power,
            Amount = amount,
            Target = target,
            Applier = applier,
            CardSource = cardSource
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            PowerAmountChangedMirrors.InvokeBefore(listener, context);
        }
    }

    public static decimal ModifyPowerAmountGiven(
        CombatPredictionSimulator simulator,
        PowerModel power,
        Creature applier,
        decimal amount,
        Creature target,
        PredictedCard? cardSource,
        out List<AbstractModel> modifiers)
    {
        var context = new ModifyPowerAmountMirrorContext
        {
            Simulator = simulator,
            Power = power,
            Target = target,
            Applier = applier,
            CardSource = cardSource,
            Amount = amount
        };
        modifiers = [];

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.Amount = amount;
            var additive = ModifyPowerAmountMirrors.InvokeGivenAdditive(listener, context);
            if (additive != 0m)
            {
                modifiers.Add(listener);
            }

            amount += additive;
        }

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.Amount = amount;
            var multiplicative = ModifyPowerAmountMirrors.InvokeGivenMultiplicative(listener, context);
            if (multiplicative != 1m)
            {
                modifiers.Add(listener);
            }

            amount *= multiplicative;
        }

        return amount;
    }

    public static decimal ModifyPowerAmountReceived(
        CombatPredictionSimulator simulator,
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        out List<AbstractModel> modifiers)
    {
        var context = new ModifyPowerAmountMirrorContext
        {
            Simulator = simulator,
            Power = power,
            Target = target,
            Applier = applier,
            CardSource = null,
            Amount = amount
        };
        modifiers = [];

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.Amount = amount;
            var modification = ModifyPowerAmountMirrors.InvokeReceived(listener, context);
            if (modification.WasModified)
            {
                modifiers.Add(listener);
                amount = modification.Amount;
            }
        }

        return amount;
    }

    public static void AfterModifyingPowerAmountGiven(
        CombatPredictionSimulator simulator,
        IEnumerable<AbstractModel> modifiers,
        PowerModel power)
    {
        var context = new AfterModifyingPowerAmountMirrorContext { Simulator = simulator, Power = power };
        var modifierSet = modifiers.ToHashSet();
        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            if (modifierSet.Contains(listener))
            {
                PowerAmountChangedMirrors.InvokeAfterGiven(listener, context);
            }
        }
    }

    public static void AfterModifyingPowerAmountReceived(
        CombatPredictionSimulator simulator,
        IEnumerable<AbstractModel> modifiers,
        PowerModel power)
    {
        var context = new AfterModifyingPowerAmountMirrorContext { Simulator = simulator, Power = power };
        var modifierSet = modifiers.ToHashSet();
        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            if (modifierSet.Contains(listener))
            {
                PowerAmountChangedMirrors.InvokeAfterReceived(listener, context);
            }
        }
    }

    public static void AfterPowerAmountChanged(
        CombatPredictionSimulator simulator,
        PowerModel power,
        decimal amount,
        Creature target,
        Creature? applier,
        PredictedCard? cardSource)
    {
        var context = new PowerAmountChangedMirrorContext
        {
            Simulator = simulator,
            Power = power,
            Amount = amount,
            Target = target,
            Applier = applier,
            CardSource = cardSource
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            PowerAmountChangedMirrors.InvokeAfter(listener, context);
        }
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyXValue"/>.
    /// </summary>
    public static int ModifyXValue(CombatPredictionSimulator simulator, CardModel card, int value)
    {
        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            value = listener.ModifyXValue(card, value);
        }

        return value;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyKeywordsInCombat"/>.
    /// </summary>
    public static void ModifyKeywordsInCombat(
        CombatPredictionSimulator simulator,
        CardModel card,
        ISet<CardKeyword> keywords)
    {
        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            listener.TryModifyKeywordsInCombat(card, keywords);
        }
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldPayExcessEnergyCostWithStars"/>.
    /// </summary>
    public static bool ShouldPayExcessEnergyCostWithStars(
        CombatPredictionSimulator simulator,
        Player player)
    {
        return IterateCombatHookListeners(simulator)
            .Any(listener => listener.ShouldPayExcessEnergyCostWithStars(player));
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldAfflict"/>.
    /// </summary>
    public static bool ShouldAfflict(
        CombatPredictionSimulator simulator,
        CardModel card,
        AfflictionModel affliction)
    {
        return IterateCombatHookListeners(simulator)
            .All(listener => listener.ShouldAfflict(card, affliction));
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyEnergyGain"/>.
    /// </summary>
    public static decimal ModifyEnergyGain(
        CombatPredictionSimulator simulator,
        Player player,
        decimal amount,
        out IEnumerable<AbstractModel> modifiers)
    {
        List<AbstractModel> modifierList = [];
        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            var previousAmount = amount;
            amount = listener.ModifyEnergyGain(player, amount);
            if ((int)previousAmount != (int)amount)
            {
                modifierList.Add(listener);
            }
        }

        modifiers = modifierList;
        return amount;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldGainStars"/>.
    /// </summary>
    public static bool ShouldGainStars(
        CombatPredictionSimulator simulator,
        decimal amount,
        Player player)
    {
        return IterateCombatHookListeners(simulator)
            .All(listener => listener.ShouldGainStars(amount, player));
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldEtherealTrigger"/>.
    /// </summary>
    public static bool ShouldEtherealTrigger(CombatPredictionSimulator simulator, CardModel card)
    {
        return IterateCombatHookListeners(simulator)
            .All(listener => listener.ShouldEtherealTrigger(card));
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldStopCombatFromEnding"/>.
    /// </summary>
    public static bool ShouldStopCombatFromEnding(CombatPredictionSimulator simulator)
    {
        return IterateCombatHookListeners(simulator, allowWhenCombatEnding: true)
            .Any(listener => listener.ShouldStopCombatFromEnding());
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyUnblockedDamageTarget"/>.
    /// </summary>
    public static Creature ModifyUnblockedDamageTarget(
        CombatPredictionSimulator simulator,
        Creature originalTarget,
        decimal amount,
        ValueProp props,
        Creature? dealer)
    {
        var context = new ModifyUnblockedDamageTargetMirrorContext
        {
            Simulator = simulator,
            Target = originalTarget,
            Amount = amount,
            Props = props,
            Dealer = dealer
        };
        foreach (var listener in IterateCombatHookListeners(simulator, allowWhenCombatEnding: true))
        {
            context.Target = ModifyUnblockedDamageTargetMirrors.Invoke(listener, context);
        }

        return context.Target;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldCreatureBeRemovedFromCombatAfterDeath"/>.
    /// </summary>
    public static bool ShouldCreatureBeRemovedFromCombatAfterDeath(
        CombatPredictionSimulator simulator,
        Creature creature)
    {
        return IterateCombatHookListeners(simulator, allowWhenCombatEnding: true)
            .All(listener => listener.ShouldCreatureBeRemovedFromCombatAfterDeath(creature));
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldAllowHitting"/>.
    /// </summary>
    public static bool ShouldAllowHitting(CombatPredictionSimulator simulator, Creature creature)
    {
        var context = new ShouldAllowHittingMirrorContext
        {
            Simulator = simulator,
            Creature = creature
        };
        return IterateCombatHookListeners(simulator)
            .All(listener => ShouldAllowHittingMirrors.Invoke(listener, context));
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyBlock"/>.
    /// </summary>
    public static decimal ModifyBlock(
        CombatPredictionSimulator simulator,
        Creature target,
        decimal block,
        ValueProp props,
        PredictedCard? cardSource,
        CardPlay? cardPlay,
        out List<AbstractModel> modifiers)
    {
        modifiers = [];

        var cardModel = cardSource?.Preview;
        if (cardModel?.Enchantment is { } enchantment)
        {
            block += enchantment.EnchantBlockAdditive(block);
            block *= enchantment.EnchantBlockMultiplicative(block);
        }

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            var additive = listener.ModifyBlockAdditive(target, block, props, cardModel, cardPlay);
            block += additive;
            if (additive != 0)
            {
                modifiers.Add(listener);
            }
        }

        var context = new ModifyBlockMultiplicativeMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Amount = block,
            Props = props,
            CardSource = cardSource,
            CardPlay = cardPlay
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.Amount = block;
            var multiplier = ModifyBlockMultiplicativeMirrors.Invoke(listener, context);
            block *= multiplier;
            if (multiplier != 1)
            {
                modifiers.Add(listener);
            }
        }

        return Math.Max(0, block);
    }

    /// <summary>
    /// Mirrors <see cref="Hook.AfterModifyingBlockAmount"/>.
    /// </summary>
    public static void AfterModifyingBlockAmount(
        CombatPredictionSimulator simulator,
        decimal modifiedBlock,
        PredictedCard? cardSource,
        CardPlay? cardPlay,
        IReadOnlyList<AbstractModel> modifiers)
    {
        var context = new AfterModifyingBlockAmountMirrorContext
        {
            Simulator = simulator,
            ModifiedBlock = modifiedBlock,
            CardSource = cardSource,
            CardPlay = cardPlay
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            if (modifiers.Contains(listener))
            {
                AfterModifyingBlockAmountMirrors.Invoke(listener, context);
            }
        }
    }

    // Mirrors Hook.BeforeBlockGained.
    public static void BeforeBlockGained(
        CombatPredictionSimulator simulator,
        Creature creature,
        decimal amount,
        ValueProp props,
        PredictedCard? source)
    {
        var context = new BeforeBlockGainedMirrorContext
        {
            Simulator = simulator,
            Creature = creature,
            Amount = amount,
            Props = props,
            Source = source
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            BeforeBlockGainedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterBlockGained.
    public static void AfterBlockGained(
        CombatPredictionSimulator simulator,
        Creature creature,
        decimal amount,
        ValueProp props,
        PredictedCard? source)
    {
        var context = new AfterBlockGainedMirrorContext
        {
            Simulator = simulator,
            Creature = creature,
            Amount = amount,
            Props = props,
            Source = source
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterBlockGainedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterBlockBroken. Vanilla deliberately iterates the combat state directly
    // so the hook still fires for a block-breaking hit that is also ending combat.
    public static void AfterBlockBroken(
        CombatPredictionSimulator simulator,
        Creature target,
        Creature? breaker)
    {
        var context = new AfterBlockBrokenMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Breaker = breaker
        };

        foreach (var listener in IterateCombatHookListeners(simulator, allowWhenCombatEnding: true))
        {
            AfterBlockBrokenMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.ShouldDraw with listener short-circuiting.
    public static bool ShouldDraw(
        CombatPredictionSimulator simulator,
        Player player,
        bool fromHandDraw,
        [NotNullWhen(false)] out AbstractModel? modifier)
    {
        var context = new ShouldDrawMirrorContext
        {
            Simulator = simulator,
            Player = player,
            FromHandDraw = fromHandDraw
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            if (!ShouldDrawMirrors.Invoke(listener, context))
            {
                modifier = listener;
                return false;
            }
        }

        modifier = null;
        return true;
    }

    // Mirrors Hook.AfterCardDrawnEarly followed by Hook.AfterCardDrawn.
    public static void AfterCardDrawn(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        bool fromHandDraw)
    {
        var context = new AfterCardDrawnMirrorContext
        {
            Simulator = simulator,
            Card = card,
            FromHandDraw = fromHandDraw
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterCardDrawnMirrors.InvokeEarly(listener, context);
        }

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterCardDrawnMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCardExhausted.
    public static void AfterCardExhausted(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        bool causedByEthereal)
    {
        var context = new AfterCardExhaustedMirrorContext
        {
            Simulator = simulator,
            Card = card,
            CausedByEthereal = causedByEthereal
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterCardExhaustedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.ModifyShuffleOrder.
    public static void ModifyShuffleOrder(
        CombatPredictionSimulator simulator,
        Player player,
        List<PredictedCard> cards,
        bool isInitialShuffle)
    {
        var context = new ModifyShuffleOrderMirrorContext
        {
            Simulator = simulator,
            Player = player,
            Cards = cards,
            IsInitialShuffle = isInitialShuffle
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            ModifyShuffleOrderMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterShuffle.
    public static void AfterShuffle(CombatPredictionSimulator simulator, Player player)
    {
        var context = new AfterShuffleMirrorContext { Simulator = simulator, Player = player };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterShuffleMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCardDiscarded.
    public static void AfterCardDiscarded(CombatPredictionSimulator simulator, PredictedCard card)
    {
        var context = new AfterCardDiscardedMirrorContext { Simulator = simulator, Card = card };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterCardDiscardedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCardGeneratedForCombat.
    public static void AfterCardGeneratedForCombat(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        Player? creator)
    {
        var context = new AfterCardGeneratedForCombatMirrorContext
        {
            Simulator = simulator,
            Card = card,
            Creator = creator
        };

        // Prediction-local generated cards are not included as later listeners until simulated
        // hook iteration owns prediction-local card listeners.
        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterCardGeneratedForCombatMirrors.Invoke(listener, context);
        }
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ShouldPlay"/>.
    /// </summary>
    public static bool ShouldPlay(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        [NotNullWhen(false)] out AbstractModel? preventer,
        AutoPlayType autoPlayType)
    {
        var context = new ShouldPlayMirrorContext
        {
            Simulator = simulator,
            Card = card,
            AutoPlayType = autoPlayType
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            if (!ShouldPlayMirrors.Invoke(listener, context))
            {
                preventer = listener;
                return false;
            }
        }

        preventer = null;
        return true;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyEnergyCostInCombat"/>.
    /// </summary>
    public static decimal ModifyEnergyCostInCombat(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        decimal originalCost)
    {
        if (originalCost < 0)
        {
            return originalCost;
        }

        var context = new ModifyEnergyCostInCombatMirrorContext
        {
            Simulator = simulator,
            Card = card,
            Cost = originalCost
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.Cost = ModifyEnergyCostInCombatMirrors.Invoke(listener, context);
        }

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.Cost = ModifyEnergyCostInCombatMirrors.InvokeLate(listener, context);
        }

        return context.Cost;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyStarCost"/>.
    /// </summary>
    public static decimal ModifyStarCost(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        decimal originalCost)
    {
        if (originalCost < 0)
        {
            return originalCost;
        }

        var context = new ModifyStarCostMirrorContext
        {
            Simulator = simulator,
            Card = card,
            Cost = originalCost
        };
        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.Cost = ModifyStarCostMirrors.Invoke(listener, context);
        }

        return context.Cost;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyCardPlayCount"/>.
    /// </summary>
    public static int ModifyCardPlayCount(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        int originalPlayCount,
        Creature? target,
        out List<AbstractModel> modifiers)
    {
        var context = new ModifyCardPlayCountMirrorContext
        {
            Simulator = simulator,
            Card = card,
            Target = target,
            PlayCount = originalPlayCount
        };
        modifiers = [];

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            var previousPlayCount = context.PlayCount;
            context.PlayCount = ModifyCardPlayCountMirrors.Invoke(listener, context);
            if (context.PlayCount != previousPlayCount)
            {
                modifiers.Add(listener);
            }
        }

        return context.PlayCount;
    }

    /// <summary>
    /// Mirrors <see cref="Hook.AfterModifyingCardPlayCount"/>.
    /// </summary>
    public static void AfterModifyingCardPlayCount(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        IReadOnlyList<AbstractModel> modifiers)
    {
        var context = new AfterModifyingCardPlayCountMirrorContext
        {
            Simulator = simulator,
            Card = card
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            if (modifiers.Contains(listener))
            {
                ModifyCardPlayCountMirrors.InvokeAfter(listener, context);
            }
        }
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyCardPlayResultLocation"/>.
    /// </summary>
    public static CardLocation ModifyCardPlayResultLocation(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation originalLocation,
        out List<AbstractModel> modifiers)
    {
        var context = new ModifyCardPlayResultLocationMirrorContext
        {
            Simulator = simulator,
            Card = card,
            IsAutoPlay = isAutoPlay,
            Resources = resources,
            Location = originalLocation
        };
        modifiers = [];

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            var previousLocation = context.Location;
            context.Location = ModifyCardPlayResultLocationMirrors.Invoke(listener, context);
            if (context.Location != previousLocation)
            {
                modifiers.Add(listener);
            }
        }

        return context.Location;
    }

    // Vanilla Hook has no facade for this step. Mirrors CardModel.OnPlayWrapper's direct
    // iteration over the modifier list returned by Hook.ModifyCardPlayResultLocation.
    public static void AfterModifyingCardPlayResultLocation(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        CardLocation location,
        IReadOnlyList<AbstractModel> modifiers)
    {
        var context = new AfterModifyingCardPlayResultLocationMirrorContext
        {
            Simulator = simulator,
            Card = card,
            Location = location
        };

        foreach (var modifier in modifiers)
        {
            ModifyCardPlayResultLocationMirrors.InvokeAfter(modifier, context);
        }
    }

    // Mirrors Hook.BeforeCardPlayed. Unlike the two after phases, vanilla suppresses this
    // guarded dispatch when combat was already over or ending at dispatch start.
    public static void BeforeCardPlayed(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        CardPlay cardPlay)
    {
        var context = new BeforeCardPlayedMirrorContext
        {
            Simulator = simulator,
            Card = card,
            CardPlay = cardPlay
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            BeforeCardPlayedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterCardPlayed's ordinary pass followed by a fresh full late pass. Vanilla
    // deliberately iterates the combat state directly so a killing card can finish resolving.
    public static void AfterCardPlayed(
        CombatPredictionSimulator simulator,
        PredictedCard card,
        CardPlay cardPlay)
    {
        var context = new AfterCardPlayedMirrorContext
        {
            Simulator = simulator,
            Card = card,
            CardPlay = cardPlay
        };

        foreach (var listener in IterateCombatHookListeners(simulator, allowWhenCombatEnding: true))
        {
            AfterCardPlayedMirrors.Invoke(listener, context);
        }

        foreach (var listener in IterateCombatHookListeners(simulator, allowWhenCombatEnding: true))
        {
            AfterCardPlayedMirrors.InvokeLate(listener, context);
        }
    }

    // Mirrors Hook.AfterCurrentHpChanged.
    public static void AfterCurrentHpChanged(
        CombatPredictionSimulator simulator,
        Creature creature,
        decimal delta)
    {
        var context = new AfterCurrentHpChangedMirrorContext
        {
            Simulator = simulator,
            Creature = creature,
            Delta = delta
        };

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            AfterCurrentHpChangedMirrors.Invoke(listener, context);
        }
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyDamage"/>.
    /// </summary>
    public static decimal ModifyDamage(
        CombatPredictionSimulator simulator,
        Creature? target,
        Creature? dealer,
        decimal damage,
        ValueProp props,
        PredictedCard? cardSource,
        CardPlay? cardPlay)
    {
        var cardModel = cardSource?.Preview;
        if (cardModel?.Enchantment is { } enchantment)
        {
            damage += enchantment.EnchantDamageAdditive(damage, props);
            damage *= enchantment.EnchantDamageMultiplicative(damage, props);
        }

        var context = new ModifyDamageMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Dealer = dealer,
            Amount = damage,
            Props = props,
            CardSource = cardSource,
            CardPlay = cardPlay
        };
        foreach (var listener in IterateRunHookListeners(simulator, applyCompatibilityFilter: false))
        {
            context.Amount = damage;
            damage += ModifyDamageMirrors.InvokeAdditive(listener, context);
        }

        foreach (var listener in IterateRunHookListeners(simulator, applyCompatibilityFilter: false))
        {
            context.Amount = damage;
            damage *= ModifyDamageMirrors.InvokeMultiplicative(listener, context);
        }

        var cap = decimal.MaxValue;
        foreach (var listener in IterateRunHookListeners(simulator, applyCompatibilityFilter: false))
        {
            cap = Math.Min(cap, listener.ModifyDamageCap(target, props, dealer, cardModel, cardPlay));
        }

        return Math.Max(0, Math.Min(damage, cap));
    }

    /// <summary>
    /// Mirrors <see cref="Hook.ModifyHpLost"/>.
    /// </summary>
    public static decimal ModifyHpLost(
        CombatPredictionSimulator simulator,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        PredictedCard? cardSource,
        HpLossHookPhase phases,
        out List<AbstractModel> modifiers)
    {
        var context = new ModifyHpLostMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Amount = amount,
            Props = props,
            Dealer = dealer,
            CardSource = cardSource
        };
        modifiers = [];

        if (phases.HasFlag(HpLossHookPhase.BeforeOsty))
        {
            foreach (var listener in IterateRunHookListeners(simulator))
            {
                var previousAmount = context.Amount;
                context.Amount = ModifyHpLostMirrors.InvokeBeforeOsty(listener, context);
                if (decimal.Truncate(previousAmount) != decimal.Truncate(context.Amount))
                {
                    modifiers.Add(listener);
                }
            }

            foreach (var listener in IterateRunHookListeners(simulator))
            {
                var previousAmount = context.Amount;
                context.Amount = ModifyHpLostMirrors.InvokeBeforeOstyLate(listener, context);
                if (decimal.Truncate(previousAmount) != decimal.Truncate(context.Amount))
                {
                    modifiers.Add(listener);
                }
            }
        }

        if (phases.HasFlag(HpLossHookPhase.AfterOsty))
        {
            foreach (var listener in IterateRunHookListeners(simulator))
            {
                var previousAmount = context.Amount;
                context.Amount = ModifyHpLostMirrors.InvokeAfterOsty(listener, context);
                if (decimal.Truncate(previousAmount) != decimal.Truncate(context.Amount))
                {
                    modifiers.Add(listener);
                }
            }

            foreach (var listener in IterateRunHookListeners(simulator))
            {
                var previousAmount = context.Amount;
                context.Amount = ModifyHpLostMirrors.InvokeAfterOstyLate(listener, context);
                if (decimal.Truncate(previousAmount) != decimal.Truncate(context.Amount))
                {
                    modifiers.Add(listener);
                }
            }
        }

        return context.Amount;
    }

    // Mirrors Hook.AfterDamageGiven.
    public static void AfterDamageGiven(
        CombatPredictionSimulator simulator,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        PredictedCard? source)
    {
        var context = new AfterDamageGivenMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Result = result,
            Props = props,
            Dealer = dealer,
            Source = source
        };

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            AfterDamageGivenMirrors.Invoke(listener, context);
        }
    }

    /// <summary>
    /// Mirrors <see cref="Hook.AfterModifyingHpLostAfterOsty"/>.
    /// </summary>
    public static void AfterModifyingHpLostAfterOsty(
        CombatPredictionSimulator simulator,
        IReadOnlyList<AbstractModel> modifiers)
    {
        var context = new AfterModifyingHpLostMirrorContext { Simulator = simulator };

        foreach (var modifier in IterateRunHookListeners(simulator))
        {
            if (modifiers.Contains(modifier))
            {
                AfterModifyingHpLostAfterOstyMirrors.Invoke(modifier, context);
            }
        }
    }

    // Mirrors Hook.BeforeDamageReceived.
    public static void BeforeDamageReceived(
        CombatPredictionSimulator simulator,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        PredictedCard? source)
    {
        var context = new BeforeDamageReceivedMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Amount = amount,
            Props = props,
            Dealer = dealer,
            Source = source
        };

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            BeforeDamageReceivedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterDamageReceived followed by Hook.AfterDamageReceivedLate.
    public static void AfterDamageReceived(
        CombatPredictionSimulator simulator,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        PredictedCard? source)
    {
        var context = new AfterDamageReceivedMirrorContext
        {
            Simulator = simulator,
            Target = target,
            Result = result,
            Props = props,
            Dealer = dealer,
            Source = source
        };

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            AfterDamageReceivedMirrors.Invoke(listener, context);
        }

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            AfterDamageReceivedMirrors.InvokeLate(listener, context);
        }
    }

    // Mirrors Hook.BeforeAttack.
    public static void BeforeAttack(CombatPredictionSimulator simulator, AttackCommand command)
    {
        var context = new BeforeAttackMirrorContext { Simulator = simulator, Command = command };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            BeforeAttackMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.ModifyAttackHitCount with listener-to-listener result chaining.
    public static int ModifyAttackHitCount(
        CombatPredictionSimulator simulator,
        AttackCommand command,
        int originalHitCount)
    {
        var context = new ModifyAttackHitCountMirrorContext
        {
            Simulator = simulator,
            Command = command,
            HitCount = originalHitCount
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            context.HitCount = ModifyAttackHitCountMirrors.Invoke(listener, context);
        }

        return context.HitCount;
    }

    // Mirrors Hook.AfterAttack.
    public static void AfterAttack(CombatPredictionSimulator simulator, AttackCommand command)
    {
        var context = new AfterAttackMirrorContext { Simulator = simulator, Command = command };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterAttackMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.ShouldDie followed by Hook.ShouldDieLate, including first-preventer short-circuiting.
    public static bool ShouldDie(
        CombatPredictionSimulator simulator,
        Creature creature,
        [NotNullWhen(false)] out AbstractModel? preventer)
    {
        var context = new ShouldDieMirrorContext { Simulator = simulator, Creature = creature };

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            if (!ShouldDieMirrors.Invoke(listener, context))
            {
                preventer = listener;
                return false;
            }
        }

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            if (!ShouldDieMirrors.InvokeLate(listener, context))
            {
                preventer = listener;
                return false;
            }
        }

        preventer = null;
        return true;
    }

    // Mirrors Hook.AfterPreventingDeath's only-preventer dispatch.
    public static void AfterPreventingDeath(
        CombatPredictionSimulator simulator,
        AbstractModel preventer,
        Creature creature)
    {
        var context = new AfterPreventingDeathMirrorContext
        {
            Simulator = simulator,
            Creature = creature
        };

        if (IterateRunHookListeners(simulator).Contains(preventer))
        {
            AfterPreventingDeathMirrors.Invoke(preventer, context);
        }
    }

    // Mirrors Hook.BeforeDeath.
    public static void BeforeDeath(CombatPredictionSimulator simulator, Creature creature)
    {
        var context = new BeforeDeathMirrorContext { Simulator = simulator, Creature = creature };

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            BeforeDeathMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterDeath.
    public static void AfterDeath(
        CombatPredictionSimulator simulator,
        Creature creature,
        bool wasRemovalPrevented)
    {
        var context = new AfterDeathMirrorContext
        {
            Simulator = simulator,
            Creature = creature,
            WasRemovalPrevented = wasRemovalPrevented
        };

        foreach (var listener in IterateRunHookListeners(simulator))
        {
            AfterDeathMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.ModifyOrbPassiveTriggerCount.
    public static int ModifyOrbPassiveTriggerCount(
        CombatPredictionSimulator simulator,
        OrbModel orb,
        int triggerCount,
        out List<AbstractModel> modifiers)
    {
        var context = new ModifyOrbPassiveTriggerCountMirrorContext
        {
            Simulator = simulator,
            Orb = orb,
            TriggerCount = triggerCount
        };
        modifiers = [];

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            var newTriggerCount = ModifyOrbPassiveTriggerCountMirrors.Invoke(listener, context);
            if (newTriggerCount != context.TriggerCount)
            {
                context.TriggerCount = newTriggerCount;
                modifiers.Add(listener);
            }
        }

        return context.TriggerCount;
    }

    // Mirrors Hook.AfterOrbChanneled.
    public static void AfterOrbChanneled(CombatPredictionSimulator simulator, Player player, OrbModel orb)
    {
        var context = new AfterOrbChanneledMirrorContext
        {
            Simulator = simulator,
            Player = player,
            Orb = orb
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterOrbChanneledMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterOrbEvoked.
    public static void AfterOrbEvoked(
        CombatPredictionSimulator simulator,
        OrbModel orb,
        IReadOnlyList<Creature> targets)
    {
        var context = new AfterOrbEvokedMirrorContext
        {
            Simulator = simulator,
            Orb = orb,
            Targets = targets
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterOrbEvokedMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.AfterAutoPostPlayPhaseEntered.
    public static void AfterAutoPostPlayPhaseEntered(CombatPredictionSimulator simulator, Player player)
    {
        var context = new AfterAutoPostPlayMirrorContext { Simulator = simulator, Player = player };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            AfterAutoPostPlayPhaseEnteredMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.BeforeSideTurnEnd.
    public static void BeforeSideTurnEnd(
        CombatPredictionSimulator simulator,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        var context = new BeforeSideTurnEndMirrorContext
        {
            Simulator = simulator,
            Side = side,
            Participants = participants
        };

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            BeforeSideTurnEndMirrors.InvokeVeryEarly(listener, context);
        }

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            BeforeSideTurnEndMirrors.InvokeEarly(listener, context);
        }

        foreach (var listener in IterateCombatHookListeners(simulator))
        {
            BeforeSideTurnEndMirrors.Invoke(listener, context);
        }
    }

    /// <summary>
    /// Enumerates combat Hook listeners using the vanilla combat-ending guard and the compatibility filter.
    /// </summary>
    /// <param name="simulator">The prediction simulator whose shadow combat state supplies the listeners.</param>
    /// <param name="applyCompatibilityFilter">
    /// Whether to skip Mod listeners when the corresponding compatibility setting is enabled.
    /// </param>
    /// <param name="allowWhenCombatEnding">
    /// Whether to enumerate listeners even when the shadow combat is already over or ending.
    /// </param>
    /// <returns>
    /// An ordered listener sequence, or an empty sequence when the combat-ending guard suppresses dispatch.
    /// </returns>
    private static IEnumerable<AbstractModel> IterateCombatHookListeners(
        CombatPredictionSimulator simulator,
        bool applyCompatibilityFilter = true,
        bool allowWhenCombatEnding = false)
    {
        if (!allowWhenCombatEnding && simulator.IsOverOrEnding)
        {
            return [];
        }

        var listeners = simulator.State.IterateHookListeners();

        return applyCompatibilityFilter
            ? CompatibilityUtils.FilterHookListeners(listeners)
            : listeners;
    }

    /// <summary>
    /// Enumerates run and combat Hook listeners from the simulator's prediction-associated run state.
    /// </summary>
    /// <param name="simulator">The prediction simulator whose run and combat state supply the listeners.</param>
    /// <param name="applyCompatibilityFilter">
    /// Whether to skip Mod listeners when the corresponding compatibility setting is enabled.
    /// </param>
    /// <returns>An ordered listener sequence.</returns>
    private static IEnumerable<AbstractModel> IterateRunHookListeners(
        CombatPredictionSimulator simulator,
        bool applyCompatibilityFilter = true)
    {
        var combatState = simulator.State.CombatState;
        var listeners = combatState.RunState.IterateHookListeners(combatState);

        return applyCompatibilityFilter
            ? CompatibilityUtils.FilterHookListeners(listeners)
            : listeners;
    }
}
