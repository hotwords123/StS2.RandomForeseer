using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer.Tests.Infrastructure;

// DispatchProxy requires a public, non-sealed proxy. Unexpected API use fails loudly.
public class CombatStateProxy : DispatchProxy
{
    internal IReadOnlyList<Creature> Allies { get; set; } = [];
    internal IReadOnlyList<Creature> Enemies { get; set; } = [];
    internal IEnumerable<AbstractModel> Listeners { get; set; } = [];
    internal IEnumerable<AbstractModel> HookListeners =>
        Listeners.Concat(Allies.Concat(Enemies).SelectMany(creature => creature.Powers)).Distinct();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
    {
        "get_Allies" => Allies,
        "get_Enemies" => Enemies,
        "get_Creatures" => Allies.Concat(Enemies).ToArray(),
        "get_Players" => Allies.Select(creature => creature.Player!).ToArray(),
        "get_RunState" => NullRunState.Instance,
        "IterateHookListeners" => HookListeners,
        "get_RoundNumber" => 1,
        "get_CurrentSide" => CombatSide.Player,
        "GetPlayer" => Allies.Select(creature => creature.Player).Single(player => player!.NetId == (ulong)args![0]!),
        _ => throw new NotSupportedException($"Combat test fixture does not implement {targetMethod.Name}.")
    };
}
