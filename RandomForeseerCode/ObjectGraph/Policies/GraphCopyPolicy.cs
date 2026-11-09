using System.Reflection;
using RandomForeseer.ObjectGraph.Plans;

namespace RandomForeseer.ObjectGraph.Policies;

/// <summary>Ordered rules and policy-local type plans. The first match wins, including rejection.</summary>
/// <remarks>
/// Configuration is copied at construction and stays stable across scopes. Matchers, selectors and copiers
/// must not mutate sources or retain per-operation state. Binding and copying are synchronous and single-threaded.
/// Instance selectors receive the current source and copier services through GraphReferenceSelection;
/// cached rules and plans must not capture that selection, its caller context or its instance action.
/// Share/Keep terminate traversal; field rules only apply to objects or values selected for field copying.
/// Value rules match non-nullable storage; the copier unwraps nullable values before consulting their underlying rules.
/// </remarks>
internal sealed class GraphCopyPolicy
{
    private readonly GraphReferenceRule[] _references;
    private readonly GraphValueRule[] _values;
    private readonly GraphFieldRule[] _fields;
    private readonly GraphKeyRule[] _keys;

    private readonly GraphRuleMatch<GraphReferenceStrategy> _referenceFallback;
    private readonly GraphRuleMatch<GraphValueAction> _valueFallback;

    private readonly Dictionary<Type, GraphTypePlan> _plans = [];

    /// <summary>Opaque-type rejection precedes specific and common rules; fallbacks run only when none matches.</summary>
    public GraphCopyPolicy(
        IEnumerable<GraphReferenceRule>? referenceRules = null,
        IEnumerable<GraphValueRule>? valueRules = null,
        IEnumerable<GraphFieldRule>? fieldRules = null,
        IEnumerable<GraphKeyRule>? keyRules = null,
        GraphReferenceStrategy? referenceFallback = null,
        GraphValueAction valueFallback = GraphValueAction.Reject)
    {
        _references =
        [
            CommonGraphRules.RejectOpaqueReference,
            .. referenceRules ?? [],
            .. CommonGraphRules.References
        ];
        _values =
        [
            CommonGraphRules.RejectOpaqueValue,
            .. valueRules ?? [],
            .. CommonGraphRules.Values
        ];
        _fields = [.. fieldRules ?? [], .. CommonGraphRules.Fields];
        _keys = [.. keyRules ?? [], .. CommonGraphRules.Keys];
        _referenceFallback = new("reference.fallback", referenceFallback ?? GraphReferenceStrategy.Reject);
        _valueFallback = new("value.fallback", valueFallback);
    }

    /// <summary>Gets a lazily bound plan shared only by scopes using this stable policy instance.</summary>
    internal GraphTypePlan<T> GetPlan<T>()
    {
        if (_plans.TryGetValue(typeof(T), out var existing))
        {
            return (GraphTypePlan<T>)existing;
        }

        var plan = new GraphTypePlan<T>(this);
        // Register before lazy binding follows dependent types; plans contain no scope or source state.
        _plans.Add(typeof(T), plan);
        return plan;
    }

    /// <summary>Gets the same policy-local plan by its exact runtime or declared storage type.</summary>
    internal GraphTypePlan GetPlan(Type type)
    {
        if (_plans.TryGetValue(type, out var existing))
        {
            return existing;
        }

        var plan = (GraphTypePlan)Activator.CreateInstance(typeof(GraphTypePlan<>).MakeGenericType(type), this)!;
        _plans.Add(type, plan);
        return plan;
    }

    internal GraphRuleMatch<GraphReferenceStrategy> BindReferenceRule(Type type)
    {
        foreach (var rule in _references)
        {
            if (rule.Match(type) is { } strategy)
            {
                return new(rule.Id, strategy);
            }
        }

        return _referenceFallback;
    }

    internal GraphRuleMatch<GraphValueAction> BindValueRule(Type type)
    {
        foreach (var rule in _values)
        {
            if (rule.Match(type) is { } strategy)
            {
                return new(rule.Id, strategy);
            }
        }

        return _valueFallback;
    }

    public GraphRuleMatch<GraphFieldAction> GetFieldRule(FieldInfo field)
    {
        foreach (var rule in _fields)
        {
            if (rule.Match(field) is { } strategy)
            {
                return new(rule.Id, strategy);
            }
        }

        return new("field.copy", GraphFieldAction.Copy);
    }

    public bool IsSupportedKeyType(Type declaredType, Type runtimeType)
    {
        foreach (var rule in _keys)
        {
            if (rule.Match(declaredType, runtimeType) is { } allowed)
            {
                return allowed;
            }
        }

        return false;
    }
}
