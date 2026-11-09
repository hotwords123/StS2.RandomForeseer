namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Builds a dictionary from copied keys/values using the original reviewed comparer.</summary>
/// <remarks>
/// The empty destination is registered before recursion. Identity keys may refer to unfinished clones.
/// Each entry's index path covers key validation and both child copies; Key/Value paths unwind independently.
/// </remarks>
internal sealed class GraphDictionaryCopier<TKey, TValue> : IGraphReferenceCopier where TKey : notnull
{
    public object Allocate(object source)
    {
        var dictionary = (Dictionary<TKey, TValue>)source;
        return new Dictionary<TKey, TValue>(dictionary.Count, dictionary.Comparer);
    }

    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        var dictionary = (Dictionary<TKey, TValue>)source;
        var destination = (Dictionary<TKey, TValue>)clone;
        GraphKeyComparer<TKey>.ValidateComparer(dictionary.Comparer, copier);

        var index = 0;
        foreach (var (key, value) in dictionary)
        {
            using var path = copier.EnterIndex(index++);
            GraphKeyComparer<TKey>.ValidateKey(key, dictionary.Comparer, copier);
            destination.Add(copier.CopyChild(key, "Key"), copier.CopyChild(value, "Value"));
        }
    }
}
