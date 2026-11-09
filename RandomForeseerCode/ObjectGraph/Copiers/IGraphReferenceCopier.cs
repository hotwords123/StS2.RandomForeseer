namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Copies an identity-bearing node, including a real boxed struct.</summary>
/// <remarks>
/// Use for special allocation, containers or boxed values. Source is non-null and must remain unchanged.
/// Adapters own their contents; field policies are not applied automatically. Do not retain the execution copier.
/// Propagate allocation and population failures to the root entry, which invalidates the scope.
/// </remarks>
internal interface IGraphReferenceCopier
{
    /// <summary>Creates a fresh, non-null object of the source's exact runtime type, without recursively copying children.</summary>
    /// <remarks>The framework registers the returned identity before calling Populate.</remarks>
    object Allocate(object source);

    /// <summary>Copies contents into the registered destination using the copier for recursive children.</summary>
    /// <remarks>
    /// Keep the destination identity. Recursive results may still be unfinished; do not run business methods on them.
    /// Hash adapters must establish indexes here and require stable key hash/equality even for unfinished cycle results.
    /// Use copier.CopyChild for synchronous DFS recursion; copier.Copy is reserved for roots.
    /// The copier's path describes the current DFS position; capture diagnostics before leaving a path scope.
    /// </remarks>
    void Populate(object source, object clone, PredictionGraphCopier copier);
}
