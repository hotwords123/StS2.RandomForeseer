using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(NDeckTransformSelectScreen))]
internal static class DeckTransformSelectionScreenPatches
{
    [HarmonyPatch("OnCardClicked")]
    [HarmonyPostfix]
    private static void RefreshHoverTips(NDeckTransformSelectScreen __instance, CardModel card)
    {
        var holder = __instance._grid.GetCardHolder(card);
        if (holder is not { _isHovered: true })
        {
            return;
        }

        NHoverTipSet.Remove(holder);
        if (!__instance._previewContainer.Visible)
        {
            holder.Call(NCardHolder.MethodName.CreateHoverTips);
        }
    }

    [HarmonyPatch(nameof(NDeckTransformSelectScreen.OpenPreviewScreen))]
    [HarmonyPrefix]
    private static void ResetPredictor(NDeckTransformSelectScreen __instance)
    {
        // A canceled preview can reopen with the same delegate. Realign its private RNG before replaying selections.
        (__instance._cardToTransformation.Target as DeckTransformPreviewPredictor)?.Reset();
    }
}

[HarmonyPatch(
    typeof(CardSelectCmd),
    nameof(CardSelectCmd.FromDeckForTransformation),
    typeof(Player),
    typeof(CardSelectorPrefs),
    typeof(Func<CardModel, CardTransformation>))]
internal static class DeckTransformPredictionInjectionPatch
{
    [HarmonyPrefix]
    private static void InjectPredictor(
        CardSelectorPrefs prefs,
        ref Func<CardModel, CardTransformation>? cardToTransformation)
    {
        // Patch this boundary before vanilla replaces null with its default transformation factory.
        // Explicit factories may implement different transform rules and must remain untouched.
        var source = DeckTransformPredictionContext.CurrentSource;
        if (cardToTransformation is not null || source is null)
        {
            return;
        }

        try
        {
            if (DeckTransformPrediction.TryCreatePredictor(prefs, source, out var predictor))
            {
                cardToTransformation = predictor.PredictNext;
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Failed to inject deck transform preview predictor: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "deck_transform_prediction",
                "inject_preview_predictor",
                CreateTelemetryContext(source));
        }
    }

    private static object? CreateTelemetryContext(DeckTransformPredictionSource source)
    {
        return source switch
        {
            DeckTransformPredictionSource.Relic relicSource => new
            {
                Relic = TelemetryContext.ForModel(relicSource.Model)
            },
            DeckTransformPredictionSource.Event eventSource => new
            {
                Event = TelemetryContext.ForModel(eventSource.Model),
                Option = eventSource.Option.TextKey
            },
            _ => null
        };
    }
}

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), typeof(RelicModel), typeof(Player), typeof(int))]
internal static class DeckTransformPredictionRelicSourcePatch
{
    [HarmonyPrefix]
    private static void EnterSource(RelicModel relic, out IDisposable? __state)
    {
        __state = DeckTransformPredictionContext.EnterRelic(relic);
    }

    [HarmonyFinalizer]
    private static void RestoreCallerSource(IDisposable? __state)
    {
        // The async original captured this source in its ExecutionContext before returning its Task.
        // Restore the caller immediately so unrelated work started before Task completion cannot inherit it.
        __state?.Dispose();
    }
}

[HarmonyPatch(typeof(EventOption), nameof(EventOption.Chosen))]
internal static class DeckTransformPredictionEventOptionSourcePatch
{
    [HarmonyPrefix]
    private static void EnterSource(EventOption __instance, out IDisposable? __state)
    {
        __state = DeckTransformPredictionContext.EnterEventOption(__instance);
    }

    [HarmonyFinalizer]
    private static void RestoreCallerSource(IDisposable? __state)
    {
        // EventOption.Chosen captures the source for its continuations before returning its Task.
        __state?.Dispose();
    }
}
