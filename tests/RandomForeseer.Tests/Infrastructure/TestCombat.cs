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
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.Tests.Infrastructure;

internal sealed class TestCombat
{
    public Player Player { get; } = CreatePlayer(1);
    public Player OtherPlayer { get; } = CreatePlayer(2);
    public Creature Enemy => Proxy.Enemies[0];
    public CombatStateProxy Proxy { get; }
    public CombatPredictionSimulator Simulator { get; }
    public SimPlayerCombatState PlayerState => Simulator.State.GetPlayerCombatState(Player);

    public TestCombat(int enemyCount = 1)
    {
        var combat = DispatchProxy.Create<ICombatState, CombatStateProxy>();
        Proxy = (CombatStateProxy)combat;
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
        Simulator = new CombatPredictionSimulator(combat);
    }

    public PredictedCard Card<T>(Player? owner = null, PileType pile = PileType.Hand) where T : CardModel =>
        Card(typeof(T), owner, pile);

    public PredictedCard Card(Type type, Player? owner = null, PileType pile = PileType.Hand)
    {
        ModelDb.Inject(type);
        var original = ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();
        original.Owner = owner ?? Player;
        var card = new PredictedCard(original, (CardModel)original.MutableClone());
        Simulator.State.GetPlayerCombatState(original.Owner).GetCardPile(pile)!.Add(card);
        return card;
    }

    public static T Power<T>(Creature owner, int amount, bool addToLiveCollection = false) where T : PowerModel
    {
        ModelDb.Inject(typeof(T));
        var power = (T)ModelDb.Power<T>().ToMutable();
        power._owner = owner;
        power._amount = amount;
        if (addToLiveCollection) owner._powers.Add(power);
        return power;
    }

    public static T Relic<T>(Player owner) where T : RelicModel
    {
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
    public void LiveHistory(PredictedCard card, bool finished = false, int round = 1)
    {
        var history = CombatManager.Instance.History;
        history._entries.Add(finished
            ? new CardPlayFinishedEntry(Play(card), round, CombatSide.Player, history, [card.Preview.Owner])
            : new CardPlayStartedEntry(Play(card), round, CombatSide.Player, history, [card.Preview.Owner]));
    }

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
