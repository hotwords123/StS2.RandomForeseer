using System.Runtime.CompilerServices;

namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Copies entries observable during enumeration into a new weak-key table.</summary>
internal sealed class GraphConditionalWeakTableCopier<TKey, TValue> : IGraphReferenceCopier
    where TKey : class
    where TValue : class?
{
    public object Allocate(object source) => new ConditionalWeakTable<TKey, TValue>();

    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        var table = (ConditionalWeakTable<TKey, TValue>)source;
        var destination = (ConditionalWeakTable<TKey, TValue>)clone;
        var index = 0;

        foreach (var (key, value) in table)
        {
            using var path = copier.EnterIndex(index++);
            destination.Add(copier.CopyChild(key, "Key"), copier.CopyChild(value, "Value"));
        }
    }
}
