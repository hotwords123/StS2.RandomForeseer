using System.Runtime.CompilerServices;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Preserves delegate bindings while remapping captured objects and multicast entries.</summary>
/// <remarks>Uses the .NET 9 CoreCLR delegate layout; Clone retains method pointers and runtime binding metadata.</remarks>
internal sealed class GraphDelegateCopier : IGraphReferenceCopier
{
    public static GraphDelegateCopier Instance { get; } = new();

    public object Allocate(object source) => ((Delegate)source).Clone();

    public void Populate(object source, object clone, PredictionGraphCopier copier)
    {
        // CoreCLR can store the delegate itself in _target. The already-registered clone resolves this back-reference.
        Target((Delegate)clone) = copier.CopyChild(Target((Delegate)source), "Target");
        InvocationList((MulticastDelegate)clone) =
            copier.CopyChild(InvocationList((MulticastDelegate)source), "InvocationList");
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
}
