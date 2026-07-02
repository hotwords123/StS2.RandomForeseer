using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal sealed class RunPredictionContext(
    Player player,
    RunPredictionSharedRngSet sharedRng,
    Dictionary<Player, RunPredictionPlayerContext> playerContexts)
{
    public Player Player => player;

    public IRunState RunState => Player.RunState;

    public RunPredictionSharedRngSet SharedRng => sharedRng;

    public RunPredictionPlayerRngSet Rng => GetPlayer(player).Rng;

    public RelicGrabBag RelicGrabBag => GetPlayer(player).RelicGrabBag;

    public SimCardPile Deck => GetPlayer(player).Deck;

    public CardRarityOdds CardRarityOdds => GetPlayer(player).CardRarityOdds;

    public PotionRewardOdds PotionRewardOdds => GetPlayer(player).PotionRewardOdds;

    public RunPredictionContext(Player player)
        : this(player, RunPredictionSharedRngSet.From(player.RunState.Rng), [])
    { }

    public RunPredictionPlayerContext GetPlayer(Player otherPlayer)
    {
        if (!playerContexts.TryGetValue(otherPlayer, out var context))
        {
            context = new RunPredictionPlayerContext(otherPlayer);
            playerContexts[otherPlayer] = context;
        }

        return context;
    }

    public RunPredictionContext Clone()
    {
        return new(
            player,
            sharedRng.Clone(),
            playerContexts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()));
    }

    public RunPredictionContext ForPlayer(Player otherPlayer)
    {
        return ReferenceEquals(player, otherPlayer)
            ? this
            : new(otherPlayer, sharedRng, playerContexts);
    }
}

internal sealed class RunPredictionPlayerContext(
    Player player,
    RunPredictionPlayerRngSet rng,
    RelicGrabBag? relicGrabBag,
    SimCardPile? deck,
    CardRarityOdds cardRarityOdds,
    PotionRewardOdds potionRewardOdds)
{
    public Player Player => player;

    public RunPredictionPlayerRngSet Rng => rng;

    public RelicGrabBag RelicGrabBag => relicGrabBag ??= player.RelicGrabBag.Clone();

    public SimCardPile Deck => deck ??= SimCardPile.FromPlayerPile(PileType.Deck, player);

    public CardRarityOdds CardRarityOdds => cardRarityOdds;

    public PotionRewardOdds PotionRewardOdds => potionRewardOdds;

    public RunPredictionPlayerContext(Player player)
        : this(player, RunPredictionPlayerRngSet.From(player.PlayerRng))
    { }

    public RunPredictionPlayerContext(Player player, RunPredictionPlayerRngSet rng)
        : this(
            player,
            rng,
            null,
            null,
            new(player.PlayerOdds.CardRarity.CurrentValue, rng.Rewards),
            new(player.PlayerOdds.PotionReward.CurrentValue, rng.Rewards))
    { }

    public RunPredictionPlayerContext Clone()
    {
        var clonedRng = rng.Clone();
        return new(
            player,
            clonedRng,
            relicGrabBag?.Clone(),
            deck?.Clone(),
            new(cardRarityOdds.CurrentValue, clonedRng.Rewards),
            new(potionRewardOdds.CurrentValue, clonedRng.Rewards));
    }
}

internal sealed class RunPredictionSharedRngSet
{
    public required Rng Niche { get; init; }

    public static RunPredictionSharedRngSet From(RunRngSet rng)
    {
        return new RunPredictionSharedRngSet
        {
            Niche = PredictionUtils.CloneRng(rng.Niche),
        };
    }

    public RunPredictionSharedRngSet Clone()
    {
        return new RunPredictionSharedRngSet
        {
            Niche = PredictionUtils.CloneRng(Niche),
        };
    }
}

internal sealed class RunPredictionPlayerRngSet
{
    public required Rng Rewards { get; init; }

    public static RunPredictionPlayerRngSet From(PlayerRngSet rng)
    {
        return new RunPredictionPlayerRngSet
        {
            Rewards = PredictionUtils.CloneRng(rng.Rewards)
        };
    }

    public RunPredictionPlayerRngSet Clone()
    {
        return new RunPredictionPlayerRngSet
        {
            Rewards = PredictionUtils.CloneRng(Rewards)
        };
    }
}
