using System.Runtime.CompilerServices;

namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Copies an object's fields without constructors, getters or model clone hooks.</summary>
internal static class ShallowCopier
{
    /// <summary>Creates a same-runtime-type copy, retaining all referenced objects and source self-references.</summary>
    /// <remarks>
    /// Does not apply model semantics or register/remap graph identities. Only mutate the copy when sharing is
    /// acceptable or its references have been remapped. Native resources and model getters require separate review.
    /// </remarks>
    public static T Copy<T>(T source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        return (T)MemberwiseClone(source);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
    private static extern object MemberwiseClone(object source);
}
