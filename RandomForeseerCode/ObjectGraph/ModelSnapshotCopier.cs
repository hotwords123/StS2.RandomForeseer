using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.ObjectGraph;

/// <summary>Captures a model's current data with shared external combat/run context and no gameplay clone hooks.</summary>
/// <remarks>
/// Only the mutable root and its current card attachments are selected for model cloning; other models are shared.
/// Selection reads attachment identities from the root; keep those links stable until capture returns.
/// Model-owned data, collections and callbacks follow the snapshot policy;
/// getters can still read live state through shared context. Uninitialized fields remain lazy and events are cleared.
/// Each capture has a new identity scope; cached policies and their plans are for synchronous, single-threaded use.
/// </remarks>
internal static class ModelSnapshotCopier
{
    public static GraphCopyPolicy Policy { get; } = new(
        referenceRules:
        [
            new(
                "snapshot.model",
                type => typeof(AbstractModel).IsAssignableFrom(type),
                GraphReferenceStrategy.Choose(selection => selection.GetContext<SnapshotContext>().Select(selection))),
            new(
                "snapshot.context",
                type =>
                    typeof(Player).IsAssignableFrom(type) ||
                    typeof(Creature).IsAssignableFrom(type) ||
                    typeof(IRunState).IsAssignableFrom(type) ||
                    typeof(ICombatState).IsAssignableFrom(type) ||
                    typeof(CardPile).IsAssignableFrom(type),
                GraphReferenceStrategy.Share)
        ],
        referenceFallback: GraphReferenceStrategy.Clone,
        valueFallback: GraphValueAction.Fields);

    public static T Copy<T>(T source) where T : AbstractModel
    {
        ArgumentNullException.ThrowIfNull(source);
        using var scope = new PredictionObjectScope();
        return new PredictionGraphCopier(scope, Policy, new SnapshotContext(source)).Copy(source);
    }

    private sealed class SnapshotContext(AbstractModel root)
    {
        public GraphReferenceAction Select(GraphReferenceSelection selection)
        {
            var source = selection.GetSource<AbstractModel>();
            if (!source.IsMutable)
            {
                return GraphReferenceAction.Share;
            }

            if (source == root || root is CardModel card && (source == card.Enchantment || source == card.Affliction))
            {
                return GraphReferenceAction.Clone;
            }

            return GraphReferenceAction.Share;
        }
    }
}
