using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Copiers;
using RandomForeseer.ObjectGraph.Metadata;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.Tests.ObjectGraph.Infrastructure;

/// <summary>Checks field/array copies independently of execution plans, including omitted fields and identity mappings.</summary>
/// <remarks>
/// Reads ordinary fields, arrays and public collection contents; supports Keep and transparent nullable storage.
/// Nullable presence is checked before matching the underlying value's policy, independently of copier dispatch.
/// Hash adapters also have dedicated lookup tests. Other custom adapters require their own semantic assertions and are explicitly rejected by this checker.
/// Policy selectors must obey their no-side-effects contract. Shared boundaries stop traversal and are reported.
/// </remarks>
internal static class ObjectGraphAssert
{
    internal sealed record Boundary(string Path, string RuleId);

    public static IReadOnlyList<Boundary> Equivalent<T>(in T source, in T clone,
        PredictionObjectScope scope, GraphCopyPolicy policy, object? context = null)
    {
        var comparison = new Comparison(scope, policy, context);
        comparison.Storage(typeof(T), source, clone, "$root");
        return comparison.Boundaries;
    }

    internal static void Check(bool condition, string path, string message) => Assert.True(condition, $"{path}: {message}");

    internal static void Retained(Type type, object? source, object? clone, string path)
    {
        if (!type.IsValueType)
        {
            Check(ReferenceEquals(source, clone), path, "retained reference changed");
            return;
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            Check((source == null) == (clone == null), path, "nullable state changed");
            if (source != null)
            {
                Retained(underlying, source, clone, path + ".Value");
            }

            return;
        }

        if (type.IsPrimitive || type.IsEnum)
        {
            Check(Equals(source, clone), path, "primitive or enum value changed");
            return;
        }

        foreach (var field in GraphTypeLayout.For(type).Fields)
        {
            Retained(field.FieldType, field.GetValue(source), field.GetValue(clone), path + "." + field.Name);
        }
    }

    internal static bool CopiesFields(IGraphReferenceCopier? copier) => copier is FieldsGraphReferenceCopier;

    private sealed class Comparison(PredictionObjectScope scope, GraphCopyPolicy policy, object? context)
    {
        // Resolve selectors with the capture's policy/context; this checker never asks the copier to copy anything.
        private readonly PredictionGraphCopier _selectorCopier = new(scope, policy, context);
        private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);
        public List<Boundary> Boundaries { get; } = [];

        public void Storage(Type type, object? source, object? clone, string path)
        {
            if (type.IsValueType)
            {
                Value(type, source, clone, path);
                return;
            }

            Check((source == null) == (clone == null), path, "null reference changed");
            if (source == null)
            {
                return;
            }

            Check(source.GetType() == clone!.GetType(), path, "runtime type changed");
            var match = policy.GetPlan(source.GetType()).ReferenceRule;
            var rule = match.Strategy;
            var action = rule.Select(new GraphReferenceSelection(source, _selectorCopier));
            if (action == GraphReferenceAction.Share)
            {
                Check(ReferenceEquals(source, clone), path, "shared identity changed");
                Boundaries.Add(new(path, match.Id));
                return;
            }

            Check(action == GraphReferenceAction.Clone, path, "policy rejects this reference");
            Check(!ReferenceEquals(source, clone), path, "mutable source reference leaked into clone");
            Check(scope.TryGetClone(source, out var mapped) && ReferenceEquals(mapped, clone), path, "source-to-clone alias mismatch");
            Check(scope.TryGetSource(clone, out var original) && ReferenceEquals(original, source), path, "clone-to-source identity mismatch");
            if (!_visited.Add(source))
            {
                return;
            }

            if (source is Array array && rule.Copier?.GetType() ==
                typeof(GraphArrayCopier<>).MakeGenericType(source.GetType().GetElementType()!))
            {
                ArrayContents(array, (Array)clone, path);
                return;
            }

            if (GraphCollectionSnapshot.Supports(rule.Copier))
            {
                CollectionContents(source, clone, path);
                return;
            }

            if (rule.Copier is GraphDelegateCopier)
            {
                DelegateContents((Delegate)source, (Delegate)clone, path);
                return;
            }

            Check(CopiesFields(rule.Copier), path, "custom reference adapter needs semantic verification");
            if (source.GetType().IsValueType)
            {
                Value(source.GetType(), source, clone, path);
            }
            else
            {
                Fields(source.GetType(), source, clone, path);
            }
        }

