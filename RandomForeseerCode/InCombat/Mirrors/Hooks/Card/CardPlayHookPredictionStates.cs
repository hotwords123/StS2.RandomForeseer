using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Card;

// Shadow model state shared by card-play lifecycle hooks and their downstream value/predicate hook mirrors.
internal sealed class CounterPredictionState(int value)
{
    public int Value { get; set; } = value;
}

internal sealed class ChainsOfBindingPredictionState(ChainsOfBindingPower power)
{
    public bool BoundCardPlayed { get; set; } =
        power.GetInternalData<ChainsOfBindingPower.Data>().boundCardPlayed;
}

internal sealed class SurroundedPredictionState(SurroundedPower power)
{
    public SurroundedPower.Direction Facing { get; set; } = power.Facing;
}

internal sealed class PenNibPredictionState(PenNib relic)
{
    public int AttacksPlayed { get; set; } = relic.AttacksPlayed;

    public CardModel? AttackToDouble { get; set; }
}

internal sealed class PaelsLegionPredictionState(PaelsLegion relic)
{
    public int Cooldown { get; set; } = relic._cooldown;

    public bool TriggeredBlockLastTurn { get; set; } = relic._triggeredBlockLastTurn;

    public CardPlay? AffectedCardPlay { get; set; } = relic._affectedCardPlay;
}

internal sealed class VambracePredictionState(Vambrace relic)
{
    public CardModel? TriggeringCard { get; set; } = relic._triggeringCard;

    public bool BlockGainedThisCombat { get; set; } = relic._blockGainedThisCombat;
}

internal sealed class VoidFormPredictionState(VoidFormPower power)
{
    public int CardsPlayedThisTurn { get; set; } =
        power.GetInternalData<VoidFormPower.Data>().cardsPlayedThisTurn;
}
