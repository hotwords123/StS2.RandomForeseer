using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal sealed class RunPredictionContext
{
    private RelicGrabBag? _relicGrabBag;

    public RunPredictionContext(
        Player player,
        RunPredictionRngSet? rng = null,
        RelicGrabBag? relicGrabBag = null,
        CardRarityOdds? cardRarityOdds = null,
        PotionRewardOdds? potionRewardOdds = null)
    {
        Player = player;
        Rng = rng ?? RunPredictionRngSet.FromPlayer(player);
        _relicGrabBag = relicGrabBag;
        CardRarityOdds = cardRarityOdds ?? new(player.PlayerOdds.CardRarity.CurrentValue, Rng.Rewards);
        PotionRewardOdds = potionRewardOdds ?? new(player.PlayerOdds.PotionReward.CurrentValue, Rng.Rewards);
    }

    public Player Player { get; }

    public IRunState RunState => Player.RunState;

    public RunPredictionRngSet Rng { get; }

    public RelicGrabBag RelicGrabBag => _relicGrabBag ??= Player.RelicGrabBag.Clone();

    public CardRarityOdds CardRarityOdds { get; }

    public PotionRewardOdds PotionRewardOdds { get; }

    public RunPredictionContext Clone()
    {
        var rng = Rng.Clone();
        var cardRarityOdds = new CardRarityOdds(CardRarityOdds.CurrentValue, rng.Rewards);
        var potionRewardOdds = new PotionRewardOdds(PotionRewardOdds.CurrentValue, rng.Rewards);
        return new RunPredictionContext(Player, rng, _relicGrabBag?.Clone(), cardRarityOdds, potionRewardOdds);
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
