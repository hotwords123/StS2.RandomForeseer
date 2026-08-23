using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.Localization;
using RandomForeseer.RandomForeseerCode.Telemetry;
using STS2RitsuLib.Utils.HarmonyIl;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NCardPileScreen))]
internal static class FrozenEyeCardPileScreenPatches
{
    private static readonly ConditionalWeakTable<NCardPileScreen, ScreenRefreshCallback> RefreshCallbacks = [];

    [HarmonyPatch("OnPileContentsChanged")]
    [HarmonyPrefix]
    private static bool RefreshDrawPileView(NCardPileScreen __instance)
    {
        try
        {
            return !FrozenEyeDrawPileView.TryRefresh(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Frozen Eye draw pile view refresh failed: {ex}");
            ModTelemetry.CaptureException(ex, "frozen_eye_patch", "refresh_draw_pile_view");
            return true;
        }
    }

    [HarmonyPatch(nameof(NCardPileScreen._EnterTree))]
    [HarmonyPostfix]
    private static void SubscribeDiscardPileChanges(NCardPileScreen __instance)
    {
        if (!CardPileUtils.TryGetDrawPileOwner(__instance.Pile, out var player) ||
            player.PlayerCombatState is not { } playerCombatState)
        {
            return;
        }

        var callback = RefreshCallbacks.GetValue(__instance, screen => new ScreenRefreshCallback(screen));
        playerCombatState.DiscardPile.ContentsChanged -= callback.Refresh;
        playerCombatState.DiscardPile.ContentsChanged += callback.Refresh;
    }

    [HarmonyPatch(nameof(NCardPileScreen._ExitTree))]
    [HarmonyPostfix]
    private static void UnsubscribeDiscardPileChanges(NCardPileScreen __instance)
    {
        if (!CardPileUtils.TryGetDrawPileOwner(__instance.Pile, out var player) ||
            player.PlayerCombatState is not { } playerCombatState)
        {
            return;
        }

        if (RefreshCallbacks.TryGetValue(__instance, out var callback))
        {
            playerCombatState.DiscardPile.ContentsChanged -= callback.Refresh;
        }
    }

    [HarmonyPatch(nameof(NCardPileScreen.AfterCapstoneOpened))]
    [HarmonyPostfix]
    private static void RefreshAfterOpened(NCardPileScreen __instance)
    {
        __instance.OnPileContentsChanged();
    }

    private sealed class ScreenRefreshCallback(NCardPileScreen screen)
    {
        public void Refresh()
        {
            if (!ModData.Settings.ShufflePredictionEnabled)
            {
                return;
            }

            FrozenEyeDrawPileView.TryRefresh(screen);
        }
    }
}

[HarmonyPatch(typeof(NCombatCardPile), "OnRelease")]
internal static class FrozenEyeEmptyDrawPileOpenPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase original)
    {
        var instructionList = instructions.ToList();

        try
        {
            var rewriter = HarmonyIlRewriter.From(instructionList, original);
            rewriter
                .ReplaceCall(
                    "allow empty draw pile view when shuffle prediction can be shown",
                    AccessTools.PropertyGetter(typeof(CardPile), nameof(CardPile.IsEmpty)),
                    AccessTools.Method(typeof(FrozenEyeEmptyDrawPileOpenPatch), nameof(ShouldTreatPileAsEmpty)))
                .RequireExactly(1);
            return rewriter.InstructionsChecked("Frozen Eye empty draw pile open");
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Frozen Eye empty draw pile transpiler failed for {original.FullDescription()}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "frozen_eye_patch",
                "rewrite_empty_draw_pile_open",
                TelemetryContext.ForMethod(original));
            return instructionList;
        }
    }

    private static bool ShouldTreatPileAsEmpty(CardPile pile)
    {
        if (!pile.IsEmpty)
        {
            return false;
        }

        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.FrozenEyeEnabled || !settings.ShufflePredictionEnabled ||
            !CardPileUtils.TryGetDrawPileOwner(pile, out var player) ||
            player.Creature.CombatState?.CurrentSide != player.Creature.Side)
        {
            return true;
        }

        return player.PlayerCombatState?.DiscardPile.IsEmpty ?? true;
    }
}

