using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class RoundTeaPartyPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<RoundTeaParty>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(RoundTeaParty roundTeaParty, EventOption option)
    {
        return option.TextKey is "ROUND_TEA_PARTY.pages.INITIAL.options.PICK_FIGHT" or "ROUND_TEA_PARTY.pages.PICK_FIGHT.options.CONTINUE_FIGHT"
            ? OutOfCombatPredictionUtils.RelicTipsWithPickup(roundTeaParty.Owner!, OutOfCombatPredictionUtils.PredictRelicRewards(roundTeaParty.Owner!, 1))
            : [];
    }
}
