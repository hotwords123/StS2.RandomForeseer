using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Copiers;
using RandomForeseer.ObjectGraph.Metadata;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.Tests.ObjectGraph.Infrastructure;

/// <summary>Records source field/array slots before capture and checks them afterwards without invoking business getters.</summary>
/// <remarks>
/// Supports reference roots and the same ordinary field/array and logical collection paths as ObjectGraphAssert.
/// Records boundary slots but does not enter shared/kept/reset references. Custom adapters require separate source checks.
/// Nullable presence is recorded before applying the underlying value policy; wrappers do not have their own action.
/// </remarks>
internal sealed class GraphSourceSnapshot
{
    private readonly List<Action> _checks = [];
    private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);

    private GraphSourceSnapshot() { }

    public static GraphSourceSnapshot Capture<T>(T source, GraphCopyPolicy policy, object? context = null) where T : class
    {
        var snapshot = new GraphSourceSnapshot();
        // Selection queries use the same policy/context but never allocate identities in this temporary scope.
        using var scope = new PredictionObjectScope();
        var selectorCopier = new PredictionGraphCopier(scope, policy, context);
        snapshot.Visit(typeof(T), () => source, "$source", selectorCopier);
        return snapshot;
    }

    public void AssertUnchanged()
    {
        foreach (var check in _checks)
        {
            check();
        }
    }

    private void Visit(Type type, Func<object?> read, string path, PredictionGraphCopier selectorCopier, bool retained = false)
    {
        var policy = selectorCopier.Policy;
        var value = read();
        if (!type.IsValueType)
        {
            _checks.Add(() => ObjectGraphAssert.Check(ReferenceEquals(value, read()), path, "source reference changed"));
            if (value == null || retained)
            {
                return;
            }

            var rule = policy.GetPlan(value.GetType()).ReferenceRule.Strategy;
            var action = rule.Select(new GraphReferenceSelection(value, selectorCopier));
            if (action == GraphReferenceAction.Share)
            {
                return;
            }

            ObjectGraphAssert.Check(action == GraphReferenceAction.Clone, path, "snapshot does not traverse rejected references");
            if (!_visited.Add(value))
            {
                return;
            }

            if (value is Array array && rule.Copier?.GetType() ==
                typeof(GraphArrayCopier<>).MakeGenericType(value.GetType().GetElementType()!))
            {
                var indices = Enumerable.Range(0, array.Rank).Select(array.GetLowerBound).ToArray();
                for (var flat = 0; flat < array.Length; flat++)
                {
                    var slot = indices.ToArray();
                    Visit(value.GetType().GetElementType()!, () => array.GetValue(slot), $"{path}[{flat}]", selectorCopier);
                    for (var dimension = indices.Length - 1; dimension >= 0; dimension--)
                    {
                        if (++indices[dimension] <= array.GetUpperBound(dimension))
                        {
                            break;
                        }

                        indices[dimension] = array.GetLowerBound(dimension);
                    }
                }

                return;
            }

            if (GraphCollectionSnapshot.Supports(rule.Copier))
            {
                var contents = GraphCollectionSnapshot.Read(value);
                var current = contents;
                // Refresh once before checking slots, including inline values. Slot readers share this snapshot.
                _checks.Add(() =>
                {
                    current = GraphCollectionSnapshot.Read(value);
                    ObjectGraphAssert.Check(contents.Items.Length == current.Items.Length, path, "source collection count changed");
                    ObjectGraphAssert.Check(ReferenceEquals(contents.Comparer, current.Comparer), path, "source comparer changed");
                });
                for (var index = 0; index < contents.Items.Length; index++)
                {
                    var slot = index;
                    if (contents.Keys != null)
                    {
                        Visit(contents.KeyType!, () => current.Keys![slot],
                            $"{path}[{slot}].Key", selectorCopier);
                    }

                    Visit(contents.ItemType, () => current.Items[slot],
                        $"{path}[{slot}]", selectorCopier);
                }

                return;
            }

            if (rule.Copier is GraphDelegateCopier)
            {
                var callback = (Delegate)value;
                var entries = callback.GetInvocationList();
                var current = entries;
                _checks.Add(() =>
                {
                    current = callback.GetInvocationList();
                    ObjectGraphAssert.Check(entries.Length == current.Length, path, "source invocation count changed");
                });
                if (entries.Length == 1)
                {
                    var method = callback.Method;
                    _checks.Add(() => ObjectGraphAssert.Check(method == callback.Method, path, "source delegate method changed"));
                    Visit(typeof(object), () => callback.Target, path + ".Target", selectorCopier);
                }
                else
                {
                    for (var index = 0; index < entries.Length; index++)
                    {
                        var slot = index;
                        Visit(typeof(Delegate), () => current[slot], $"{path}.InvocationList[{slot}]", selectorCopier);
                    }
                }

                return;
            }

            ObjectGraphAssert.Check(ObjectGraphAssert.CopiesFields(rule.Copier), path, "custom reference adapter needs a source snapshot assertion");
            type = value.GetType();
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            _checks.Add(() => ObjectGraphAssert.Check((value == null) == (read() == null), path, "source nullable state changed"));
            if (value != null)
            {
                Visit(underlying, read, path + ".Value", selectorCopier, retained);
            }

            return;
        }

        if (type.IsValueType)
        {
            var decision = policy.GetPlan(type).ValueRule;
            retained |= decision.Strategy == GraphValueAction.Keep;
            if (!retained)
            {
                ObjectGraphAssert.Check(decision.Strategy == GraphValueAction.Fields,
                    path, $"snapshot rejects value under {decision.Id}");
            }
        }

        if (type.IsPrimitive || type.IsEnum)
        {
            _checks.Add(() => ObjectGraphAssert.Retained(type, value, read(), path));
            return;
        }

        foreach (var field in GraphTypeLayout.For(type).Fields)
        {
            var keepEdge = retained || policy.GetFieldRule(field).Strategy != GraphFieldAction.Copy;
            Visit(field.FieldType, () => field.GetValue(read()), $"{path}.{field.DeclaringType!.Name}.{field.Name}", selectorCopier, keepEdge);
        }
    }
}
