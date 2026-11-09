using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.InCombat.Mirrors.Afflictions.OnPlay;

internal sealed class AfflictionOnPlayMirrorContext : CombatCardMirrorContext<AfflictionModel>
{
    public required Creature? Target { get; init; }

    protected override AbstractModel GetDispatchSource(AfflictionModel receiver)
    {
        return OriginalCard.Affliction is { } original && original.GetType() == receiver.GetType()
            ? original
            : receiver;
    }
}
