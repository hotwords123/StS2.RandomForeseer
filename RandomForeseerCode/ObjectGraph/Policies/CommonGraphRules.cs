using System.Reflection;
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

    public static GraphKeyRule AllowImmutableKey { get; } = new(
        "key.immutable",
        (_, runtime) => GraphTypeTraits.IsImmutable(runtime) ? true : null);

    public static GraphKeyRule AllowIdentityKey { get; } = new(
        "key.identity",
        (declared, runtime) => GraphTypeTraits.HasIdentityEquality(declared, runtime) ? true : null);

    public static IReadOnlyList<GraphReferenceRule> References { get; } =
    [
        ShareImmutableReference, Box, Delegate, Array, List, Dictionary, HashSet
    ];

    public static IReadOnlyList<GraphValueRule> Values { get; } =
    [
        KeepImmutableValue
    ];

    public static IReadOnlyList<GraphFieldRule> Fields { get; } =
    [
        ResetEvent
    ];

    public static IReadOnlyList<GraphKeyRule> Keys { get; } =
    [
        AllowImmutableKey, AllowIdentityKey
    ];
}
