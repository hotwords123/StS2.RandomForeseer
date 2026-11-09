using RandomForeseer.ObjectGraph.Metadata;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.ObjectGraph.Plans;

/// <summary>A policy-local plan: cached bindings and field operations, never scope state or instance decisions.</summary>
/// <remarks>Execution receives the active DFS copier as an argument; plans must never retain it or its diagnostic path.</remarks>
internal abstract class GraphTypePlan(Type type, GraphCopyPolicy policy)
{
    public Type Type { get; } = type;

    protected GraphCopyPolicy Policy { get; } = policy;

    public GraphRuleMatch<GraphReferenceStrategy> ReferenceRule => field ??= Policy.BindReferenceRule(Type);

    /// <summary>Bound strategy for non-nullable values; nullable storage delegates to its underlying type in the copier.</summary>
    public GraphRuleMatch<GraphValueAction> ValueRule => field ??= Policy.BindValueRule(Type);

    /// <summary>Only fields requiring writes; complete metadata remains in GraphTypeLayout.</summary>
    public IReadOnlyList<GraphFieldPlan> Fields => field ??= BuildFields();

    internal abstract bool UseShallowCopy { get; }

    private IReadOnlyList<GraphFieldPlan> BuildFields()
    {
        List<GraphFieldPlan> fields = [];

        foreach (var info in GraphTypeLayout.For(Type).Fields)
        {
            var rule = Policy.GetFieldRule(info).Strategy;

            if (rule == GraphFieldAction.Keep)
            {
                continue;
            }

            switch (rule)
            {
                case GraphFieldAction.Copy when Policy.GetPlan(info.FieldType).UseShallowCopy:
                    continue;

                case GraphFieldAction.Copy:
                case GraphFieldAction.Reset:
                    fields.Add(new GraphFieldPlan(info, rule == GraphFieldAction.Reset));
                    break;

                default:
                    throw new InvalidOperationException($"Invalid field rule {rule} for {info}.");
            }
        }

        return fields;
    }

    /// <summary>Populates a registered same-type shallow copy; boxed values apply their value rule without changing box identity.</summary>
    /// <remarks>Active root operation only. Fields use the supplied copier for child paths and recursion.</remarks>
    internal abstract void PopulateObject(object source, object clone, PredictionGraphCopier copier);
}

/// <summary>Dispatches typed values and supplies field execution for class objects or real boxes of the same CLR type.</summary>
internal sealed class GraphTypePlan<T>(GraphCopyPolicy policy)
    : GraphTypePlan(typeof(T), policy)
{
    private bool? _useShallowCopy;

    private GraphExecutionCompiler.ObjectPopulator ObjectPopulator =>
        field ??= GraphExecutionCompiler.CompileObject(this);

    private GraphExecutionCompiler.ValueCopier<T> ValueCopier =>
        field ??= GraphExecutionCompiler.CompileValue(this);

    /// <summary>Whether copying this storage can be omitted under the current policy.</summary>
    internal override bool UseShallowCopy => _useShallowCopy ??= CanUseShallowCopy();

    private bool CanUseShallowCopy()
    {
        if (!typeof(T).IsValueType)
        {
            // A base type's rule says nothing about the runtime rule of its descendants.
            return typeof(T).IsSealed && ReferenceRule.Strategy.StaticAction == GraphReferenceAction.Share;
        }

        if (Nullable.GetUnderlyingType(typeof(T)) is { } underlying)
        {
            return Policy.GetPlan(underlying).UseShallowCopy;
        }

        return ValueRule.Strategy == GraphValueAction.Keep ||
               ValueRule.Strategy == GraphValueAction.Fields && Fields.Count == 0;
    }

    /// <summary>Applies the value rule to T, which must be a non-nullable value type.</summary>
    /// <remarks>
    /// Active root operation only. Copies through a typed local without boxing; the copier unwraps nullable storage first.
    /// Uses the supplied copier for DFS recursion and captures rejection diagnostics at the current path before unwinding.
    /// </remarks>
    internal T CopyValue(in T source, PredictionGraphCopier copier)
    {
        var rule = ValueRule;
        var action = rule.Strategy;

        switch (action)
        {
            case GraphValueAction.Keep:
                return source;

            case GraphValueAction.Fields:
                return UseShallowCopy ? source : ValueCopier(in source, copier);

            case GraphValueAction.Reject:
                throw copier.Unsupported($"Value {typeof(T)} is rejected by {rule.Id}.");

            default:
                throw new InvalidOperationException($"{copier.Path}: invalid value action {action}.");
        }
    }

    internal override void PopulateObject(object source, object clone, PredictionGraphCopier copier)
    {
        if (typeof(T).IsValueType)
        {
            var action = ValueRule.Strategy;

            switch (action)
            {
                case GraphValueAction.Keep:
                    return;

                case GraphValueAction.Fields:
                    break;

                case GraphValueAction.Reject:
                    throw copier.Unsupported($"Boxed value {typeof(T)} is rejected by {ValueRule.Id}.");

                default:
                    throw new InvalidOperationException($"{copier.Path}: invalid value action {action}.");
            }
        }

        // Write the registered object/box itself. Never substitute a freshly boxed value.
        if (Fields.Count != 0)
        {
            ObjectPopulator(source, clone, copier);
        }
    }
}
