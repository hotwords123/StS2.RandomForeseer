
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PredictionExtensions
{
    public static Rng Clone(this Rng rng)
    {
        var clone = new Rng(rng.Seed)
        {
            Counter = rng.Counter
        };

        // STS2 0.107.1 stores Rng state in MegaRandom's Xoshiro** state.
        // Copying it directly avoids replaying an ever-growing counter during predictions.
        clone._random._s0 = rng._random._s0;
        clone._random._s1 = rng._random._s1;
        clone._random._s2 = rng._random._s2;
        clone._random._s3 = rng._random._s3;
        return clone;
    }

    public static RelicGrabBag Clone(this RelicGrabBag grabBag)
    {
        return RelicGrabBag.FromSerializable(grabBag.ToSerializable());
    }

    public static IEnumerable<CardModel> GetUnlockedCards(this Player player, CardPoolModel cardPool)
    {
        return cardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint);
    }

    public static IEnumerable<CardModel> GetUnlockedCharacterCards(this Player player)
    {
        return player.GetUnlockedCards(player.Character.CardPool);
    }

    public static IEnumerable<CardModel> GetUnlockedColorlessCards(this Player player)
    {
        return player.GetUnlockedCards(ModelDb.CardPool<ColorlessCardPool>());
    }

    public static IEnumerable<CardModel> GetUnlockedCurseCards(this Player player)
    {
        return player.GetUnlockedCards(ModelDb.CardPool<CurseCardPool>());
    }
}
