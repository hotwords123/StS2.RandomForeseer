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

    public IReadOnlyList<Creature> Allies => CombatState.Allies;

    public IReadOnlyList<Creature> Enemies => CombatState.Enemies;

    public IReadOnlyList<Creature> Creatures => CombatState.Creatures;

    public IReadOnlyList<Creature> PlayerCreatures => CombatState.PlayerCreatures;

    public IReadOnlyList<Player> Players => CombatState.Players;

    public IReadOnlyList<Creature> HittableEnemies =>
        Enemies.Where(enemy => GetCreature(enemy).IsHittable).ToArray();

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
        // TODO: Make this align with the real CombatState's GetOpponentsOf
        return CombatState.GetOpponentsOf(creature)
            .Where(opponent => GetCreature(opponent).IsAlive)
            .ToList();
    }

    public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side)
    {
        // TODO: Make this align with the real CombatState's GetCreaturesOnSide
        return CombatState.GetCreaturesOnSide(side)
            .Where(creature => GetCreature(creature).IsAlive)
            .ToList();
    }

    public SimPlayerCombatState GetPlayerCombatState(Player player)
    {
        if (!_playerCombatStates.TryGetValue(player, out var state))
        {
            state = new SimPlayerCombatState(player);
            _playerCombatStates.Add(player, state);
        }

        return state;
    }

    public PredictedCard? FindCard(CardModel card)
    {
        return GetPlayerCombatState(card.Owner).FindCard(card);
    }
}
