using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common.Hooks;

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

    public void Add(AbstractModel model)
    {
        HasRisk = true;
        if (_modelIds.Add(model.Id))
        {
            _models.Add(model);
        }
    }

    public void AddHookResults(IEnumerable<HookResult> results)
    {
        foreach (var result in results)
        {
            if (result.IsPredictionRisk)
            {
                Add(result.Model);
            }
        }
    }

    public PredictionRisk Snapshot()
    {
        return HasRisk
            ? new PredictionRisk(true, _models.ToList())
            : PredictionRisk.None;
    }
}
