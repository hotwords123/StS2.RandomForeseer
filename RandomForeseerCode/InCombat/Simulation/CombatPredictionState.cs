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

    private readonly HashSet<Creature> _removedCreatures = [];

    private readonly Dictionary<Player, SimPlayerCombatState> _playerCombatStates = [];

    public IReadOnlyList<Creature> Allies => ExcludeRemoved(CombatState.Allies);

    public IReadOnlyList<Creature> Enemies => ExcludeRemoved(CombatState.Enemies);

    public IReadOnlyList<Creature> Creatures => ExcludeRemoved(CombatState.Creatures);

    public IReadOnlyList<Creature> PlayerCreatures => ExcludeRemoved(CombatState.PlayerCreatures);

    public IReadOnlyList<Player> Players => CombatState.Players;

    public IReadOnlyList<Creature> HittableEnemies =>
        [.. ExcludeRemoved(CombatState.Enemies).Where(enemy => GetCreature(enemy).IsHittable)];

    public SimCreatureState GetCreature(Creature creature)
    {
        if (!_creatures.TryGetValue(creature, out var state))
        {
            state = new SimCreatureState(creature);
            _creatures.Add(creature, state);
        }

        return state;
    }

    public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) =>
        ExcludeRemoved(CombatState.GetOpponentsOf(creature));

    public IReadOnlyList<Creature> GetTeammatesOf(Creature creature) =>
        ExcludeRemoved(CombatState.GetTeammatesOf(creature));

    public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) =>
        ExcludeRemoved(CombatState.GetCreaturesOnSide(side));

    public IEnumerable<AbstractModel> IterateHookListeners() => CombatState.IterateHookListeners();

    public SimPlayerCombatState GetPlayerCombatState(Player player)
    {
        if (!_playerCombatStates.TryGetValue(player, out var state))
        {
            state = new SimPlayerCombatState(player);
            _playerCombatStates.Add(player, state);
        }

        return state;
    }

    public void RemoveCreature(Creature creature)
    {
        if (Creatures.Contains(creature))
        {
            _removedCreatures.Add(creature);
        }
    }

    public PredictedCard? FindCard(CardModel card)
    {
        return GetPlayerCombatState(card.Owner).FindCard(card);
    }

    private IReadOnlyList<Creature> ExcludeRemoved(IEnumerable<Creature> creatures)
    {
        return [.. creatures.Where(creature => !_removedCreatures.Contains(creature))];
    }
}
