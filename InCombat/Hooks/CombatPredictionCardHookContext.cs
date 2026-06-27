using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat.Hooks;

internal abstract class CombatPredictionCardHookContext : CombatPredictionHookContext
{
    public required PredictedCard Card { get; init; }

    public CardModel OriginalCard => Card.Original;

    public CardModel PreviewCard => Card.Preview;

    public CardModel MutablePreviewCard => Card.MutablePreview;
}
