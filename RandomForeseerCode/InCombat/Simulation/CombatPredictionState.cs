using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class CombatPredictionState(ICombatState combatState)
{
    public ICombatState CombatState { get; } = combatState;

    private readonly Dictionary<Creature, SimCreatureState> _creatures = [];

    private readonly Dictionary<Player, SimPlayerCombatState> _playerCombatStates = [];

    public SimCreatureState GetCreature(Creature creature)
    {
        if (!_creatures.TryGetValue(creature, out var state))
        {
            state = new SimCreatureState(creature);
            _creatures.Add(creature, state);
        }

        return state;
    }

    public IReadOnlyList<Creature> GetOpponentsOf(Creature creature)
    {
        return CombatState.GetOpponentsOf(creature)
            .Where(opponent => GetCreature(opponent).IsAlive)
            .ToList();
    }

    public IReadOnlyList<Creature> GetHittableOpponentsOf(Creature creature)
    {
        return CombatState.GetOpponentsOf(creature)
            .Where(opponent => GetCreature(opponent).IsHittable)
            .ToList();
    }

    public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side)
    {
        return CombatState.GetCreaturesOnSide(side)
            .Where(creature => GetCreature(creature).IsAlive)
            .ToList();
    }

    public SimPlayerCombatState GetPlayerCombatState(Player player)
    {
        if (!_playerCombatStates.TryGetValue(player, out var state))
        {
            state = new SimPlayerCombatState(player, CombatState);
            _playerCombatStates.Add(player, state);
        }

        return state;
    }

    public PredictedCard? FindCard(CardModel card)
    {
        return GetPlayerCombatState(card.Owner).FindCard(card);
    }
}
