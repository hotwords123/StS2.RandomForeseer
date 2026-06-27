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

    public bool HasRisk { get; private set; }

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

    public void AddCurrentSources(PredictionSourceStack sourceStack)
    {
        if (sourceStack.Current == null)
        {
            throw new InvalidOperationException("Cannot add current prediction risk source outside a source scope.");
        }

        foreach (var model in sourceStack.CurrentChain)
        {
            Add(model);
        }
    }

    public PredictionRisk Snapshot()
    {
        return HasRisk
            ? new PredictionRisk(true, _models.ToList())
            : PredictionRisk.None;
    }
}
