
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
        // StS2 v0.109.0 serializes the complete Xoshiro state instead of rebuilding it from a
        // seed and counter. Round-tripping that state keeps prediction cloning constant-time.
        return new Rng(rng.ToSerializable());
    }

    public static void Advance(this Rng rng, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        // StS2 v0.108.0 FastForwardCounter advanced MegaRandom once per discarded value.
        // v0.109.0 removed counter-based reconstruction, so discard raw draws directly.
        for (var i = 0; i < count; i++)
        {
            _ = rng.NextUnsignedLong();
        }
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
