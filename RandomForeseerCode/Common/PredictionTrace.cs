using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common;

internal enum PredictionActionKind
{
    CardPlayLifecycle,
    PotionUse,
    DynamicVariableCalculation
}

internal readonly record struct PredictionInvocation(
    MethodInfo? Method,
    PredictionActionKind? Action)
{
    public static PredictionInvocation ForMethod(MethodInfo method) => new(method, null);

    public static PredictionInvocation ForAction(PredictionActionKind action) => new(null, action);
}

internal sealed class PredictionTraceFrame
{
    public required PredictionTraceFrame? Parent { get; init; }

    public required AbstractModel Source { get; init; }

    public required PredictionInvocation Invocation { get; init; }

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

internal sealed class PredictionTrace
{
    public PredictionTraceFrame? Current { get; private set; }

    public IDisposable Push(AbstractModel source, PredictionInvocation invocation)
    {
        var frame = new PredictionTraceFrame
        {
            Parent = Current,
            Source = source,
            Invocation = invocation
        };
        Current = frame;
        return new TraceScope(this, frame);
    }

    private void Pop(PredictionTraceFrame frame)
    {
        if (!ReferenceEquals(Current, frame))
        {
            throw new InvalidOperationException("Prediction trace scopes are unbalanced.");
        }

        Current = frame.Parent;
    }

    private sealed class TraceScope(PredictionTrace trace, PredictionTraceFrame frame) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            trace.Pop(frame);
            _disposed = true;
        }
    }
}
