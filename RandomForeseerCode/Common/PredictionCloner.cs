using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.Common;

/// <summary>Provides low-level object copying for prediction code.</summary>
/// <remarks>
/// This primitive does not apply game-model clone/reset semantics, register objects, or build an isolated object
/// graph. Future graph copying can use it to allocate a copy before registering and remapping reference fields.
/// </remarks>
internal static class PredictionCloner
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly MirrorMethodSpec[] ModelCloneMethods =
    [
        new(typeof(AbstractModel), "DeepCloneFields", BindingFlags.Instance | BindingFlags.NonPublic, []),
        new(typeof(AbstractModel), "AfterCloned", BindingFlags.Instance | BindingFlags.NonPublic, [])
    ];

    private static readonly HashSet<Type> KnownModelTypes =
    [
        typeof(CardModel),
        typeof(EnchantmentModel),
        typeof(AfflictionModel),
        typeof(PotionModel),
        typeof(OrbModel),
        typeof(SovereignBlade)
    ];

    private static readonly ConcurrentDictionary<Type, CloneTypePlan> TypePlans = new();

    private sealed record CloneTypePlan(FieldInfo[] EventFields);

    /// <summary>Copies an object's fields for a lightweight snapshot or object-graph capture.</summary>
    /// <remarks>
    /// Uses Object.MemberwiseClone without running constructors or model clone hooks. Referenced objects, including
    /// collections, event subscribers and owner links, remain shared; self-references still point to the source.
    /// Use only when sharing is acceptable or the references will be remapped. Do not mutate shared state through
    /// the copy. This method does not change model mutability or make native resources and model getters safe to use.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static T ShallowClone<T>(T source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        return (T)MemberwiseClone(source);
    }

    /// <summary>Returns canonical models unchanged and copies mutable models for prediction.</summary>
    /// <remarks>
    /// Use for model snapshots that must retain current runtime state without changing the source. A canonical
    /// result is the original instance and must not be treated as writable. Mutable copies duplicate supported
    /// model-owned state and card attachments, keep uninitialized fields lazy, and discard copied event subscribers
    /// without invoking gameplay operations. This is not a full graph clone: owners and other references remain
    /// shared, so model getters may still read live state. Unreviewed clone overrides use category fallback rules;
    /// additional mutable fields may remain shared, and a warning is logged.
    /// </remarks>
    public static T CloneModel<T>(T source) where T : AbstractModel
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.IsMutable)
        {
            return source;
        }

        var type = source.GetType();
        var plan = TypePlans.GetOrAdd(type, CreateTypePlan);
        var clone = ShallowClone(source);

        switch (clone)
        {
            case CardModel card:
                FinalizeCloneCard(card);
                break;

            case EnchantmentModel enchantment:
                FinalizeCloneEnchantment(enchantment);
                break;
        }

        foreach (var field in plan.EventFields)
        {
            field.SetValue(clone, null);
        }

        return clone;
    }

    private static void FinalizeCloneCard(CardModel card)
    {
        card.AssertMutable();

        card._keywords = card._keywords?.ToHashSet();
        card._dynamicVars = CloneDynamicVars(card._dynamicVars, card);
        card._energyCost = card._energyCost?.Clone(card);
        card._temporaryStarCosts = [.. card._temporaryStarCosts];

        if (card.Enchantment is { } enchantment)
        {
            var copy = CloneModel(enchantment);
            copy.AssertMutable();
            copy._card = card;
            card.Enchantment = copy;
        }

        if (card.Affliction is { } affliction)
        {
            var copy = CloneModel(affliction);
            copy.AssertMutable();
            copy._card = card;
            card.Affliction = copy;
        }

        card._canonicalInstance ??= ModelDb.GetById<CardModel>(card.Id);
    }

    private static void FinalizeCloneEnchantment(EnchantmentModel enchantment)
    {
        enchantment.AssertMutable();
        enchantment._dynamicVars = CloneDynamicVars(enchantment._dynamicVars, enchantment);
    }

    private static DynamicVarSet? CloneDynamicVars(DynamicVarSet? source, AbstractModel owner)
    {
        if (source == null)
        {
            return null;
        }

        // DynamicVarSet.Clone calls DynamicVar.Clone, which resets preview/enchantment values.
        // Copy each existing variable's complete state and only redirect its owner.
        var clone = new DynamicVarSet(source.Values.Select(ShallowClone));
        foreach (var variable in clone.Values)
        {
            variable._owner = owner;
        }
        return clone;
    }

    private static CloneTypePlan CreateTypePlan(Type type)
    {
        foreach (var method in ModelCloneMethods)
        {
            if (!method.TryGetOverride(type, out var overrideMethod) ||
                overrideMethod.DeclaringType != null && KnownModelTypes.Contains(overrideMethod.DeclaringType))
            {
                continue;
            }

            Entry.Logger.Warn(
                $"Prediction cloning does not support {type.FullName}.{method.Name} override " +
                $"in {overrideMethod.DeclaringType?.FullName}.{overrideMethod.Name}");
        }

        List<FieldInfo> events = [];

        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var eventInfo in current.GetEvents(InstanceMembers | BindingFlags.DeclaredOnly))
            {
                var field = current.GetField(eventInfo.Name, InstanceMembers | BindingFlags.DeclaredOnly);
                if (field == null || field.FieldType != eventInfo.EventHandlerType)
                {
                    Entry.Logger.Warn(
                        $"Prediction cloning does not support {type.FullName}.{eventInfo.Name} event " +
                        $"declared in {current.FullName}");
                    continue;
                }

                events.Add(field);
            }

            if (current == typeof(AbstractModel))
            {
                break;
            }
        }

        return new CloneTypePlan([.. events]);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
    private static extern object MemberwiseClone(object source);
}
