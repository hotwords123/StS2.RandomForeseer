using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal sealed class PredictionSourceStack
{
    private readonly Stack<AbstractModel> _models = [];

    public AbstractModel? Current => _models.TryPeek(out var model) ? model : null;

    public IEnumerable<AbstractModel> CurrentChain => _models.Reverse();

    public IDisposable Push(AbstractModel model)
    {
        _models.Push(model);
        return new SourceScope(this, model);
    }

    private void PopSource(AbstractModel model)
    {
        if (!_models.TryPop(out var popped) || !ReferenceEquals(popped, model))
        {
            throw new InvalidOperationException("Prediction source stack is unbalanced.");
        }
    }

    private sealed class SourceScope(PredictionSourceStack stack, AbstractModel model) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            stack.PopSource(model);
            _disposed = true;
        }
    }
}