[HarmonyPatch(typeof(NCardGrid))]
internal static class FrozenEyeCardGridModulatePatch
{
    private static readonly Color PredictedShuffleCardModulate = new(0.65f, 0.65f, 0.65f);
    private static readonly ConditionalWeakTable<NCard, StrongBox<Color>> OriginalModulates = [];

    [HarmonyPatch(nameof(NCardGrid.InitGrid), [])]
    [HarmonyPostfix]
    private static void ApplyOnInitGrid(NCardGrid __instance)
    {
        ApplyPredictionModulate(__instance, __instance.CurrentlyDisplayedCardHolders);
    }

    [HarmonyPatch("AssignCardsToRow")]
    [HarmonyPostfix]
    private static void ApplyOnAssignCardsToRow(NCardGrid __instance, List<NGridCardHolder> row)
    {
        ApplyPredictionModulate(__instance, row);
    }

    private static void ApplyPredictionModulate(NCardGrid grid, IEnumerable<NGridCardHolder> holders)
    {
        if (!FrozenEyeDrawPileViewState.TryGetPredictedShuffleCards(grid, out var predictedCards))
        {
            return;
        }

        foreach (var holder in holders)
        {
            if (holder is { Visible: true, CardModel: { } card } && predictedCards.Contains(card))
            {
                ApplyPredictedModulate(holder);
            }
            else
            {
                RestoreModulate(holder);
            }
        }
    }

    private static void ApplyPredictedModulate(NGridCardHolder holder)
    {
        if (holder.CardNode is not { } cardNode)
        {
            return;
        }

        if (!OriginalModulates.TryGetValue(cardNode, out _))
        {
            OriginalModulates.Add(cardNode, new StrongBox<Color>(cardNode.Modulate));
        }

        cardNode.Modulate = PredictedShuffleCardModulate;
    }

    private static void RestoreModulate(NGridCardHolder holder)
    {
        if (holder.CardNode is not { } cardNode ||
            !OriginalModulates.TryGetValue(cardNode, out var state))
        {
            return;
        }

        cardNode.Modulate = state.Value;
        OriginalModulates.Remove(cardNode);
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class FrozenEyeDrawPileRawTextPatch
{
    private static void Postfix(LocString __instance, ref string __result)
    {
        try
        {
            __result = (__instance.LocTable, __instance.LocEntryKey) switch
            {
                ("static_hover_tips", "DRAW_PILE.description") => ReplaceDrawPileDescription(__result),
                ("gameplay_ui", "DRAW_PILE_INFO") => ReplaceDrawPileInfo(__result),
                _ => __result
            };
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Failed to replace raw text for {__instance}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "frozen_eye_patch",
                "replace_draw_pile_text",
                new { __instance.LocTable, __instance.LocEntryKey });
        }
    }

    private static string ReplaceDrawPileDescription(string text)
    {
        var settings = ModData.Settings;
        if (settings is not { IsPredictionEnabled: true, FrozenEyeEnabled: true })
        {
            return text;
        }

        var mainDescription = text.Split("\n\n", 2)[0];
        var viewDescription = ModLocalization.Text("frozen_eye.draw_pile_hover_view").GetRawText();

        return $"{mainDescription}\n\n{viewDescription}";
    }

    private static string ReplaceDrawPileInfo(string text)
    {
        var settings = ModData.Settings;
        if (settings is not { IsPredictionEnabled: true, FrozenEyeEnabled: true })
        {
            return text;
        }

        var firstLine = text.Split('\n', 2)[0];
        var orderInfoKey = settings.ShufflePredictionEnabled
            ? "frozen_eye.draw_pile_info_order_with_shuffle_prediction"
            : "frozen_eye.draw_pile_info_order";
        var orderInfo = ModLocalization.Text(orderInfoKey).GetRawText();

        return $"{firstLine}\n{orderInfo}";
    }
}
