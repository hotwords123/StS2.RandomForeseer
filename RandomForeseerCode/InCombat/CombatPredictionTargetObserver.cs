using System.Diagnostics.CodeAnalysis;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>Maps targetable UI nodes to the combat creatures they represent.</summary>
internal static class CombatPredictionTargetResolver
{
    /// <summary>
    /// Resolves both an on-field creature node and a multiplayer player-status node to the represented creature.
    /// </summary>
    public static bool TryResolveCreature(Node node, [NotNullWhen(true)] out Creature? creature)
    {
        creature = node switch
        {
            NCreature creatureNode => creatureNode.Entity,
            NMultiplayerPlayerState playerState => playerState.Player.Creature,
            _ => null
        };
        return creature is not null;
    }
}

/// <summary>
/// Normalizes the two target-manager hover signal families into one identity-safe prediction target stream.
/// </summary>
/// <remarks>
/// <see cref="NTargetManager"/> emits creature signals for <see cref="NCreature"/> and node signals for targets such
/// as <see cref="NMultiplayerPlayerState"/>. Consumers must therefore observe both families. Unhover events are
/// matched by exact node identity so a stale event cannot clear a newer target that represents the same creature.
/// The observer automatically stops listening immediately before the target manager resolves the targeting result.
/// </remarks>
internal sealed class CombatPredictionTargetObserver : IDisposable
{
    private static readonly HashSet<CombatPredictionTargetObserver> _activeObservers = [];

    private readonly NTargetManager _targetManager;

    private Node? _hoveredNode;
    private bool _disposed;

    /// <summary>
    /// Raised when the active target node changes, or with <see langword="null"/> when that node is unhovered.
    /// </summary>
    /// <remarks>Finishing targeting raises <see cref="TargetingFinishing"/> without synthesizing an unhover event.</remarks>
    public event Action<Creature?>? TargetChanged;

    /// <summary>
    /// Raised after the observer stops listening, immediately before the target manager resolves the targeting result.
    /// </summary>
    public event Action? TargetingFinishing;

    /// <summary>Begins observing one target manager until targeting ends or this instance is disposed.</summary>
    public CombatPredictionTargetObserver(NTargetManager targetManager)
    {
        _targetManager = targetManager;
        targetManager.CreatureHovered += OnCreatureHovered;
        targetManager.CreatureUnhovered += OnCreatureUnhovered;
        targetManager.NodeHovered += OnNodeHovered;
        targetManager.NodeUnhovered += OnNodeUnhovered;
        _activeObservers.Add(this);
    }

    /// <summary>Stops observing the target manager. Repeated calls are ignored.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _targetManager.CreatureHovered -= OnCreatureHovered;
        _targetManager.CreatureUnhovered -= OnCreatureUnhovered;
        _targetManager.NodeHovered -= OnNodeHovered;
        _targetManager.NodeUnhovered -= OnNodeUnhovered;
        _activeObservers.Remove(this);
    }

    private void OnCreatureHovered(NCreature creature)
    {
        SetHoveredTarget(creature);
    }

    private void OnCreatureUnhovered(NCreature creature)
    {
        ClearHoveredTarget(creature);
    }

    private void OnNodeHovered(Node node)
    {
        SetHoveredTarget(node);
    }

    private void OnNodeUnhovered(Node node)
    {
        ClearHoveredTarget(node);
    }

    private void SetHoveredTarget(Node node)
    {
        if (!ReferenceEquals(_hoveredNode, node) &&
            CombatPredictionTargetResolver.TryResolveCreature(node, out var creature))
        {
            _hoveredNode = node;
            TargetChanged?.Invoke(creature);
        }
    }

    private void ClearHoveredTarget(Node node)
    {
        if (ReferenceEquals(_hoveredNode, node))
        {
            _hoveredNode = null;
            TargetChanged?.Invoke(null);
        }
    }

    private void OnTargetingFinishing()
    {
        _hoveredNode = null;
        Dispose();
        TargetingFinishing?.Invoke();
    }

    internal static void OnTargetingFinishing(NTargetManager targetManager)
    {
        foreach (var observer in _activeObservers.ToArray())
        {
            if (ReferenceEquals(observer._targetManager, targetManager))
            {
                observer.OnTargetingFinishing();
            }
        }
    }
}
