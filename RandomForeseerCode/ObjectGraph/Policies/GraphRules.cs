using System.Reflection;
using RandomForeseer.ObjectGraph.Copiers;

namespace RandomForeseer.ObjectGraph.Policies;

/// <summary>A named match. Null means no match; the first matching rule supplies the strategy.</summary>
internal sealed record GraphReferenceRule(string Id, Func<Type, GraphReferenceStrategy?> Match)
{
    public GraphReferenceRule(string id, Func<Type, bool> match, GraphReferenceStrategy strategy)
        : this(id, type => match(type) ? strategy : null)
    {
    }
}

/// <summary>A strategy for non-nullable value storage; nullable wrappers are handled transparently by the copier.</summary>
internal sealed record GraphValueRule(string Id, Func<Type, GraphValueAction?> Match)
{
    public GraphValueRule(string id, Func<Type, bool> match, GraphValueAction action)
        : this(id, type => match(type) ? action : null)
    {
    }
}

/// <summary>Matches a field's metadata to a static action, independently of its containing runtime type.</summary>
internal sealed record GraphFieldRule(string Id, Func<FieldInfo, GraphFieldAction?> Match)
{
    public GraphFieldRule(string id, Func<FieldInfo, bool> match, GraphFieldAction action)
        : this(id, field => match(field) ? action : null)
    {
    }
}

/// <summary>Determines whether default key equality remains stable during graph copying.</summary>
internal sealed record GraphKeyRule(string Id, Func<Type, Type, bool?> Match);

internal sealed record GraphRuleMatch<T>(string Id, T Strategy);

/// <summary>Controls whether an identity-bearing object is shared, cloned or rejected.</summary>
internal enum GraphReferenceAction
{
    Reject,

    /// <summary>Use the original object without traversing or registering it.</summary>
    Share,

    /// <summary>Create a mapped identity using the bound reference copier.</summary>
    Clone
}

/// <summary>Controls how a non-nullable inline value or the contents of a real box are processed.</summary>
internal enum GraphValueAction
{
    Reject,

    /// <summary>Keep the whole value, including any references it contains.</summary>
    Keep,

    /// <summary>Apply field rules to a reviewed non-scalar, non-nullable struct.</summary>
    Fields
}

/// <summary>Static field binding; Keep is omitted from the executable plan.</summary>
internal enum GraphFieldAction
{
    /// <summary>Dispatch using the declared field type; statically unnecessary copies are omitted.</summary>
    Copy,

    /// <summary>Retain the shallow-copied field without applying its type rule.</summary>
    Keep,

    /// <summary>Write default without reading or traversing the source field.</summary>
    Reset
}

/// <summary>Selects an action for the current source using the active copier's policy and caller context.</summary>
/// <remarks>Decisions must be stable and independent of traversal order. Do not retain the selection or mutate its inputs.</remarks>
internal delegate GraphReferenceAction GraphReferenceSelector(GraphReferenceSelection selection);

/// <summary>Binds either a fixed reference action or an instance selector, plus its cloning strategy.</summary>
internal sealed class GraphReferenceStrategy
{
    private readonly GraphReferenceSelector? _selector;

    private GraphReferenceStrategy(
        GraphReferenceAction? action,
        GraphReferenceSelector? selector = null,
        IGraphReferenceCopier? copier = null)
    {
        StaticAction = action;
        _selector = selector;
        Copier = copier;
    }

    /// <summary>Null for instance selectors; only fixed actions allow plan-time omission.</summary>
    public GraphReferenceAction? StaticAction { get; }

    public IGraphReferenceCopier? Copier { get; }

    public GraphReferenceAction Select(GraphReferenceSelection selection) => StaticAction ?? _selector!(selection);

    public static GraphReferenceStrategy Share { get; } = new(GraphReferenceAction.Share);

    public static GraphReferenceStrategy Reject { get; } = new(GraphReferenceAction.Reject);

    public static GraphReferenceStrategy Clone { get; } =
        new(GraphReferenceAction.Clone, copier: FieldsGraphReferenceCopier.Instance);

    public static GraphReferenceStrategy CloneWith(IGraphReferenceCopier copier) =>
        new(GraphReferenceAction.Clone, copier: copier);

    /// <summary>Selects per non-null source instance; cached plans retain the selector, never its selection or result.</summary>
    public static GraphReferenceStrategy Choose(
        GraphReferenceSelector selector,
        IGraphReferenceCopier? copier = null) =>
        new(null, selector, copier ?? FieldsGraphReferenceCopier.Instance);
}

/// <summary>The current non-null source and read-only services for selecting its reference action.</summary>
/// <remarks>
/// Passed by value for each decision; the source and caller context remain references to the original objects.
/// Queries may bind type plans but never copy objects or read/write scope mappings. Source and context data must
/// stay stable across all roots copied by the same copier. Selectors and cached policies must not retain this selection.
/// </remarks>
internal readonly struct GraphReferenceSelection(object source, PredictionGraphCopier copier)
{
    /// <summary>The original object being classified, including its identity when it is a real box.</summary>
    public object Source { get; } = source;

    /// <summary>Resolves another non-null object's action under the same policy and context, without changing Source.</summary>
    /// <remarks>Does not consult existing clones. Queries must not form a recursive cycle of selector decisions.</remarks>
    public GraphReferenceAction GetReferenceAction(object source) => copier.GetReferenceAction(source);

    /// <summary>Returns the original source as T; throws if its runtime type is incompatible.</summary>
    public T GetSource<T>() where T : class => Source as T
        ?? throw new InvalidOperationException($"A source of type {typeof(T)} is required.");

    /// <summary>Returns this copier's caller-supplied context; throws if absent or incompatible with T.</summary>
    public T GetContext<T>() where T : class => copier.Context as T
        ?? throw new InvalidOperationException($"A copy context of type {typeof(T)} is required.");
}
