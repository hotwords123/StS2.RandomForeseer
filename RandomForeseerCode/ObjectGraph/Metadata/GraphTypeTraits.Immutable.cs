using System.Collections.Concurrent;

namespace RandomForeseer.ObjectGraph.Metadata;

internal static partial class GraphTypeTraits
{
    private static readonly ConcurrentDictionary<Type, bool> ImmutableTypes = new()
    {
        [typeof(bool)] = true,
        [typeof(char)] = true,
        [typeof(byte)] = true,
        [typeof(sbyte)] = true,
        [typeof(short)] = true,
        [typeof(ushort)] = true,
        [typeof(int)] = true,
        [typeof(uint)] = true,
        [typeof(long)] = true,
        [typeof(ulong)] = true,
        [typeof(float)] = true,
        [typeof(double)] = true,
        [typeof(decimal)] = true,

        [typeof(string)] = true,
        [typeof(object)] = true,

        // Intentionally register System.RuntimeType, the CLR implementation of Type.
        // ReSharper disable once PossibleMistakenCallToGetType
        [typeof(Type).GetType()] = true
    };

    /// <summary>Whether a type's state can be shared without copying.</summary>
    public static bool IsImmutable(Type type)
    {
        if (ImmutableTypes.TryGetValue(type, out var result))
        {
            return result;
        }

        // Discover the dependency graph without publishing provisional conclusions for cycles.
        Dictionary<Type, ImmutableNode> nodes = [];
        Queue<ImmutableNode> unexplored = new();
        var root = GetNode(type);

        while (unexplored.TryDequeue(out var node))
        {
            var candidate = node.Type;
            if (candidate.IsEnum || IsRuntimeMetadata(candidate))
            {
                continue;
            }

            if (Nullable.GetUnderlyingType(candidate) is { } underlying)
            {
                GetNode(underlying).Dependents.Add(node);
                continue;
            }

            if (candidate.IsInterface || candidate.IsAbstract || candidate.IsArray ||
                candidate.ContainsGenericParameters || IsOpaque(candidate))
            {
                node.Rejected = true;
                continue;
            }

            foreach (var field in GraphTypeLayout.For(candidate).Fields)
            {
                if (!field.IsInitOnly || !field.FieldType.IsSealed)
                {
                    node.Rejected = true;
                    break;
                }

                GetNode(field.FieldType).Dependents.Add(node);
            }
        }

        // Any unsafe dependency rejects every type that depends on it, including its entire cycle.
        Queue<ImmutableNode> rejected = new(nodes.Values.Where(node => node.Rejected));
        while (rejected.TryDequeue(out var node))
        {
            foreach (var dependent in node.Dependents)
            {
                if (dependent.Rejected)
                {
                    continue;
                }

                dependent.Rejected = true;
                rejected.Enqueue(dependent);
            }
        }

        // A cycle is shareable only when no failed dependency can reach it. Publishing during
        // discovery would let an unfinished cycle cache success before a later field rejects it.
        foreach (var node in nodes.Values)
        {
            ImmutableTypes.TryAdd(node.Type, !node.Rejected);
        }

        return !root.Rejected;

        ImmutableNode GetNode(Type candidate)
        {
            if (nodes.TryGetValue(candidate, out var existing))
            {
                return existing;
            }

            var node = new ImmutableNode(candidate);
            nodes.Add(candidate, node);
            if (ImmutableTypes.TryGetValue(candidate, out var immutable))
            {
                node.Rejected = !immutable;
            }
            else
            {
                unexplored.Enqueue(node);
            }

            return node;
        }
    }

    private sealed class ImmutableNode(Type type)
    {
        public Type Type { get; } = type;

        public bool Rejected { get; set; }

        public List<ImmutableNode> Dependents { get; } = [];
    }
}
