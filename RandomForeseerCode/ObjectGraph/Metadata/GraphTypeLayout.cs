using System.Collections.Concurrent;
using System.Reflection;
using RandomForeseer.Common;

namespace RandomForeseer.ObjectGraph.Metadata;

/// <summary>Global metadata cache only. Policy bindings and field operations live in each policy's plans.</summary>
internal sealed record GraphTypeLayout(IReadOnlyList<FieldInfo> Fields)
{
    private static readonly ConcurrentDictionary<Type, GraphTypeLayout> Layouts = new();

    /// <summary>Gets the cached derived-to-base instance layout, including private fields; excludes properties and static fields.</summary>
    public static GraphTypeLayout For(Type type) => Layouts.GetOrAdd(type, Build);

    private static GraphTypeLayout Build(Type type)
    {
        List<FieldInfo> fields = [];

        for (var current = type; current != null; current = current.BaseType)
        {
            fields.AddRange(current.GetFields(ReflectionFlags.AllInstance | BindingFlags.DeclaredOnly)
                .OrderBy(field => field.MetadataToken));
        }

        return new GraphTypeLayout(fields);
    }
}
