using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.Tests.Infrastructure;

/// <summary>Owns process-global isolation for one test in <see cref="GameTestCollection"/>.</summary>
public abstract class GameTestBase : IDisposable
{
    private static bool _assemblyInitialized;
    private static GameTestBase? _active;
    private readonly Harmony _harmony = new($"RandomForeseer.Tests.{Guid.NewGuid():N}");
    private bool _disposed;
    internal List<string> Calls { get; } = [];
    internal List<(string Name, Creature Target, int Amount)> PowerChanges { get; } = [];
    internal Action<CombatPredictionSimulator>? AfterAttack { get; set; }
    internal int BlockBreaks { get; private set; }
    internal int Drawn { get; private set; }

    protected GameTestBase(bool observePowerCommands = false)
    {
        if (_active is not null) throw new InvalidOperationException("Game tests must not run concurrently.");
        _active = this;
        try
        {
            if (!_assemblyInitialized)
            {
                MegaCrit.Sts2.Core.Modding.AssemblyInfo.Init();
                _assemblyInitialized = true;
            }
            // Relic static initialization constructs StringName values, which needs a Godot native runtime.
            _harmony.Patch(AccessTools.Constructor(typeof(Godot.StringName), [typeof(string)]),
                prefix: new HarmonyMethod(typeof(GameTestBase), nameof(SkipStringName)));
            Patch(typeof(CompatibilityUtils), nameof(CompatibilityUtils.FilterHookListeners), nameof(Filter));
            Patch(typeof(HookMirrors), "IterateRunHookListeners", nameof(RunListeners));
            ModelDb.ResetForTest();
            ModelDb.Init([typeof(ArtifactPower), typeof(WeakPower), typeof(VulnerablePower),
                typeof(StrengthPower), typeof(PoisonPower)]);
            CombatManager.Instance.History.Clear();
            // Keep optional patch setup inside this cleanup boundary: a failed test constructor is not disposed by xUnit.
            if (observePowerCommands) ObservePowerCommands();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void ObservePowerCommands()
    {
        // Observe power/block command order without replacing the command or hook implementation.
        Patch(typeof(HookMirrors), nameof(HookMirrors.BeforePowerAmountChanged), nameof(BeforePower));
        Patch(typeof(HookMirrors), nameof(HookMirrors.AfterPowerAmountChanged), nameof(AfterPower));
        Patch(typeof(HookMirrors), nameof(HookMirrors.AfterBlockBroken), nameof(BlockBroken));
        // These tests assert OnPlay command ordering, not attack damage or card drawing internals.
        Patch(typeof(CombatPredictionSimulator), nameof(CombatPredictionSimulator.ExecuteAttack), nameof(Attack));
        Patch(typeof(CompatibilityUtils), nameof(CompatibilityUtils.IsPredictionAllowedForType), nameof(Allow));
        _harmony.Patch(AccessTools.Method(typeof(CombatPredictionSimulator), nameof(CombatPredictionSimulator.Draw),
                [typeof(Player), typeof(int), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(GameTestBase), nameof(Draw)));
    }

    private void Patch(Type type, string method, string prefix) =>
        _harmony.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(typeof(GameTestBase), prefix));

    private static bool SkipStringName() => false;
    private static bool Allow(ref bool __result) { __result = true; return false; }
    private static bool Filter(IEnumerable<AbstractModel> listeners, ref IEnumerable<AbstractModel> __result)
    {
        __result = listeners;
        return false;
    }
    private static bool RunListeners(CombatPredictionSimulator simulator, ref IEnumerable<AbstractModel> __result)
    {
        __result = simulator.State.CombatState.IterateHookListeners();
        return false;
    }
    private static void BeforePower(PowerModel __1, Creature __3) =>
        _active!.Calls.Add($"{__1.GetType().Name}:{__3.CombatId}");
    private static void AfterPower(CombatPredictionSimulator __0, PowerModel __1, Creature __3) =>
        _active!.PowerChanges.Add((__1.GetType().Name, __3,
            RandomForeseerCode.InCombat.Mirrors.Hooks.PredictionStateStorePowerAmountExtensions
                .GetPowerAmount(__0.StateStore, __1).Amount));
    private static void BlockBroken()
    {
        _active!.BlockBreaks++;
        _active.Calls.Add("BlockBroken");
    }
    private static bool Attack(CombatPredictionSimulator __instance)
    {
        _active!.Calls.Add("Attack");
        _active.AfterAttack?.Invoke(__instance);
        return false;
    }
    private static bool Draw(int __1, ref IReadOnlyList<PredictedCard> __result)
    {
        _active!.Drawn += __1;
        __result = [];
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _harmony.UnpatchAll(_harmony.Id);
            CombatManager.Instance.History.Clear();
            ModelDb.ResetForTest();
        }
        finally
        {
            _active = null;
        }
        GC.SuppressFinalize(this);
    }
}
