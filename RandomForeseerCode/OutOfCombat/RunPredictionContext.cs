using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal sealed class RunPredictionContext(
    Player player,
    RunPredictionRngSet? rng = null,
    RelicGrabBag? relicGrabBag = null)
{
    public Player Player => player;

    public IRunState RunState => player.RunState;

    public RunPredictionRngSet Rng { get; } = rng ?? RunPredictionRngSet.FromPlayer(player);

    public RelicGrabBag RelicGrabBag => relicGrabBag ??= player.RelicGrabBag.Clone();

    public RunPredictionContext Clone()
    {
        return new RunPredictionContext(Player, Rng.Clone(), relicGrabBag?.Clone());
    }
}

internal sealed class RunPredictionRngSet
{
    public required Rng Niche { get; init; }
    public required Rng Rewards { get; init; }

    public static RunPredictionRngSet FromPlayer(Player player)
    {
        return new RunPredictionRngSet
        {
            Niche = PredictionUtils.CloneRng(player.RunState.Rng.Niche),
            Rewards = PredictionUtils.CloneRng(player.PlayerRng.Rewards)
        };
    }

    public RunPredictionRngSet Clone()
    {
        return new RunPredictionRngSet
        {
            Niche = PredictionUtils.CloneRng(Niche),
            Rewards = PredictionUtils.CloneRng(Rewards)
        };
    }
}
