using System.Collections.Concurrent;
using System.Numerics;
using RandomForeseer.Common;

namespace RandomForeseer.ObjectGraph.Metadata;

/// <summary>Policy-independent metadata and conservative readonly-field immutability proof.</summary>
/// <remarks>Never calls getters. Recursive types and polymorphic storage remain unproven.</remarks>
internal static class GraphTypeTraits
{
    private static readonly ConcurrentDictionary<Type, bool> ImmutableTypes = new()
    {
        [typeof(bool)] = true,
        [typeof(char)] = true,
        [typeof(byte)] = true,
        [typeof(sbyte)] = true,
        [typeof(short)] = true,
        [typeof(ushort)] = true,
        [typeof(int)] = true,
        [typeof(uint)] = true,
        [typeof(long)] = true,
        [typeof(ulong)] = true,
        [typeof(float)] = true,
        [typeof(double)] = true,
        [typeof(decimal)] = true,
        [typeof(nint)] = true,
        [typeof(nuint)] = true,

        [typeof(Int128)] = true,
        [typeof(UInt128)] = true,
        [typeof(Half)] = true,
        [typeof(Complex)] = true,

        [typeof(string)] = true,
        [typeof(object)] = true
    };

    private static readonly ConcurrentDictionary<(Type Declared, Type Runtime), bool> IdentityKeyTypes = new();

    public static bool IsImmutable(Type type) => AnalyzeImmutable(type);

    /// <summary>Whether ordinary instance fields cannot establish the type's complete state or lifecycle.</summary>
    public static bool IsOpaque(Type type)
    {
        if (type.IsByRefLike || type.IsByRef || type.IsPointer || type.IsFunctionPointer ||
            type == typeof(IntPtr) || type == typeof(UIntPtr))
        {
            return true;
        }

        if (typeof(IDisposable).IsAssignableFrom(type) || typeof(IAsyncDisposable).IsAssignableFrom(type))
        {
            return true;
        }

        if (type.IsClass && type.GetMethod("Finalize", ReflectionFlags.NonPublicInstance)?.DeclaringType != typeof(object))
        {
            return true;
        }

        if (type.Namespace is { } ns && (ns.StartsWith("System.Threading", StringComparison.Ordinal) ||
                                         ns.StartsWith("System.Runtime.InteropServices", StringComparison.Ordinal) ||
                                         ns.StartsWith("System.Reflection", StringComparison.Ordinal)))
        {
            return true;
        }

        if (typeof(WeakReference).IsAssignableFrom(type) ||
            type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(WeakReference<>) ||
                                   type.GetGenericTypeDefinition() == typeof(Lazy<>)))
        {
            return true;
        }

        if (type == typeof(Godot.Variant) || typeof(Godot.GodotObject).IsAssignableFrom(type))
        {
            return true;
        }

        return false;
    }

    public static bool HasIdentityEquality(Type declaredType, Type runtimeType)
    {
        if (IdentityKeyTypes.TryGetValue((declaredType, runtimeType), out var result))
        {
            return result;
        }

        result = runtimeType.IsClass &&
                 !typeof(IEquatable<>).MakeGenericType(declaredType).IsAssignableFrom(declaredType) &&
                 runtimeType.GetMethod(nameof(object.Equals), [typeof(object)])!.DeclaringType == typeof(object) &&
                 runtimeType.GetMethod(nameof(object.GetHashCode), Type.EmptyTypes)!.DeclaringType == typeof(object);
        IdentityKeyTypes.TryAdd((declaredType, runtimeType), result);
        return result;
    }

    private static bool AnalyzeImmutable(Type type, HashSet<Type>? visiting = null)
    {
        if (ImmutableTypes.TryGetValue(type, out var result))
        {
            return result;
        }

        visiting ??= [];
        if (!visiting.Add(type))
        {
            return false;
        }

        try
        {
            result = ProveImmutable(type, visiting);
            ImmutableTypes.TryAdd(type, result);
            return result;
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    private static bool ProveImmutable(Type type, HashSet<Type> visiting)
    {
        if (type.IsEnum)
        {
            return true;
        }

        if (type.IsInterface || type.IsAbstract || type.IsArray || type.ContainsGenericParameters)
        {
            return false;
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return AnalyzeImmutable(underlying, visiting);
        }

        if (IsOpaque(type))
        {
            return false;
        }

        return GraphTypeLayout.For(type).Fields.All(field =>
            field.IsInitOnly && field.FieldType.IsSealed && AnalyzeImmutable(field.FieldType, visiting));
    }
}
