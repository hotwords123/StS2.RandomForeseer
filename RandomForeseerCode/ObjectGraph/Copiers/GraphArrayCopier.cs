using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Binds array handlers to their runtime element type, including rectangular and non-zero-bound arrays.</summary>
internal static class GraphArrayCopier
{
    /// <summary>Creates a handler for a closed array type; the argument must be an array with a supported element type.</summary>
    public static IGraphReferenceCopier Create(Type arrayType) =>
        (IGraphReferenceCopier)Activator.CreateInstance(
            typeof(GraphArrayCopier<>).MakeGenericType(arrayType.GetElementType()!))!;
}

/// <summary>Preserves shape and array identity, and copies slots according to the exact runtime element type.</summary>
internal sealed class GraphArrayCopier<T> : IGraphReferenceCopier
{
    /// <summary>Shallow-copies the source array, preserving runtime type, lengths and lower bounds.</summary>
    public object Allocate(object source) => ((Array)source).Clone();

    /// <summary>Copies elements into the registered array by exact storage type; diagnostics use flat zero-based indices.</summary>
    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        ReadOnlySpan<T> input = AsSpan((Array)source);
        var output = AsSpan((Array)clone);

        if (input.IsEmpty || copier.Policy.GetPlan<T>().UseShallowCopy)
        {
            return;
        }

        for (var i = 0; i < input.Length; i++)
        {
            output[i] = copier.CopyChild(in input[i], i);
        }
    }

    /// <summary>Exposes contiguous array storage only when its exact element type matches T; empty arrays are not dereferenced.</summary>
    private static Span<T> AsSpan(Array array)
    {
        if (array.GetType().GetElementType() != typeof(T))
        {
            throw new InvalidOperationException($"Array {array.GetType()} does not have exact element type {typeof(T)}.");
        }

        if (array.Length == 0)
        {
            return Span<T>.Empty;
        }

        ref var data = ref MemoryMarshal.GetArrayDataReference(array);
        return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref data), array.Length);
    }
}
