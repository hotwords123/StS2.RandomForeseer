using System.Collections;
using RandomForeseer.ObjectGraph.Copiers;

namespace RandomForeseer.Tests.ObjectGraph.Infrastructure;

/// <summary>Reads the logical contents of built-in collection adapters through public BCL APIs.</summary>
internal sealed record GraphCollectionSnapshot(Type ItemType, Type? KeyType, object? Comparer,
    object?[] Items, object?[]? Keys)
{
    public static bool Supports(IGraphReferenceCopier? copier) =>
        copier?.GetType() is { IsGenericType: true } type &&
        (type.GetGenericTypeDefinition() == typeof(GraphListCopier<>) ||
         type.GetGenericTypeDefinition() == typeof(GraphDictionaryCopier<,>) ||
         type.GetGenericTypeDefinition() == typeof(GraphHashSetCopier<>));

    public static GraphCollectionSnapshot Read(object source)
    {
        var type = source.GetType();
        var arguments = type.GenericTypeArguments;
        var comparer = type.GetProperty("Comparer")?.GetValue(source);
        if (source is IDictionary dictionary)
        {
            return new(arguments[1], arguments[0], comparer,
                dictionary.Values.Cast<object?>().ToArray(), dictionary.Keys.Cast<object?>().ToArray());
        }

        return new(arguments[0], null, comparer, ((IEnumerable)source).Cast<object?>().ToArray(), null);
    }
}
