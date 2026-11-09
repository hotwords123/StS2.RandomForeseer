using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.ObjectGraph;
using RandomForeseer.Tests.Infrastructure;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Checks canonical identity and mutable-model snapshots in <see cref="ModelSnapshotCopier.Copy{T}"/>.</summary>
/// <remarks>
/// Uses game fixtures for shared owner/target/origin context, attachment back-links, lazy fields and event subscribers.
/// Snapshot data may be mutated through fields and owned collections; gameplay setters and live context are not isolated.
/// </remarks>
[Collection(GameTestCollection.Name)]
public sealed class ModelSnapshotCopierTests : GameTestBase
{
    [Fact]
    public void MutableCardCopyPreservesRuntimeStateWithoutChangingSource()
    {
        using var combat = new TestCombat();
        var source = combat.ArrangeCard<StrikeIronclad>();
        ModelDb.Inject(typeof(Sharp));
        ModelDb.Inject(typeof(Bound));
        var enchantment = ModelDb.Enchantment<Sharp>().ToMutable();
        enchantment._card = source;
        source.Enchantment = enchantment;
        var affliction = ModelDb.Affliction<Bound>().ToMutable();
        affliction._card = source;
        source.Affliction = affliction;
        source.CurrentTarget = combat.Player.Creature;
        source._deckVersion = ModelDb.Card<StrikeIronclad>().ToMutable();
        source._cloneOf = ModelDb.Card<StrikeIronclad>().ToMutable();
        source._tags = [CardTag.Strike];
        source.DynamicVars.Damage.PreviewValue = 27;
        var sourceCost = source.EnergyCost;
        sourceCost.AddThisTurn(-1);
        var sourceModifierAmount = sourceCost._localModifiers[0].Amount;
        var executions = 0;
        source.ExecutionFinished += _ => executions++;

        var copy = ModelSnapshotCopier.Copy(source);
        copy.DynamicVars.Damage.PreviewValue = 5;
        copy.EnergyCost._localModifiers[0].Amount++;
        copy._tags!.Clear();
        copy.InvokeExecutionFinished();

        Assert.NotSame(source, copy);
        Assert.Same(source.CurrentTarget, copy.CurrentTarget);
        Assert.Same(source.Owner, copy.Owner);
        Assert.Same(source.DeckVersion, copy.DeckVersion);
        Assert.Same(source.CloneOf, copy.CloneOf);
        Assert.Same(source.CanonicalInstance, copy.CanonicalInstance);
        Assert.Same(source, source.Enchantment.Card);
        Assert.Same(source, source.Affliction.Card);
        Assert.Same(copy, copy.Enchantment!.Card);
        Assert.Same(copy, copy.Affliction!.Card);
        Assert.NotSame(sourceCost, copy.EnergyCost);
        Assert.Same(copy, copy.EnergyCost._card);
        Assert.Same(copy, copy.DynamicVars.Damage._owner);
        Assert.Equal(sourceModifierAmount, sourceCost._localModifiers[0].Amount);
        Assert.Contains(CardTag.Strike, source._tags);
        Assert.Equal(27, source.DynamicVars.Damage.PreviewValue);
        Assert.Equal(0, executions);
    }

    [Fact]
    public void CanonicalModelReturnsItself()
    {
        ModelDb.Inject(typeof(StrikeIronclad));
        var canonical = ModelDb.Card<StrikeIronclad>();

        Assert.Same(canonical, ModelSnapshotCopier.Copy(canonical));
    }

    [Fact]
    public void CustomModelCopiesOwnedDataAndDropsSubscribersWithoutCloneHooks()
    {
        ModelDb.Inject(typeof(CloneFallbackAffliction));
        var source = (CloneFallbackAffliction)ModelDb.Affliction<CloneFallbackAffliction>().ToMutable();
        var other = (CloneFallbackAffliction)ModelDb.Affliction<CloneFallbackAffliction>().ToMutable();
        var calls = 0;
        source.ExtraChanged += () => calls++;
        source.Values.Add(7);
        source.RejectCloneHook = true;
        source.LinkedModel = other;
        other.LinkedModel = source;
        source.ReadLinked = source.ReadLinkedModel;
        source.ExternalReadLinked = other.ReadLinkedModel;

        var copy = ModelSnapshotCopier.Copy(source);
        copy.Raise();
        copy.Values.Clear();
        source.Raise();

        Assert.NotSame(source, copy);
        Assert.Equal(1, calls);
        Assert.Equal([7], source.Values);
        Assert.Same(other, copy.LinkedModel);
        Assert.Same(copy, copy.ReadLinked!.Target);
        Assert.Same(other, copy.ReadLinked());
        Assert.Same(source.ExternalReadLinked, copy.ExternalReadLinked);
        Assert.Same(source, copy.ExternalReadLinked!());
    }

