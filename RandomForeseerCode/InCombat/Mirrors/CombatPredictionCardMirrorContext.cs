using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

internal abstract class CombatPredictionCardMirrorContext : CombatPredictionMirrorContext
{
    public required PredictedCard Card { get; init; }

    public CardModel OriginalCard => Card.Original;

    public CardModel PreviewCard => Card.Preview;

    public CardModel MutablePreviewCard => Card.MutablePreview;
}
