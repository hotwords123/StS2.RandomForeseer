using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Card;

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

internal sealed class EncorePredictionState(EncorePower power)
{
    public List<CardModel> CardsBeingPlayed { get; } =
        [.. power.GetInternalData<EncorePower.Data>().cardsBeingPlayed];

    public int CardsPlayed { get; set; } =
        power.GetInternalData<EncorePower.Data>().numberOfCardsPlayedSinceApplied;

    public bool Removed { get; set; }
}

internal sealed class StretchPredictionState(StretchPower power)
{
    public CardPlay? Play { get; set; } = power.GetInternalData<StretchPower.Data>().cardPlay;

    public int StartingAmount { get; set; } = power.GetInternalData<StretchPower.Data>().amountWhenCardStarted;

    public bool GaveBonus { get; set; } = power.GetInternalData<StretchPower.Data>().gaveBonusBlock;
}
