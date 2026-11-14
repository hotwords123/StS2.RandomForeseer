using System.Collections.Concurrent;
using System.Reflection;
using RandomForeseer.Common;

namespace RandomForeseer.ObjectGraph.Metadata;

/// <summary>Policy-independent metadata and conservative readonly-field immutability proof.</summary>
/// <remarks>Never calls getters. Polymorphic field storage remains unproven.</remarks>
internal static partial class GraphTypeTraits
{
    private static readonly ConcurrentDictionary<(Type Declared, Type Runtime), bool> IdentityKeyTypes = new();

    /// <summary>Whether ordinary instance fields cannot establish the type's complete state or lifecycle.</summary>
    public static bool IsOpaque(Type type)
    {
        if (type.IsByRefLike || type.IsByRef || type.IsPointer || type.IsFunctionPointer ||
            type == typeof(nint) || type == typeof(nuint))
        {
            return true;
        }

        if (typeof(IDisposable).IsAssignableFrom(type) || typeof(IAsyncDisposable).IsAssignableFrom(type))
        {
            return true;
        }

        if (type.IsClass &&
            type.GetMethod("Finalize", ReflectionFlags.NonPublicInstance)?.DeclaringType != typeof(object))
        {
            return true;
        }

        if (type.Namespace is { } ns &&
            (ns.StartsWith("System.Threading", StringComparison.Ordinal) ||
             ns.StartsWith("System.Runtime.InteropServices", StringComparison.Ordinal) ||
             ns.StartsWith("System.Reflection", StringComparison.Ordinal)))
        {
            return true;
        }

        if (typeof(WeakReference).IsAssignableFrom(type) ||
            type.IsGenericType && type.GetGenericTypeDefinition() is { } definition &&
            (definition == typeof(WeakReference<>) || definition == typeof(Lazy<>)))
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

    /// <summary>Shares CLR metadata identities without traversing runtime caches, handles or loader state.</summary>
    public static bool IsRuntimeMetadata(Type type)
    {
        // Restrict the exception to CoreCLR implementations. Emit builders and user-defined reflection
        // wrappers can carry mutable state; inheriting a metadata API alone does not grant sharing.
        return type is { IsAbstract: false, Namespace: "System.Reflection" } &&
               type.Assembly == typeof(Type).Assembly &&
               !typeof(TypeDelegator).IsAssignableFrom(type) &&
               (typeof(MemberInfo).IsAssignableFrom(type) ||
                typeof(ParameterInfo).IsAssignableFrom(type) ||
                typeof(Assembly).IsAssignableFrom(type) ||
                typeof(Module).IsAssignableFrom(type));
    }
}
