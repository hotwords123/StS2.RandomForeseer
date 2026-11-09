using System.Diagnostics.CodeAnalysis;

namespace RandomForeseer.ObjectGraph;

/// <summary>Maintains the reference-identity correspondence between sources and their actual copies.</summary>
/// <remarks>
/// Queries never copy or populate objects. Shared leaves and inline values are not registered; cloned real boxes are.
/// Use one stable copier/policy per scope. All queries require non-null arguments and fail after disposal.
/// </remarks>
internal sealed class PredictionObjectScope : IDisposable
{
    private readonly Dictionary<object, object> _sourceToClone = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, object> _cloneToSource = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    /// <summary>Registers a fresh same-runtime-type identity before its contents are populated.</summary>
    /// <remarks>Framework only. Both arguments must be non-null and distinct, with no existing source or destination mapping.</remarks>
    internal void Register(object source, object clone)
    {
        if (ReferenceEquals(source, clone))
        {
            throw new ArgumentException("The source and clone cannot be the same instance.");
        }

        if (source.GetType() != clone.GetType())
        {
            throw new ArgumentException("The source and clone must be of the same type.");
        }

        _sourceToClone.Add(source, clone);
        _cloneToSource.Add(clone, source);
    }

    public T GetClone<T>(T source) where T : class
    {
        ThrowIfDisposed();

        return _sourceToClone.TryGetValue(source, out var result)
            ? (T)result
            : throw new InvalidOperationException("The input was not captured by this scope.");
    }

    public T GetSource<T>(T clone) where T : class
    {
        ThrowIfDisposed();

        return _cloneToSource.TryGetValue(clone, out var result)
            ? (T)result
            : throw new InvalidOperationException("The input was not captured by this scope.");
    }

    public bool TryGetClone<T>(T source, [NotNullWhen(true)] out T? clone) where T : class
    {
        ThrowIfDisposed();

        if (_sourceToClone.TryGetValue(source, out var result))
        {
            clone = (T)result;
            return true;
        }

        clone = null;
        return false;
    }

    public bool TryGetSource<T>(T clone, [NotNullWhen(true)] out T? source) where T : class
    {
        ThrowIfDisposed();

        if (_cloneToSource.TryGetValue(clone, out var result))
        {
            source = (T)result;
            return true;
        }

        source = null;
        return false;
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        _disposed = true;
        _sourceToClone.Clear();
        _cloneToSource.Clear();
    }
}
