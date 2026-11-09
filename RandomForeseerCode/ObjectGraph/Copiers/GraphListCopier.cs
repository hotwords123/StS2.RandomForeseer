namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Copies logical list elements in order; internal storage and capacity are not graph identities.</summary>
internal sealed class GraphListCopier<T> : IGraphReferenceCopier
{
    public object Allocate(object source) => new List<T>(((List<T>)source).Count);

    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        var items = (List<T>)source;
        var destination = (List<T>)clone;
        if (items.Count == 0)
        {
            return;
        }

        if (copier.Policy.GetPlan<T>().UseShallowCopy)
        {
            destination.AddRange(items);
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            destination.Add(copier.CopyChild(items[i], i));
        }
    }
}
