using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>Describes which interaction owns a combat prediction session's presentation.</summary>
internal enum CombatPredictionSessionMode
{
    /// <summary>Allows automatic target resolution and exposes HoverTips through the model's ordinary hover UI.</summary>
    Hover = 1,

    /// <summary>Owns an initiated action; source adapters decide which action-specific HoverTip surfaces to expose.</summary>
    Action = 2
}

/// <summary>
/// Owns the common prediction, target-observation, and global-projection lifecycle for one combat source.
/// </summary>
/// <remarks>
/// Source-specific adapters remain responsible for holder identity, ordinary HoverTip integration, explicit HoverTip
/// placement, and asynchronous game lifecycle cleanup. Disposing an old session cannot clear projection owned by a
/// newer session.
/// </remarks>
internal abstract class CombatPredictionSession(CombatPredictionSessionMode mode) : IDisposable
{
    private CombatPredictionTargetObserver? _targetObserver;
    private bool _disposed;

    /// <summary>The exact original card or potion identity simulated by this session.</summary>
    public abstract AbstractModel Source { get; }

    /// <summary>The interaction that owns this session's presentation.</summary>
    public CombatPredictionSessionMode Mode { get; } = mode;

    /// <summary>Whether this action session has entered explicit target selection.</summary>
    public bool IsTargeting => _targetObserver is not null;

    /// <summary>The explicit targeting selection, or <see langword="null"/> outside a selected targeting state.</summary>
    public Creature? Target { get; private set; }

    /// <summary>The most recently completed projection, or <see langword="null"/> when none should be presented.</summary>
    public CombatPredictionProjection? Projection { get; private set; }

    /// <summary>
    /// Raised after the projection and shared global projection have been updated.
    /// </summary>
    /// <remarks>Adapters use this only for source-specific UI such as explicitly positioned targeting HoverTips.</remarks>
    public event Action? ProjectionChanged;

    /// <summary>
    /// Raised immediately before the target manager resolves a finished or canceled targeting result.
    /// </summary>
    public event Action? TargetingFinishing;

    /// <summary>Runs an untargeted prediction, allowing the source facade to resolve a unique target automatically.</summary>
    public void RefreshUntargeted()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsTargeting)
        {
            throw new InvalidOperationException("A targeting session cannot use automatic target resolution.");
        }

        StopObservingTargets();
        Target = null;
        RefreshProjection();
    }

    /// <summary>
    /// Enters explicit targeting, clears any automatically resolved projection, and observes actual target changes.
    /// </summary>
    /// <remarks>
    /// Callers must invoke this before the target manager synchronously reports already-focused targets.
    /// A missing or unhovered target clears the projection instead of falling back to automatic target resolution.
    /// </remarks>
    public void BeginTargeting(NTargetManager targetManager)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Mode != CombatPredictionSessionMode.Action || IsTargeting)
        {
            throw new InvalidOperationException("Only a non-targeting action session can begin targeting.");
        }

        Target = null;
        SetProjection(null);

        _targetObserver = new(targetManager);
        _targetObserver.TargetChanged += OnTargetChanged;
        _targetObserver.TargetingFinishing += OnTargetingFinishing;
    }

    /// <summary>Stops target observation and releases this session's shared projection ownership.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopObservingTargets();
        CombatPredictionProjectionController.Release(this);
        ProjectionChanged = null;
    }

    /// <summary>Runs the source-specific facade for the supplied explicit or automatically resolved target.</summary>
    protected abstract CombatPredictionProjection? Predict(Creature? target);

    private void OnTargetChanged(Creature? target)
    {
        Target = target;
        if (target is null)
        {
            SetProjection(null);
        }
        else
        {
            RefreshProjection();
        }
    }

    private void OnTargetingFinishing()
    {
        StopObservingTargets();
        TargetingFinishing?.Invoke();
    }

    private void RefreshProjection()
    {
        CombatPredictionProjection? projection;
        try
        {
            projection = Predict(Target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat prediction failed for {Source.Id} targeting {Target?.Name}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_prediction_session",
                "refresh_projection",
                new
                {
                    Source = TelemetryContext.ForModel(Source),
                    Target = Target is null ? null : TelemetryContext.ForCreature(Target)
                });
            projection = null;
        }

        SetProjection(projection);
    }

    private void SetProjection(CombatPredictionProjection? projection)
    {
        Projection = projection;
        CombatPredictionProjectionController.Set(this, projection);
        ProjectionChanged?.Invoke();
    }

    private void StopObservingTargets()
    {
        if (_targetObserver is null)
        {
            return;
        }

        _targetObserver.Dispose();
        _targetObserver = null;
    }
}
