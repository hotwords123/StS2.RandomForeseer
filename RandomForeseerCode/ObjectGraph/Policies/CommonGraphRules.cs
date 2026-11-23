using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.ObjectGraph.Copiers;
using RandomForeseer.ObjectGraph.Metadata;
using RandomForeseer.Common;

namespace RandomForeseer.ObjectGraph.Policies;

/// <summary>Common game model, event, immutable, collection and key rules.</summary>
internal static class CommonGraphRules
{
    public static GraphReferenceRule RejectOpaqueReference { get; } =
        new("reference.opaque", GraphTypeTraits.IsOpaque, GraphReferenceStrategy.Reject);

    public static GraphValueRule RejectOpaqueValue { get; } =
        new("value.opaque", GraphTypeTraits.IsOpaque, GraphValueAction.Reject);

    public static GraphReferenceRule ShareImmutableReference { get; } =
        new("reference.immutable", GraphTypeTraits.IsImmutable, GraphReferenceStrategy.Share);

    public static GraphValueRule KeepImmutableValue { get; } =
        new("value.immutable", GraphTypeTraits.IsImmutable, GraphValueAction.Keep);

    public static GraphReferenceRule Box { get; } =
        new("reference.box", type => type.IsValueType, GraphReferenceStrategy.Clone);

    /// <summary>Share canonical instances and clone mutable ones.</summary>
    public static GraphReferenceRule Models { get; } = new(
        "reference.model",
        type => typeof(AbstractModel).IsAssignableFrom(type),
        GraphReferenceStrategy.Choose(selection => selection.GetSource<AbstractModel>().IsMutable
            ? GraphReferenceAction.Clone
            : GraphReferenceAction.Share));

    /// <summary>Omits event subscribers under the current visual-only capture assumption.</summary>
    public static GraphFieldRule ResetEvent { get; } = new(
        "field.event",
        field =>
            typeof(Delegate).IsAssignableFrom(field.FieldType) &&
            field.DeclaringType!.GetEvent(field.Name, ReflectionFlags.AllInstance | BindingFlags.DeclaredOnly) != null,
        GraphFieldAction.Reset);

    /// <summary>Resolves target sharing against the active copier's policy and context.</summary>
    public static GraphReferenceRule Delegate { get; } = new(
        "reference.delegate",
        type => typeof(Delegate).IsAssignableFrom(type),
        GraphReferenceStrategy.Choose(GraphDelegateCopier.Select, GraphDelegateCopier.Instance));

    public static GraphReferenceRule Array { get; } = new(
        "collection.array",
        type => type.IsArray ? GraphReferenceStrategy.CloneWith(GraphArrayCopier.Create(type)) : null);

    public static GraphReferenceRule List { get; } = new(
        "collection.list",
        type => GenericGraphCopier.Match(type, typeof(List<>), typeof(GraphListCopier<>)));

    public static GraphReferenceRule Dictionary { get; } = new(
        "collection.dictionary",
        type => GenericGraphCopier.Match(type, typeof(Dictionary<,>), typeof(GraphDictionaryCopier<,>)));

    public static GraphReferenceRule HashSet { get; } = new(
        "collection.hashset",
        type => GenericGraphCopier.Match(type, typeof(HashSet<>), typeof(GraphHashSetCopier<>)));

    public static GraphReferenceRule ConditionalWeakTable { get; } = new(
        "collection.conditional_weak_table",
        type => GenericGraphCopier.Match(
            type,
            typeof(ConditionalWeakTable<,>),
            typeof(GraphConditionalWeakTableCopier<,>)));

    public static GraphReferenceRule WeakReferences { get; } = new(
        "reference.weak",
        type => type == typeof(WeakReference)
            ? GraphReferenceStrategy.CloneWith(GraphWeakReferenceCopier.Instance)
            : GenericGraphCopier.Match(type, typeof(WeakReference<>), typeof(GraphWeakReferenceCopier<>)));

    public static GraphKeyRule AllowImmutableKey { get; } = new(
        "key.immutable",
        (_, runtime) => GraphTypeTraits.IsImmutable(runtime) ? true : null);

    public static GraphKeyRule AllowIdentityKey { get; } = new(
        "key.identity",
        (declared, runtime) => GraphTypeTraits.HasIdentityEquality(declared, runtime) ? true : null);

    public static IReadOnlyList<GraphReferenceRule> References { get; } =
    [
        ShareImmutableReference,
        Box,
        Delegate, ConditionalWeakTable, WeakReferences,
        Array, List, Dictionary, HashSet,
        RejectOpaqueReference
    ];

    public static IReadOnlyList<GraphValueRule> Values { get; } =
    [
        KeepImmutableValue, RejectOpaqueValue
    ];

    public static IReadOnlyList<GraphFieldRule> Fields { get; } =
    [
        ResetEvent
    ];

    public static IReadOnlyList<GraphKeyRule> Keys { get; } =
    [
        AllowImmutableKey, AllowIdentityKey
    ];

    /// <summary>Opt-in sharing of external visual resources.</summary>
    public static GraphReferenceRule ShareVisualResource { get; } =
        new("reference.visual_resource", IsVisualResource, GraphReferenceStrategy.Share);

    /// <summary>Opt-in retention of visual resource fields; place before Godot object reset rules.</summary>
    public static GraphFieldRule KeepVisualResource { get; } =
        new("field.visual_resource", field => IsVisualResource(field.FieldType), GraphFieldAction.Keep);

    /// <summary>Opt-in omission of Godot object fields without traversing or disposing the source objects.</summary>
    public static GraphFieldRule ResetGodotObject { get; } = new(
        "field.godot_object",
        field => typeof(Godot.GodotObject).IsAssignableFrom(field.FieldType),
        GraphFieldAction.Reset);

    /// <summary>Opt-in omission of transient game commands, actions and choice contexts.</summary>
    public static GraphFieldRule ResetGameExecutionContext { get; } = new(
        "field.game_execution_context",
        field =>
            typeof(AttackCommand).IsAssignableFrom(field.FieldType) ||
            typeof(AttackContext).IsAssignableFrom(field.FieldType) ||
            typeof(PlayerChoiceContext).IsAssignableFrom(field.FieldType) ||
            typeof(GameAction).IsAssignableFrom(field.FieldType),
        GraphFieldAction.Reset);

    /// <summary>Opt-in omission of asynchronous work, cancellation sources and running animations.</summary>
    public static GraphFieldRule ResetRuntimeExecutionState { get; } = new(
        "field.runtime_execution_state",
        field =>
            typeof(Task).IsAssignableFrom(field.FieldType) ||
            typeof(TaskCompletionSource).IsAssignableFrom(field.FieldType) ||
            GraphTypeTraits.IsOrDerivesFromGeneric(field.FieldType, typeof(TaskCompletionSource<>)) ||
            typeof(CancellationTokenSource).IsAssignableFrom(field.FieldType) ||
            typeof(Godot.Tween).IsAssignableFrom(field.FieldType),
        GraphFieldAction.Reset);

    private static bool IsVisualResource(Type type) =>
        typeof(Godot.Texture).IsAssignableFrom(type) ||
        typeof(Godot.Material).IsAssignableFrom(type) ||
        typeof(Godot.Shader).IsAssignableFrom(type) ||
        typeof(Godot.Font).IsAssignableFrom(type);
}
