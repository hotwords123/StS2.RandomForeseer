using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common;

internal sealed record PredictionRisk(bool HasRisk, IReadOnlyList<AbstractModel> Models)
{
    public static PredictionRisk None { get; } = new(false, []);
}

internal readonly record struct PredictionRiskCheckpoint(long Version) : IComparable<PredictionRiskCheckpoint>
{
    public int CompareTo(PredictionRiskCheckpoint other)
    {
        return Version.CompareTo(other.Version);
    }
}

internal sealed class PredictionRiskTracker
{
    private readonly List<RiskModelEntry> _models = [];
    private readonly HashSet<ModelId> _modelIds = [];
    private long? _unknownRiskVersion;
    private long _version;

    public PredictionRiskCheckpoint Checkpoint => new(_version);

    public void AddUnknown()
    {
        _unknownRiskVersion ??= ++_version;
    }

    public void Add(AbstractModel? model)
    {
        if (model is null)
        {
            AddUnknown();
        }
        else if (_modelIds.Add(model.Id))
        {
            _models.Add(new RiskModelEntry(model, ++_version));
        }
    }

    public void AddCurrentSources(PredictionSourceStack sourceStack)
    {
        if (sourceStack.Current is null)
        {
            throw new InvalidOperationException("Cannot add current prediction risk source outside a source scope.");
        }

        var newModels = sourceStack.CurrentChain
            .Where(model => _modelIds.Add(model.Id))
            .ToList();
        if (newModels.Count == 0)
        {
            return;
        }

        var version = ++_version;
        foreach (var model in newModels)
        {
            _models.Add(new RiskModelEntry(model, version));
        }
    }

    public PredictionRisk Snapshot()
    {
        return Snapshot(Checkpoint);
    }

    public PredictionRisk Snapshot(PredictionRiskCheckpoint checkpoint)
    {
        if (checkpoint.Version < 0 || checkpoint.Version > _version)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        var hasUnknownRisk = _unknownRiskVersion <= checkpoint.Version;
        var models = _models
            .TakeWhile(entry => entry.Version <= checkpoint.Version)
            .Select(static entry => entry.Model)
            .ToList();

        return hasUnknownRisk || models.Count > 0
            ? new PredictionRisk(true, models)
            : PredictionRisk.None;
    }

    private sealed record RiskModelEntry(AbstractModel Model, long Version);
}