        private void DelegateContents(Delegate source, Delegate clone, string path)
        {
            var original = source.GetInvocationList();
            var copied = clone.GetInvocationList();
            Check(original.Length == copied.Length, path, "delegate invocation count changed");
            if (original.Length == 1)
            {
                Check(source.Method == clone.Method, path, "delegate method changed");
                Storage(typeof(object), source.Target, clone.Target, path + ".Target");
                return;
            }

            for (var index = 0; index < original.Length; index++)
            {
                Storage(typeof(Delegate), original[index], copied[index], $"{path}.InvocationList[{index}]");
            }
        }

        private void Value(Type type, object? source, object? clone, string path)
        {
            if (Nullable.GetUnderlyingType(type) is { } underlying)
            {
                Check((source == null) == (clone == null), path, "nullable state changed");
                if (source != null)
                {
                    Value(underlying, source, clone, path + ".Value");
                }

                return;
            }

            var decision = policy.GetPlan(type).ValueRule;
            switch (decision.Strategy)
            {
                case GraphValueAction.Keep:
                    Retained(type, source, clone, path);
                    if (!type.IsPrimitive && !type.IsEnum)
                    {
                        Boundaries.Add(new(path, decision.Id));
                    }

                    break;
                case GraphValueAction.Fields:
                    Fields(type, source!, clone!, path);
                    break;
                default:
                    Check(false, path, $"value rejected by {decision.Id}");
                    break;
            }
        }

        private void Fields(Type ownerType, object source, object clone, string path)
        {
            foreach (var field in GraphTypeLayout.For(ownerType).Fields)
            {
                var edge = $"{path}.{field.DeclaringType!.Name}.{field.Name}";
                var original = field.GetValue(source);
                var copied = field.GetValue(clone);
                var match = policy.GetFieldRule(field);
                switch (match.Strategy)
                {
                    case GraphFieldAction.Copy:
                        Storage(field.FieldType, original, copied, edge);
                        break;
                    case GraphFieldAction.Keep:
                        Retained(field.FieldType, original, copied, edge);
                        Boundaries.Add(new(edge, match.Id));
                        break;
                    case GraphFieldAction.Reset:
                        // Array storage gives default(T) without invoking a user-defined struct constructor.
                        Retained(field.FieldType, Array.CreateInstance(field.FieldType, 1).GetValue(0), copied, edge);
                        Boundaries.Add(new(edge, match.Id));
                        break;
                    default:
                        Check(false, edge, "unknown field rule");
                        break;
                }
            }
        }

        private void CollectionContents(object source, object clone, string path)
        {
            var original = GraphCollectionSnapshot.Read(source);
            var copied = GraphCollectionSnapshot.Read(clone);
            Check(original.Items.Length == copied.Items.Length, path, "collection count changed");
            Check(ReferenceEquals(original.Comparer, copied.Comparer), path, "collection comparer changed");
            if (original.Comparer != null)
            {
                Boundaries.Add(new(path + ".Comparer", "Reviewed immutable collection comparer"));
            }

            for (var index = 0; index < original.Items.Length; index++)
            {
                var target = index;
                if (original.Comparer != null)
                {
                    var keyType = original.KeyType ?? original.ItemType;
                    var key = (original.Keys ?? original.Items)[index];
                    var expected = !keyType.IsValueType && key != null && scope.TryGetClone(key, out var mapped)
                        ? mapped : key;
                    target = Array.FindIndex(copied.Keys ?? copied.Items, candidate => keyType.IsValueType
                        ? Equals(expected, candidate) : ReferenceEquals(expected, candidate));
                    Check(target >= 0, path, "copied collection key missing");
                }

                if (original.Keys != null)
                {
                    Storage(original.KeyType!, original.Keys[index], copied.Keys![target], $"{path}[{index}].Key");
                }

                Storage(original.ItemType, original.Items[index], copied.Items[target], $"{path}[{index}]");
            }
        }

        private void ArrayContents(Array source, Array clone, string path)
        {
            for (var dimension = 0; dimension < source.Rank; dimension++)
            {
                Check(source.GetLength(dimension) == clone.GetLength(dimension) &&
                    source.GetLowerBound(dimension) == clone.GetLowerBound(dimension), path, "array shape changed");
            }

            var copied = clone.GetEnumerator();
            var index = 0;
            foreach (var original in source)
            {
                Check(copied.MoveNext(), path, "array element missing");
                Storage(source.GetType().GetElementType()!, original, copied.Current, $"{path}[{index++}]");
            }
        }
    }
}
