namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Builds a set from copied elements, preserving comparer semantics and element identities.</summary>
/// <remarks>
/// Iteration order, capacity and private storage are outside the copy contract.
/// Each element's index path stays active through key validation, child copying and duplicate detection.
/// </remarks>
internal sealed class GraphHashSetCopier<T> : IGraphReferenceCopier
{
    public object Allocate(object source)
    {
        var set = (HashSet<T>)source;
        return new HashSet<T>(set.Count, set.Comparer);
    }

    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        var set = (HashSet<T>)source;
        var destination = (HashSet<T>)clone;
        GraphKeyComparer<T>.ValidateComparer(set.Comparer, copier);

        var index = 0;
        foreach (var item in set)
        {
            using var path = copier.EnterIndex(index++);
            GraphKeyComparer<T>.ValidateKey(item, set.Comparer, copier);
            if (!destination.Add(copier.CopyChild(item)))
            {
                throw copier.Unsupported("Distinct set elements became equal after copying.");
            }
        }
    }
}
