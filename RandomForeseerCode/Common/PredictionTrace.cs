using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Utils;

namespace RandomForeseer.RandomForeseerCode.Common;

/// <summary>Identifies action boundaries that affect prediction scope and causal ownership.</summary>
internal enum PredictionActionKind
{
    /// <summary>A manual or automatic card-play lifecycle.</summary>
    CardPlay,

    /// <summary>A potion-use lifecycle.</summary>
    PotionUse,

    /// <summary>A dynamic-variable calculation.</summary>
    DynamicVariableCalculation
}

/// <summary>Identifies either one mirrored model method or one higher-level prediction action.</summary>
/// <remarks>
/// <see cref="Method"/> and <see cref="Action"/> are mutually exclusive. Callers should use
/// <see cref="ForMethod"/> or <see cref="ForAction"/> so exactly one discriminator is populated.
/// </remarks>
/// <param name="Method">The exact reflected base method represented by this invocation.</param>
/// <param name="Action">The higher-level prediction action represented by this invocation.</param>
internal readonly record struct PredictionInvocation(
    MethodInfo? Method,
    PredictionActionKind? Action)
{
    /// <summary>Creates an invocation for one exact mirrored base method.</summary>
    public static PredictionInvocation ForMethod(MethodInfo method) => new(method, null);

    /// <summary>Creates an invocation for one higher-level prediction action.</summary>
    public static PredictionInvocation ForAction(PredictionActionKind action) => new(null, action);
}

/// <summary>
/// Represents one immutable model-source frame in a prediction trace.
/// </summary>
/// <remarks>
/// Frame reference identity is stable after its active scope is popped and is used for history ownership, causal
/// grouping, and root/nested scope classification. Frames are linked from the current frame toward the root.
/// </remarks>
internal sealed class PredictionTraceFrame
{
    /// <summary>The exact enclosing frame, or <see langword="null"/> for a top-level frame.</summary>
    public required PredictionTraceFrame? Parent { get; init; }

    /// <summary>The exact model identity responsible for this frame.</summary>
    public required AbstractModel Source { get; init; }

    /// <summary>The method or action represented by this frame.</summary>
    public required PredictionInvocation Invocation { get; init; }

    /// <summary>Enumerates this frame followed by each enclosing frame from nearest to farthest.</summary>
    public IEnumerable<PredictionTraceFrame> Ancestors()
    {
        var current = this;
        do
        {
            yield return current;
            current = current.Parent;
        } while (current is not null);
    }
}

/// <summary>Maintains the strictly nested frame stack for one prediction simulation.</summary>
/// <remarks>
/// A trace is mutable only while simulation scopes are active and is not safe for concurrent use. Disposing scopes
/// out of LIFO order is a programming error; popped frame objects remain valid immutable identities for history.
/// </remarks>
internal sealed class PredictionTrace
{
    /// <summary>Gets the active innermost frame, or <see langword="null"/> when no scope is active.</summary>
    public PredictionTraceFrame? Current { get; private set; }

    /// <summary>Pushes one source/invocation frame and returns the scope that pops that exact frame.</summary>
    /// <param name="source">The exact model identity responsible for work performed in the new scope.</param>
    /// <param name="invocation">The method or action that establishes the new scope.</param>
    /// <returns>An idempotent disposable scope that must be disposed in strict LIFO order.</returns>
    public IDisposable Push(AbstractModel source, PredictionInvocation invocation)
    {
        var frame = new PredictionTraceFrame
        {
            Parent = Current,
            Source = source,
            Invocation = invocation
        };
        Current = frame;
        return new DisposableAction(() => Pop(frame));
    }

    private void Pop(PredictionTraceFrame frame)
    {
        if (!ReferenceEquals(Current, frame))
        {
            throw new InvalidOperationException("Prediction trace scopes are unbalanced.");
        }

        Current = frame.Parent;
    }
}
