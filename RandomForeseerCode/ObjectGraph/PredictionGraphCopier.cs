using System.Text;
using RandomForeseer.ObjectGraph.Copiers;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.ObjectGraph;

/// <summary>Copies reference and value roots with one identity scope and policy.</summary>
/// <remarks>
/// Roots return only after all nodes are populated. Keep source, policy and caller context stable across roots;
/// finish capture before mutating or running the copied graph. Any failure invalidates the entire scope.
/// Traversal is synchronous, single-threaded DFS. Handlers recurse through CopyChild, never the root Copy entry,
/// and must not retain the copier. Recursive results may be unfinished identities: store them without inspecting
/// business state or publishing them. Diagnostic paths describe only the current DFS position.
/// </remarks>
internal sealed class PredictionGraphCopier
{
    private readonly List<(string? Edge, int Index)> _segments = [];
    private string? _rootPath;

    internal PredictionObjectScope Scope { get; }
    internal GraphCopyPolicy Policy { get; }
    internal object? Context { get; }

    /// <summary>Creates a copier for a scope and stable policy, using cached emitted field executors.</summary>
    /// <remarks>
    /// Use one copier per scope; a stable policy can reuse bound plans across scopes with different contexts.
    /// The caller owns the scope and must keep the optional context data unchanged for this copier's lifetime.
    /// </remarks>
    public PredictionGraphCopier(PredictionObjectScope scope, GraphCopyPolicy policy, object? context = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(policy);
        Scope = scope;
        Policy = policy;
        Context = context;
    }

    /// <summary>Formats the current DFS path only when requested, normally when constructing an exception.</summary>
    public string Path
    {
        get
        {
            var path = new StringBuilder(_rootPath ?? throw new InvalidOperationException("No active copy operation."));
            foreach (var (edge, index) in _segments)
            {
                if (edge != null)
                {
                    path.Append('.').Append(edge);
                }
                else
                {
                    path.Append('[').Append(index).Append(']');
                }
            }

            return path.ToString();
        }
    }

    /// <summary>Copies and populates a reachable root graph before returning; reuses prior copies in this scope.</summary>
    /// <typeparam name="T">The root's declared type, including nullable reference and nullable value types.</typeparam>
    /// <param name="source">The original value; null passes through and present nullable values follow the underlying type's rules.</param>
    /// <param name="path">The root label used in diagnostics.</param>
    /// <returns>The copied value or a reference explicitly shared by policy.</returns>
    /// <remarks>
    /// Call synchronously against stable source state, outside any active copy operation.
    /// Non-null references stay non-null on success; field nullability declarations are not validated.
    /// Handlers must recurse through CopyChild and propagate failures. Copy failure invalidates the scope.
    /// </remarks>
    public T Copy<T>(in T source, string path = "$root")
    {
        Scope.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(path);
        if (_rootPath != null)
        {
            throw new InvalidOperationException("Handlers must recurse through CopyChild, not the root Copy entry.");
        }

        _rootPath = path;
        try
        {
            return CopyChild(in source);
        }
        catch
        {
            Scope.Dispose();
            throw;
        }
        finally
        {
            _segments.Clear();
            _rootPath = null;
        }
    }

    /// <summary>Copies a recursive value by its declared type within an active population phase.</summary>
    /// <returns>The copied value or a reference explicitly shared by policy.</returns>
    /// <remarks>
    /// Nullable storage is transparent: empty values pass through, present values recurse by their underlying declared type.
    /// If cycles are present, the result may be an object that is still being populated.
    /// </remarks>
    public T CopyChild<T>(in T source)
    {
        if (!typeof(T).IsValueType)
        {
            return (T)CopyReference(source)!;
        }

        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return NullableCopier<T>.Copy!(this, in source);
        }

        return Policy.GetPlan<T>().CopyValue(in source, this);
    }

    /// <summary>
    /// See <see cref="CopyChild{T}(in T)"/>. This overload enters a named child path for diagnostics.
    /// </summary>
    public T CopyChild<T>(in T source, string edge)
    {
        using (EnterEdge(edge))
        {
            return CopyChild(in source);
        }
    }

    /// <summary>
    /// See <see cref="CopyChild{T}(in T)"/>. This overload enters an element path for diagnostics.
    /// </summary>
    public T CopyChild<T>(in T source, int index)
    {
        using (EnterIndex(index))
        {
            return CopyChild(in source);
        }
    }

    /// <summary>Resolves a non-null object's action using this copier's policy and caller context without copying it.</summary>
    /// <remarks>
    /// May bind a type plan, but neither reads nor writes scope mappings and does not require an active copy operation.
    /// Each selector receives a fresh selection for this source. Queries must not form a recursive cycle of decisions.
    /// </remarks>
    public GraphReferenceAction GetReferenceAction(object source)
    {
        var (_, strategy) = Policy.GetPlan(source.GetType()).ReferenceRule;
        return strategy.Select(new GraphReferenceSelection(source, this));
    }

    /// <summary>Enters a named child path for a synchronous block of validation or copying.</summary>
    internal PathScope EnterEdge(string edge)
    {
        _segments.Add((edge, 0));
        return new PathScope(this);
    }

    /// <summary>Enters an element path covering validation, recursive copying and insertion diagnostics.</summary>
    internal PathScope EnterIndex(int index)
    {
        _segments.Add((null, index));
        return new PathScope(this);
    }

    /// <summary>Restores the parent DFS path without allocating a per-edge context.</summary>
    /// <remarks>Use only as a local using variable; scopes must be disposed once, in LIFO order.</remarks>
    internal readonly ref struct PathScope(PredictionGraphCopier copier)
    {
        /// <summary>Pops the segment entered by this scope after nested scopes have exited.</summary>
        public void Dispose() => copier._segments.RemoveAt(copier._segments.Count - 1);
    }

    /// <summary>Captures the current path in an unsupported-copy exception before DFS unwinding restores it.</summary>
    internal NotSupportedException Unsupported(string message) => new($"{Path}: {message}");

    /// <summary>Shares or clones an identity-bearing reference, including a real box; null passes through.</summary>
    /// <remarks>Called during population only. Inline-value reflection boxes must use the typed value path instead.</remarks>
    private object? CopyReference(object? source)
    {
        if (source == null)
        {
            return null;
        }

        if (Scope.TryGetClone(source, out var existing))
        {
            return existing;
        }

        var (ruleId, strategy) = Policy.GetPlan(source.GetType()).ReferenceRule;
        var action = strategy.Select(new GraphReferenceSelection(source, this));

        switch (action)
        {
            case GraphReferenceAction.Share:
                return source;

            case GraphReferenceAction.Clone:
                var handler = strategy.Copier!;
                var clone = handler.Allocate(source);
                Scope.Register(source, clone);
                handler.Populate(source, clone, this);
                return clone;

            case GraphReferenceAction.Reject:
                throw Unsupported($"Reference {source.GetType()} is rejected by {ruleId}.");

            default:
                throw new InvalidOperationException($"{Path}: invalid reference action {action}.");
        }
    }

    /// <summary>Applies bound field/value rules to a registered, same-type shallow copy without replacing its identity.</summary>
    /// <remarks>Population phase only. Neither source nor clone may be null; propagate failures to the root entry.</remarks>
    internal void PopulateFields(object source, object clone)
    {
        Policy.GetPlan(source.GetType()).PopulateObject(source, clone, this);
    }
}
