using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.OutOfCombat.Events;

namespace RandomForeseer.OutOfCombat;

internal static class EventOptionPrediction
{
    private static readonly Dictionary<Type, Func<EventModel, EventOption, IReadOnlyList<IHoverTip>>> Predictors = [];

    static EventOptionPrediction()
    {
        Register<AromaOfChaos>(AromaOfChaosPrediction.GetHoverTips);
        Register<BattlewornDummy>(BattlewornDummyPrediction.GetHoverTips);
        Register<BrainLeech>(BrainLeechPrediction.GetHoverTips);
        Register<ColorfulPhilosophers>(ColorfulPhilosophersPrediction.GetHoverTips);
        Register<DenseVegetation>(DenseVegetationPrediction.GetHoverTips);
        Register<DollRoom>(DollRoomPrediction.GetHoverTips);
        Register<DoorsOfLightAndDark>(DoorsOfLightAndDarkPrediction.GetHoverTips);
        Register<EndlessConveyor>(EndlessConveyorPrediction.GetHoverTips);
        Register<InfestedAutomaton>(InfestedAutomatonPrediction.GetHoverTips);
        Register<LuminousChoir>(LuminousChoirPrediction.GetHoverTips);
        Register<MorphicGrove>(MorphicGrovePrediction.GetHoverTips);
        Register<PotionCourier>(PotionCourierPrediction.GetHoverTips);
        Register<PunchOff>(PunchOffPrediction.GetHoverTips);
        Register<RanwidTheElder>(RanwidTheElderPrediction.GetHoverTips);
        Register<Reflections>(ReflectionsPrediction.GetHoverTips);
        Register<RoomFullOfCheese>(RoomFullOfCheesePrediction.GetHoverTips);
        Register<RoundTeaParty>(RoundTeaPartyPrediction.GetHoverTips);
        Register<SlipperyBridge>(SlipperyBridgePrediction.GetHoverTips);
        Register<Symbiote>(SymbiotePrediction.GetHoverTips);
        Register<TabletOfTruth>(TabletOfTruthPrediction.GetHoverTips);
        Register<TheFutureOfPotions>(TheFutureOfPotionsPrediction.GetHoverTips);
        Register<TheLegendsWereTrue>(TheLegendsWereTruePrediction.GetHoverTips);
        Register<ThisOrThat>(ThisOrThatPrediction.GetHoverTips);
        Register<TinkerTime>(TinkerTimePrediction.GetHoverTips);
        Register<TrashHeap>(TrashHeapPrediction.GetHoverTips);
        Register<Trial>(TrialPrediction.GetHoverTips);
        Register<UnrestSite>(UnrestSitePrediction.GetHoverTips);
        Register<WarHistorianRepy>(WarHistorianRepyPrediction.GetHoverTips);
        Register<WelcomeToWongos>(WelcomeToWongosPrediction.GetHoverTips);
        Register<Wellspring>(WellspringPrediction.GetHoverTips);
        Register<WhisperingHollow>(WhisperingHollowPrediction.GetHoverTips);
    }

    private static void Register<TEvent>(Func<TEvent, EventOption, IReadOnlyList<IHoverTip>> predictor)
        where TEvent : EventModel
    {
        var eventType = typeof(TEvent);
        if (!Predictors.TryAdd(eventType, (eventModel, option) => predictor((TEvent)eventModel, option)))
        {
            Entry.Logger.Warn($"Duplicate event option prediction registration ignored: {eventType}");
        }
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
            Predictors.TryGetValue(eventModel.GetType(), out var predictor))
        {
            try
            {
                tips.AddRange(predictor(eventModel, option));
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Event option prediction failed for {eventModel.Id} {option.TextKey}: {ex}");
            }
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
