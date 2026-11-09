namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Validates known comparers and delegates default-comparer key decisions to the current policy.</summary>
internal static class GraphKeyComparer<T>
{
    public static void ValidateComparer(IEqualityComparer<T> comparer, PredictionGraphCopier copier)
    {
        if (ReferenceEquals(comparer, EqualityComparer<T>.Default) ||
            ReferenceEquals(comparer, ReferenceEqualityComparer.Instance) && !typeof(T).IsValueType ||
            typeof(T) == typeof(string) && (ReferenceEquals(comparer, StringComparer.Ordinal) ||
                ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)))
        {
            return;
        }

        throw copier.Unsupported($"Unsupported comparer for {typeof(T)}.");
    }

    /// <summary>Checks the key before hashing, using the already validated comparer.</summary>
    /// <remarks>
    /// Call ValidateComparer first; only default comparer keys need policy type approval.
    /// The caller must enter the element's index path before validation so rejection captures that position.
    /// </remarks>
    public static void ValidateKey(T key, IEqualityComparer<T> comparer, PredictionGraphCopier copier)
    {
        if (key is null || ReferenceEquals(comparer, ReferenceEqualityComparer.Instance) ||
            typeof(T) == typeof(string) && (ReferenceEquals(comparer, StringComparer.Ordinal) ||
                ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)))
        {
            return;
        }

        var type = key.GetType();
        if (!copier.Policy.IsSupportedKeyType(typeof(T), type))
        {
            // A back-reference can reach this collection before the cloned key's fields are populated.
            // Even ordinary value equality can then insert the key under a hash that later changes.
            throw copier.Unsupported($"Key {type} needs stable hash/equality while its clone is populated; use an identity comparer or a dedicated copy rule.");
        }
    }
}