    [Fact]
    public void CardSnapshotSelectsOnlyItsCurrentAttachmentsByIdentity()
    {
        using var combat = new TestCombat();
        var source = combat.ArrangeCard<StrikeIronclad>();
        ModelDb.Inject(typeof(CloneFallbackAffliction));
        var attached = (CloneFallbackAffliction)ModelDb.Affliction<CloneFallbackAffliction>().ToMutable();
        var other = (CloneFallbackAffliction)ModelDb.Affliction<CloneFallbackAffliction>().ToMutable();
        attached._card = source;
        source.Affliction = attached;
        attached.LinkedModel = other;
        other.LinkedModel = source;
        attached.ReadLinked = attached.ReadLinkedModel;

        var first = ModelSnapshotCopier.Copy(source);
        var copiedAttachment = Assert.IsType<CloneFallbackAffliction>(first.Affliction);

        Assert.NotSame(source, first);
        Assert.NotSame(attached, copiedAttachment);
        Assert.Same(first, copiedAttachment.Card);
        Assert.Same(other, copiedAttachment.LinkedModel);
        Assert.Same(copiedAttachment, copiedAttachment.ReadLinked!.Target);
        Assert.Same(other, copiedAttachment.ReadLinked());

        other._card = source;
        source.Affliction = other;
        other.LinkedModel = attached;
        var second = ModelSnapshotCopier.Copy(source);
        var secondAttachment = Assert.IsType<CloneFallbackAffliction>(second.Affliction);
        Assert.NotSame(other, secondAttachment);
        Assert.Same(second, secondAttachment.Card);
        Assert.Same(attached, secondAttachment.LinkedModel);
        Assert.Same(copiedAttachment, first.Affliction);
        Assert.Same(other, copiedAttachment.LinkedModel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneAttachmentsRetainTheirCardAcrossPolicyReuse(bool cardFirst)
    {
        using var combat = new TestCombat();
        var source = combat.ArrangeCard<StrikeIronclad>();
        ModelDb.Inject(typeof(Adroit));
        ModelDb.Inject(typeof(Bound));
        var enchantment = ModelDb.Enchantment<Adroit>().ToMutable();
        enchantment._card = source;
        source.Enchantment = enchantment;
        var affliction = ModelDb.Affliction<Bound>().ToMutable();
        affliction._card = source;
        source.Affliction = affliction;
        var enchantmentVars = enchantment.DynamicVars;
        enchantmentVars.Block.PreviewValue = 27;

        void CopyCard()
        {
            var copy = ModelSnapshotCopier.Copy(source);
            Assert.Same(copy, copy.Enchantment!.Card);
            Assert.Same(copy, copy.Affliction!.Card);
        }

        void CopyAttachments()
        {
            var enchantmentCopy = ModelSnapshotCopier.Copy(enchantment);
            var afflictionCopy = ModelSnapshotCopier.Copy(affliction);
            Assert.NotSame(enchantment, enchantmentCopy);
            Assert.NotSame(affliction, afflictionCopy);
            Assert.NotSame(enchantmentVars, enchantmentCopy.DynamicVars);
            Assert.Same(enchantmentCopy, enchantmentCopy.DynamicVars.Block._owner);
            enchantmentCopy.DynamicVars.Block.PreviewValue = 5;
            Assert.Equal(27, enchantmentVars.Block.PreviewValue);
            Assert.Same(source, enchantmentCopy.Card);
            Assert.Same(source, afflictionCopy.Card);
        }

        if (cardFirst) CopyCard();
        CopyAttachments();
        CopyCard();
        CopyAttachments();
        Assert.Same(source, enchantment.Card);
        Assert.Same(source, affliction.Card);
    }

    [Fact]
    public void PotionSnapshotsIsolateDynamicVarsAndRetainTheirOwner()
    {
        using var combat = new TestCombat();
        ModelDb.Inject(typeof(StrengthPotion));
        var source = ModelDb.Potion<StrengthPotion>().ToMutable();
        source.Owner = combat.Player;
        source.DynamicVars.Strength.PreviewValue = 27;

        var first = ModelSnapshotCopier.Copy(source);
        var second = ModelSnapshotCopier.Copy(source);
        first.DynamicVars.Strength.PreviewValue = 5;

        Assert.Same(source.Owner, first.Owner);
        Assert.Same(source.CanonicalInstance, first.CanonicalInstance);
        Assert.Same(first, first.DynamicVars.Strength._owner);
        Assert.Same(second, second.DynamicVars.Strength._owner);
        Assert.Equal(27, source.DynamicVars.Strength.PreviewValue);
        Assert.Equal(27, second.DynamicVars.Strength.PreviewValue);
    }

    [Fact]
    public void SnapshotDoesNotInitializeLazyCardData()
    {
        ModelDb.Inject(typeof(StrikeIronclad));
        var source = ModelDb.Card<StrikeIronclad>().ToMutable();
        source._dynamicVars = null;
        source._energyCost = null;
        source._keywords = null;
        source._tags = null;

        var copy = ModelSnapshotCopier.Copy(source);

        Assert.Null(source._dynamicVars);
        Assert.Null(copy._dynamicVars);
        Assert.Null(source._energyCost);
        Assert.Null(copy._energyCost);
        Assert.Null(copy._keywords);
        Assert.Null(copy._tags);
    }
}

public sealed class CloneFallbackAffliction : AfflictionModel
{
    public List<int> Values = [];
    public bool RejectCloneHook;
    public AbstractModel? LinkedModel;
    public Func<AbstractModel?>? ReadLinked;
    public Func<AbstractModel?>? ExternalReadLinked;
    public event Action? ExtraChanged;

    public void Raise() => ExtraChanged?.Invoke();
    public AbstractModel? ReadLinkedModel() => LinkedModel;

    protected override void AfterCloned()
    {
        if (RejectCloneHook) throw new InvalidOperationException("Snapshot must not invoke clone hooks.");
        base.AfterCloned();
        ExtraChanged = null;
    }
}
