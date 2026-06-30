using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class RoundTeaPartyPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(RoundTeaParty roundTeaParty, EventOption option)
    {
        return option.TextKey is "ROUND_TEA_PARTY.pages.INITIAL.options.PICK_FIGHT" or "ROUND_TEA_PARTY.pages.PICK_FIGHT.options.CONTINUE_FIGHT"
            ? OutOfCombatPredictionUtils.RelicTipsWithPickup(roundTeaParty.Owner!, OutOfCombatPredictionUtils.PredictRelicRewards(roundTeaParty.Owner!, 1))
            : [];
    }
}
