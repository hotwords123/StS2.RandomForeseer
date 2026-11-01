using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal sealed class RunPredictionContext(
    Player player,
    RunPredictionRngMap rngMap,
    Dictionary<Player, RunPredictionPlayerContext> playerContexts)
{
    public Player Player => player;

    public IRunState RunState => Player.RunState;

    public RunPredictionSharedRngSet SharedRng { get; } = new(player.RunState.Rng, rngMap);

    public RunPredictionPlayerRngSet Rng => GetPlayer(player).Rng;

    public RelicGrabBag RelicGrabBag => GetPlayer(player).RelicGrabBag;

    public SimCardPile Deck => GetPlayer(player).Deck;

    public CardRarityOdds CardRarityOdds => GetPlayer(player).CardRarityOdds;

    public PotionRewardOdds PotionRewardOdds => GetPlayer(player).PotionRewardOdds;

    public RunPredictionContext(Player player)
        : this(player, new RunPredictionRngMap(), [])
    { }

    /// <summary>
    /// Returns the prediction RNG mapped to the original <paramref name="source"/>, cloning it on first use.
    /// The mapping is shared by player views and includes the standard player and shared RNG streams.
    /// </summary>
    public Rng GetOrCloneRng(Rng source) => rngMap.GetOrClone(source);

    public RunPredictionPlayerContext GetPlayer(Player otherPlayer)
    {
        if (!playerContexts.TryGetValue(otherPlayer, out var context))
        {
            context = new RunPredictionPlayerContext(otherPlayer, rngMap);
            playerContexts[otherPlayer] = context;
        }

        return context;
    }

    public RunPredictionContext Clone()
    {
        var clonedRngMap = rngMap.Clone();
        return new(
            player,
            clonedRngMap,
            playerContexts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone(clonedRngMap)));
    }

    public RunPredictionContext ForPlayer(Player otherPlayer)
    {
        return ReferenceEquals(player, otherPlayer)
            ? this
            : new(otherPlayer, rngMap, playerContexts);
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

    public SimCardPile Deck => deck ??= new SimCardPile(player.Deck);

    public CardRarityOdds CardRarityOdds => cardRarityOdds;

    public PotionRewardOdds PotionRewardOdds => potionRewardOdds;

    public RunPredictionPlayerContext(Player player, RunPredictionRngMap rngMap)
        : this(player, new RunPredictionPlayerRngSet(player.PlayerRng, rngMap))
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

    public RunPredictionPlayerContext Clone(RunPredictionRngMap rngMap)
    {
        var clonedRng = new RunPredictionPlayerRngSet(player.PlayerRng, rngMap);
        return new(
            player,
            clonedRng,
            relicGrabBag?.Clone(),
            deck?.Clone(),
            new(cardRarityOdds.CurrentValue, clonedRng.Rewards),
            new(potionRewardOdds.CurrentValue, clonedRng.Rewards));
    }
}

/// <summary>Maps original RNG references to their lazily created prediction copies.</summary>
internal sealed class RunPredictionRngMap
{
    private readonly Dictionary<Rng, Rng> _copies = new(ReferenceEqualityComparer.Instance);

    public Rng GetOrClone(Rng source)
    {
        if (!_copies.TryGetValue(source, out var prediction))
        {
            prediction = source.Clone();
            _copies.Add(source, prediction);
        }

        return prediction;
    }

    /// <summary>Copies the current prediction states while retaining their original RNG keys.</summary>
    public RunPredictionRngMap Clone()
    {
        var clone = new RunPredictionRngMap();
        foreach (var (source, prediction) in _copies)
        {
            clone._copies.Add(source, prediction.Clone());
        }

        return clone;
    }
}

internal sealed class RunPredictionSharedRngSet(RunRngSet source, RunPredictionRngMap rngMap)
{
    public Rng Niche => rngMap.GetOrClone(source.Niche);
}

internal sealed class RunPredictionPlayerRngSet(PlayerRngSet source, RunPredictionRngMap rngMap)
{
    public Rng Rewards => rngMap.GetOrClone(source.Rewards);

    public Rng Shops => rngMap.GetOrClone(source.Shops);
}
