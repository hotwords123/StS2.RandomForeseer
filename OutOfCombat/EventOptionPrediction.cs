using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.OutOfCombat.Events;

namespace RandomForeseer.OutOfCombat;

internal sealed class EventPredictionRegistry
{
    private readonly Dictionary<Type, Func<EventModel, EventOption, IReadOnlyList<IHoverTip>>> _predictors = [];

    public void Register<TEvent>(Func<TEvent, EventOption, IReadOnlyList<IHoverTip>> predictor)
        where TEvent : EventModel
    {
        var eventType = typeof(TEvent);
        if (!_predictors.TryAdd(eventType, (eventModel, option) => predictor((TEvent)eventModel, option)))
        {
            Entry.Logger.Warn($"Duplicate event option prediction registration ignored: {eventType}");
        }
    }

    public bool TryPredict(EventModel eventModel, EventOption option, out IReadOnlyList<IHoverTip> hoverTips)
    {
        if (_predictors.TryGetValue(eventModel.GetType(), out var predictor))
        {
            try
            {
                hoverTips = predictor(eventModel, option);
                return true;
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Event option prediction failed for {eventModel.Id} {option.TextKey}: {ex}");
            }
        }

        hoverTips = [];
        return false;
    }
}

internal static class EventOptionPrediction
{
    private static readonly EventPredictionRegistry Registry = CreateRegistry();

    private static EventPredictionRegistry CreateRegistry()
    {
        var registry = new EventPredictionRegistry();

        registry.Register<AromaOfChaos>(AromaOfChaosPrediction.GetHoverTips);
        registry.Register<BattlewornDummy>(BattlewornDummyPrediction.GetHoverTips);
        registry.Register<BrainLeech>(BrainLeechPrediction.GetHoverTips);
        registry.Register<ColorfulPhilosophers>(ColorfulPhilosophersPrediction.GetHoverTips);
        registry.Register<DenseVegetation>(DenseVegetationPrediction.GetHoverTips);
        registry.Register<DollRoom>(DollRoomPrediction.GetHoverTips);
        registry.Register<DoorsOfLightAndDark>(DoorsOfLightAndDarkPrediction.GetHoverTips);
        registry.Register<EndlessConveyor>(EndlessConveyorPrediction.GetHoverTips);
        registry.Register<InfestedAutomaton>(InfestedAutomatonPrediction.GetHoverTips);
        registry.Register<LuminousChoir>(LuminousChoirPrediction.GetHoverTips);
        registry.Register<MorphicGrove>(MorphicGrovePrediction.GetHoverTips);
        registry.Register<PotionCourier>(PotionCourierPrediction.GetHoverTips);
        registry.Register<PunchOff>(PunchOffPrediction.GetHoverTips);
        registry.Register<RanwidTheElder>(RanwidTheElderPrediction.GetHoverTips);
        registry.Register<Reflections>(ReflectionsPrediction.GetHoverTips);
        registry.Register<RoomFullOfCheese>(RoomFullOfCheesePrediction.GetHoverTips);
        registry.Register<RoundTeaParty>(RoundTeaPartyPrediction.GetHoverTips);
        registry.Register<SlipperyBridge>(SlipperyBridgePrediction.GetHoverTips);
        registry.Register<Symbiote>(SymbiotePrediction.GetHoverTips);
        registry.Register<TabletOfTruth>(TabletOfTruthPrediction.GetHoverTips);
        registry.Register<TheFutureOfPotions>(TheFutureOfPotionsPrediction.GetHoverTips);
        registry.Register<TheLegendsWereTrue>(TheLegendsWereTruePrediction.GetHoverTips);
        registry.Register<ThisOrThat>(ThisOrThatPrediction.GetHoverTips);
        registry.Register<TinkerTime>(TinkerTimePrediction.GetHoverTips);
        registry.Register<TrashHeap>(TrashHeapPrediction.GetHoverTips);
        registry.Register<Trial>(TrialPrediction.GetHoverTips);
        registry.Register<UnrestSite>(UnrestSitePrediction.GetHoverTips);
        registry.Register<WarHistorianRepy>(WarHistorianRepyPrediction.GetHoverTips);
        registry.Register<WelcomeToWongos>(WelcomeToWongosPrediction.GetHoverTips);
        registry.Register<Wellspring>(WellspringPrediction.GetHoverTips);
        registry.Register<WhisperingHollow>(WhisperingHollowPrediction.GetHoverTips);

        return registry;
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(EventModel eventModel, EventOption option)
    {
        if (eventModel.Owner == null || option.IsLocked)
        {
            return [];
        }

        var tips = new List<IHoverTip>();

        if (option.Relic != null)
        {
            tips.AddRange(RelicPickupPrediction.GetHoverTips(eventModel.Owner, option.Relic));
        }

        if (RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableEventOptionPrediction) &&
            Registry.TryPredict(eventModel, option, out var hoverTips))
        {
            tips.AddRange(hoverTips);
        }

        return tips;
    }
}

internal static class EventOptionEventModelMap
{
    private static readonly ConditionalWeakTable<EventOption, EventModel> EventModels = [];

    public static void Register(EventOption option, EventModel eventModel) => EventModels.AddOrUpdate(option, eventModel);

    public static bool TryGetEventModel(EventOption option, out EventModel eventModel) =>
        EventModels.TryGetValue(option, out eventModel!);
}

[HarmonyPatch(typeof(EventOption), "AddLocVars")]
internal static class EventOptionAddLocVarsPatch
{
    private static void Postfix(EventOption __instance, EventModel eventModel)
    {
        EventOptionEventModelMap.Register(__instance, eventModel);
    }
}

[HarmonyPatch(typeof(EventOption), nameof(EventOption.HoverTips), MethodType.Getter)]
internal static class EventOptionPredictionHoverTipsPatch
{
    private static void Postfix(EventOption __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!EventOptionEventModelMap.TryGetEventModel(__instance, out var eventModel))
        {
            return;
        }

        var predictionTips = EventOptionPrediction.GetHoverTips(eventModel, __instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
