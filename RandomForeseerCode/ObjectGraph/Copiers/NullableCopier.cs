using System.Reflection;
using RandomForeseer.Common;

namespace RandomForeseer.ObjectGraph.Copiers;

internal static class NullableCopier
{
    internal static MethodInfo CopyMethod =
        typeof(NullableCopier).GetMethod(nameof(Copy), ReflectionFlags.NonPublicStatic)!;

    /// <summary>Preserves nullable presence and copies a present underlying value without creating a graph identity.</summary>
    private static TValue? Copy<TValue>(PredictionGraphCopier copier, in TValue? source)
        where TValue : struct
    {
        return source is { } value ? copier.CopyChild(in value, "Value") : null;
    }
}

/// <summary>Type-only nullable metadata and a stateless typed bridge, shared across copiers and policies.</summary>
/// <remarks>The bridge is bound once per storage type; it never captures a policy, scope or execution copier.</remarks>
internal static class NullableCopier<T>
{
    /// <summary>Copies exact nullable storage through the explicitly supplied active copier.</summary>
    internal delegate T CopyDelegate(PredictionGraphCopier copier, in T source);

    /// <summary>A cached bridge for nullable storage, otherwise null; binding never reads a policy.</summary>
    internal static readonly CopyDelegate? Copy = Nullable.GetUnderlyingType(typeof(T)) is { } underlyingType
        ? NullableCopier.CopyMethod.MakeGenericMethod(underlyingType).CreateDelegate<CopyDelegate>()
        : null;
}
