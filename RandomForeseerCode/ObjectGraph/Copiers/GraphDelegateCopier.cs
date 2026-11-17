using System.Reflection;
using System.Runtime.CompilerServices;
using RandomForeseer.Common;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Preserves delegate bindings while remapping captured objects and multicast entries.</summary>
/// <remarks>Supports .NET 9 CoreCLR and Mono layouts; Clone retains method pointers and runtime binding metadata.</remarks>
internal sealed class GraphDelegateCopier : IGraphReferenceCopier
{
    private delegate void PopulateDelegate(Delegate source, Delegate clone, PredictionGraphCopier copier);

    private static readonly PopulateDelegate Populator = SelectPopulator();

    public static GraphDelegateCopier Instance { get; } = new();

    public object Allocate(object source) => ((Delegate)source).Clone();

    public void Populate(object source, object clone, PredictionGraphCopier copier)
        => Populator((Delegate)source, (Delegate)clone, copier);

    private static void PopulateCoreClr(Delegate source, Delegate clone, PredictionGraphCopier copier)
    {
        // CoreCLR can store the delegate itself in _target. The already-registered clone resolves this back-reference.
        Target(clone) = copier.CopyChild(Target(source), "Target");
        InvocationList((MulticastDelegate)clone) =
            copier.CopyChild(InvocationList((MulticastDelegate)source), "InvocationList");
    }

    private static void PopulateMono(Delegate source, Delegate clone, PredictionGraphCopier copier)
    {
        Target(clone) = copier.CopyChild(Target(source), "Target");
        MonoInvocationList((MulticastDelegate)clone) =
            copier.CopyChild(MonoInvocationList((MulticastDelegate)source), "InvocationList");
    }

    private static PopulateDelegate SelectPopulator()
    {
        // Keep accessors for different layouts in separate methods so the JIT only resolves the active layout.
        if (HasField(typeof(Delegate), "_target", typeof(object)))
        {
            if (HasField(typeof(MulticastDelegate), "_invocationList", typeof(object)))
            {
                return PopulateCoreClr;
            }

            if (HasField(typeof(MulticastDelegate), "delegates", typeof(Delegate[])))
            {
                return PopulateMono;
            }
        }

        return (_, _, copier) => throw copier.Unsupported("Unsupported runtime delegate layout.");

        static bool HasField(Type declaringType, string name, Type fieldType) => declaringType
            .GetField(name, ReflectionFlags.NonPublicInstance | BindingFlags.DeclaredOnly)?.FieldType == fieldType;
    }

    /// <summary>Shares a delegate when every invocation target is null or selected for sharing by the same copier.</summary>
    public static GraphReferenceAction Select(GraphReferenceSelection selection)
    {
        return selection.GetSource<Delegate>().GetInvocationList().All(callback =>
            callback.Target is not { } target || selection.GetReferenceAction(target) == GraphReferenceAction.Share)
            ? GraphReferenceAction.Share
            : GraphReferenceAction.Clone;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_target")]
    private static extern ref object? Target(Delegate source);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_invocationList")]
    private static extern ref object? InvocationList(MulticastDelegate source);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "delegates")]
    private static extern ref Delegate[]? MonoInvocationList(MulticastDelegate source);
}
