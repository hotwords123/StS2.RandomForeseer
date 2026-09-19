using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.Tests.Infrastructure;

/// <summary>Arranges a source combat, then owns one explicit <see cref="CombatPredictionSession"/>.</summary>
/// <remarks>
/// Complete source setup with the Arrange helpers and proxy before calling <see cref="BeginPrediction"/>.
/// Inject any additional model dependencies with <see cref="ModelDb.Inject"/> during arrangement.
/// After startup, change prediction state through simulator commands/helpers; generate cards through simulator
/// generation commands rather than adding source cards. Raw source fields, collections and global history remain
/// accessible for assertions but must not be changed to extend the scenario after startup. These references have
/// no runtime write protection, and this protocol does not make legacy lazy state an eager snapshot.
/// Use a using scope to release the owned session on both normal and exceptional exits.
/// </remarks>
internal sealed class TestCombat : IDisposable
{
    public Player Player { get; } = CreatePlayer(1);
    public Player OtherPlayer { get; } = CreatePlayer(2);
    public Creature Enemy => Proxy.Enemies[0];
    public CombatStateProxy Proxy { get; }
    private CombatPredictionSession? _session;
    private bool _begun;
    private bool _disposed;
    /// <summary>Gets the source combat for arrangement and identity assertions.</summary>
    /// <remarks>
    /// Session lifecycle tests may use this fixture only as a source builder and create/dispose their own
    /// <see cref="CombatPredictionSession"/> instances. Finish all source setup before the first session is created;
    /// do not construct a simulator directly. External sessions are not owned or tracked by this fixture.
    /// </remarks>
    public ICombatState Source { get; }
    /// <summary>Gets the simulator owned by the active prediction session.</summary>
    /// <exception cref="InvalidOperationException">Prediction has not been started.</exception>
    /// <exception cref="ObjectDisposedException">The fixture has been disposed.</exception>
    public CombatPredictionSimulator Simulator
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _session?.Simulator
                ?? throw new InvalidOperationException("Call BeginPrediction before accessing prediction state.");
        }
    }
    /// <summary>Gets the primary player's prediction state through <see cref="Simulator"/>.</summary>
    /// <remarks>Requires a started, undisposed fixture, just like <see cref="Simulator"/>.</remarks>
    public SimPlayerCombatState PlayerState => Simulator.State.GetPlayerCombatState(Player);

    /// <summary>Ends source arrangement and creates the fixture's single prediction session.</summary>
    /// <remarks>Arrange helpers and proxy collection setters reject further setup after this call.</remarks>
    /// <exception cref="InvalidOperationException">Prediction startup has already been attempted.</exception>
    /// <exception cref="ObjectDisposedException">The fixture has been disposed.</exception>
    public void BeginPrediction()
    {
        EnsureArrange();
        _begun = true;
        _session = new CombatPredictionSession(Source);
    }

    private void EnsureArrange()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_begun) throw new InvalidOperationException("Source arrangement must finish before BeginPrediction.");
    }

    /// <summary>Resolves an arranged source card in the active prediction's owner piles.</summary>
    /// <exception cref="InvalidOperationException">Prediction has not started or the card is absent from its owner piles.</exception>
    /// <exception cref="ObjectDisposedException">The fixture has been disposed.</exception>
    public PredictedCard Predicted(CardModel source) =>
        Simulator.State.GetPlayerCombatState(source.Owner).FindCard(source)
        ?? throw new InvalidOperationException("The arranged card is not in this prediction.");

    /// <summary>Idempotently releases the owned session and prevents further startup or prediction access.</summary>
    /// <remarks>Does not revoke previously obtained model/simulator references or dispose external sessions.</remarks>
    public void Dispose()
    {
        _session?.Dispose();
        _disposed = true;
    }

    /// <summary>Creates source players and combat without starting a simulator.</summary>
    public TestCombat(int enemyCount = 1)
    {
        var combat = DispatchProxy.Create<ICombatState, CombatStateProxy>();
        Source = combat;
        Proxy = (CombatStateProxy)combat;
        Proxy.EnsureArrange = EnsureArrange;
        Proxy.Allies = [Player.Creature, OtherPlayer.Creature];
        Proxy.Enemies = Enumerable.Range(1, enemyCount).Select(index =>
        {
            var creature = new Creature(null!, 100, 100) { CombatId = (uint)index };
            SetBackingField(creature, nameof(Creature.Side), CombatSide.Enemy);
            return creature;
        }).ToArray();
        Player.Creature.CombatId = 0;
        OtherPlayer.Creature.CombatId = (uint)(enemyCount + 1);
        foreach (var creature in Proxy.Allies.Concat(Proxy.Enemies)) creature.CombatState = combat;
    }

    /// <summary>Creates a source card of type <typeparamref name="T"/> using <see cref="ArrangeCard(Type, Player, PileType)"/>.</summary>
    public CardModel ArrangeCard<T>(Player? owner = null, PileType pile = PileType.Hand) where T : CardModel =>
        ArrangeCard(typeof(T), owner, pile);

    /// <summary>Creates a mutable source card and places it in the specified source pile before prediction starts.</summary>
    /// <remarks>
    /// Does not create a preview. Configure initial upgrades, costs and private fields on the returned source card
    /// before <see cref="BeginPrediction"/>, then use <see cref="Predicted"/> to resolve its prediction counterpart.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Prediction startup has already been attempted.</exception>
    /// <exception cref="ObjectDisposedException">The fixture has been disposed.</exception>
    public CardModel ArrangeCard(Type type, Player? owner = null, PileType pile = PileType.Hand)
    {
        EnsureArrange();
        ModelDb.Inject(type);
        var original = ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();
        original.Owner = owner ?? Player;
        // Arrange the source pile directly: CardPile.AddInternal can subscribe to the real combat tracker.
        (CardPile.Get(pile, original.Owner) ?? throw new ArgumentException("A source pile is required.", nameof(pile)))
            ._cards.Add(original);
        return original;
    }

    /// <summary>Creates a local Power mirror receiver during source arrangement.</summary>
    /// <param name="owner">Source owner of the Power.</param>
    /// <param name="amount">Initial source amount.</param>
    /// <param name="addToLiveCollection">Whether to register the Power in the owner's source collection for dispatch tests.</param>
    /// <remarks>Requires an undisposed fixture before <see cref="BeginPrediction"/>; otherwise throws.</remarks>
    public T ArrangePower<T>(Creature owner, int amount, bool addToLiveCollection = false) where T : PowerModel
    {
        EnsureArrange();
        ModelDb.Inject(typeof(T));
        var power = (T)ModelDb.Power<T>().ToMutable();
        power._owner = owner;
        power._amount = amount;
        if (addToLiveCollection) owner._powers.Add(power);
        return power;
    }

    /// <summary>Creates a local Relic mirror receiver during source arrangement.</summary>
    /// <remarks>
    /// Does not register a listener automatically; dispatch tests must also configure
    /// <see cref="CombatStateProxy.Listeners"/>. Requires an undisposed fixture before <see cref="BeginPrediction"/>.
    /// </remarks>
    public T ArrangeRelic<T>(Player owner) where T : RelicModel
    {
        EnsureArrange();
        ModelDb.Inject(typeof(T));
        var relic = (T)ModelDb.Relic<T>().ToMutable();
        relic._owner = owner;
        return relic;
    }

    public static CardPlay Play(PredictedCard card, int index = 0, int energySpent = 0, Creature? target = null) => new()
    {
        Card = card.Preview,
        Player = card.Preview.Owner,
        Target = target,
        ResultPile = PileType.Discard,
        Resources = new ResourceInfo { EnergySpent = energySpent, EnergyValue = energySpent, StarsSpent = 0, StarValue = 0 },
        IsAutoPlay = false,
        PlayIndex = index,
        PlayCount = 1
    };

    public void Start(PredictedCard card) => Simulator.History.CardPlayStarted(card, Play(card));
    public void Finish(PredictedCard card) => Simulator.History.CardPlayFinished(card, Play(card), false);
    /// <summary>Adds a source card-play entry to global combat history before prediction starts.</summary>
    /// <remarks>
    /// Requires an undisposed fixture before <see cref="BeginPrediction"/>. During prediction, local recorder tests
    /// use <see cref="Start"/> / <see cref="Finish"/> instead; these do not execute the complete card lifecycle.
    /// </remarks>
    public void ArrangeHistory(CardModel card, bool finished = false, int round = 1)
    {
        EnsureArrange();
        var history = CombatManager.Instance.History;
        history._entries.Add(finished
            ? new CardPlayFinishedEntry(SourcePlay(card), round, CombatSide.Player, history, [card.Owner])
            : new CardPlayStartedEntry(SourcePlay(card), round, CombatSide.Player, history, [card.Owner]));
    }

    private static CardPlay SourcePlay(CardModel card) => new()
    {
        Card = card, Player = card.Owner, Target = null, ResultPile = PileType.Discard,
        Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
        IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
    };

    public int Amount(PowerModel power) => Simulator.StateStore.GetPowerAmount(power).Amount;

    public static void SetBackingField(object instance, string property, object value) =>
        // Compiler-generated fields are deliberately excluded from Publicizer in both projects.
        AccessTools.Field(instance.GetType(), $"<{property}>k__BackingField").SetValue(instance, value);

    private static Player CreatePlayer(ulong id)
    {
        // Player's full constructor initializes run/UI services outside headless combat tests.
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        SetBackingField(player, nameof(Player.NetId), id);
        SetBackingField(player, nameof(Player.Creature), new Creature(player, 100, 100));
        SetBackingField(player, nameof(Player.Character), new Ironclad());
        player._runPiles = [];
        // Readonly collection on an uninitialized Player cannot be assigned through Publicizer.
        AccessTools.Field(typeof(Player), "_relics").SetValue(player, new List<RelicModel>());
        SetBackingField(player, nameof(Player.PlayerCombatState), new PlayerCombatState(player));
        return player;
    }
}
