using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using RandomForeseer.Common;

namespace RandomForeseer.ObjectGraph.Metadata;

/// <summary>Policy-independent metadata and conservative readonly-field immutability proof.</summary>
/// <remarks>Never calls getters. Polymorphic field storage remains unproven.</remarks>
internal static partial class GraphTypeTraits
{
    private static readonly ConcurrentDictionary<(Type Declared, Type Runtime), bool> IdentityKeyTypes = new();

    private static readonly HashSet<Type> OpaqueTypes =
    [
        typeof(nint), typeof(nuint),

        typeof(ConditionalWeakTable<,>),
        typeof(WeakReference<>),
        typeof(Lazy<>),

        typeof(Godot.Variant),
        typeof(Godot.Callable),
        typeof(Godot.Rid),
        typeof(Godot.Signal),
        typeof(Godot.SignalAwaiter),
        typeof(Godot.Collections.Array<>),
        typeof(Godot.Collections.Dictionary<,>)
    ];

    private static readonly Type[] OpaqueBaseTypes =
    [
        typeof(IDisposable), typeof(IAsyncDisposable),
        typeof(WeakReference),
        typeof(Godot.GodotObject)
    ];

    private static readonly string[] OpaqueNamespaces =
    [
        "System.Threading",
        "System.Runtime.InteropServices",
        "System.Reflection"
    ];

    /// <summary>Whether this storage requires access outside the copier's ordinary generic type parameters.</summary>
    public static bool RequiresNonGenericAccess(Type type) =>
        type.IsPointer || type.IsFunctionPointer || type.IsByRef || type.IsByRefLike;

    /// <summary>Whether a type or one of its base types matches the given generic type definition.</summary>
    public static bool IsOrDerivesFromGeneric(Type type, Type genericDefinition)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericDefinition)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether ordinary instance fields cannot establish the type's complete state or lifecycle.</summary>
    public static bool IsOpaque(Type type)
    {
        if (RequiresNonGenericAccess(type))
        {
            return true;
        }

        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (OpaqueTypes.Contains(definition) ||
            OpaqueBaseTypes.Any(baseType => baseType.IsAssignableFrom(type)))
        {
            return true;
        }

        if (type.Namespace is { } ns &&
            OpaqueNamespaces.Any(prefix => ns.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        if (type.IsClass &&
            type.GetMethod("Finalize", ReflectionFlags.NonPublicInstance)?.DeclaringType != typeof(object))
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
