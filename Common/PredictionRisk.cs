using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal sealed record PredictionRisk(bool HasRisk, IReadOnlyList<AbstractModel> Models)
{
    public static PredictionRisk None { get; } = new(false, []);
}

internal sealed class PredictionRiskTracker
{
    private readonly List<AbstractModel> _models = [];
    private readonly HashSet<ModelId> _modelIds = [];
    private readonly Stack<AbstractModel> _sourceStack = [];

    public bool HasRisk { get; private set; }

    public IDisposable PushSource(AbstractModel model)
    {
        _sourceStack.Push(model);
        return new SourceScope(this, model);
    }

    public void AddCurrentSource()
    {
        if (_sourceStack.Count == 0)
        {
            throw new InvalidOperationException("Cannot add current prediction risk source outside a source scope.");
        }

        foreach (var model in _sourceStack.Reverse())
        {
            Add(model);
        }
    }

    public void AddUnknown()
    {
        HasRisk = true;
    }

    public void Add(AbstractModel? model)
    {
        HasRisk = true;
        if (model != null && _modelIds.Add(model.Id))
        {
            _models.Add(model);
        }
    }

    public PredictionRisk Snapshot()
    {
        return HasRisk
            ? new PredictionRisk(true, _models.ToList())
            : PredictionRisk.None;
    }

    private void PopSource(AbstractModel model)
    {
        if (!_sourceStack.TryPop(out var popped) || !ReferenceEquals(popped, model))
        {
            throw new InvalidOperationException("Prediction risk source stack is unbalanced.");
        }
    }

    private sealed class SourceScope(PredictionRiskTracker tracker, AbstractModel model) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            tracker.PopSource(model);
            _disposed = true;
        }
    }
}
