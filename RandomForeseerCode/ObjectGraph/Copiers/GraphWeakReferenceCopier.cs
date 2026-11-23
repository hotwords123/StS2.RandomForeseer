using System.Runtime.Serialization;

namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Creates an independent weak reference with the original resurrection tracking behavior.</summary>
internal sealed class GraphWeakReferenceCopier : IGraphReferenceCopier
{
    public static GraphWeakReferenceCopier Instance { get; } = new();

    public object Allocate(object source)
    {
        var reference = (WeakReference)source;
        return new WeakReference(null, reference.TrackResurrection);
    }

    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        var reference = (WeakReference)source;
        var destination = (WeakReference)clone;
        destination.Target = copier.CopyChild(reference.Target, "Target");
    }
}

/// <inheritdoc cref="GraphWeakReferenceCopier"/>
internal sealed class GraphWeakReferenceCopier<T> : IGraphReferenceCopier where T : class
{
    public object Allocate(object source)
    {
        var reference = (WeakReference<T>)source;

        // WeakReference<T> exposes the tracking flag through serialization metadata.
#pragma warning disable SYSLIB0050, SYSLIB0051
        var data = new SerializationInfo(typeof(WeakReference<T>), new FormatterConverter());
        reference.GetObjectData(data, default);
#pragma warning restore SYSLIB0050, SYSLIB0051

        return new WeakReference<T>(null!, data.GetBoolean("TrackResurrection"));
    }

    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        var reference = (WeakReference<T>)source;
        var destination = (WeakReference<T>)clone;
        if (reference.TryGetTarget(out var target))
        {
            destination.SetTarget(copier.CopyChild(target, "Target"));
        }
    }
}
