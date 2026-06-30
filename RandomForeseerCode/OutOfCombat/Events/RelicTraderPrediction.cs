using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class RelicTraderPrediction
{
    public static RelicModel? GetReceivedRelic(RelicTrader relicTrader, EventOption option)
    {
        var newRelics = relicTrader.NewRelics;
        return option.TextKey switch
        {
            "RELIC_TRADER.pages.INITIAL.options.TOP" when newRelics.Count > 0 => newRelics[0],
            "RELIC_TRADER.pages.INITIAL.options.MIDDLE" when newRelics.Count > 1 => newRelics[1],
            "RELIC_TRADER.pages.INITIAL.options.BOTTOM" when newRelics.Count > 2 => newRelics[2],
            _ => null,
        };
    }
}
