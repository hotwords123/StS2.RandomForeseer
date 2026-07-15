
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PredictionExtensions
{
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
