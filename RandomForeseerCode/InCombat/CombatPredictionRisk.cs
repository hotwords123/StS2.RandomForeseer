using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;
using RandomForeseer.Common.HoverTips;
using RandomForeseer.Data;
using RandomForeseer.InCombat.Extensions;
using RandomForeseer.InCombat.Simulation;
using RandomForeseer.Localization;

namespace RandomForeseer.InCombat;

/// <summary>
/// Preserves combat risk entries through a projection boundary and creates their compact player warning.
/// </summary>
internal sealed class CombatPredictionRisk(IReadOnlyList<CombatPredictionRiskEntry> entries) : IPredictionRisk
{
    private const int MaxModelNamesPerLine = 2;

    public IReadOnlyList<CombatPredictionRiskEntry> Entries { get; } = entries;

    public bool HasRisk => Entries.Count > 0;

    public bool HasVisibleRisk => Entries.Any(static entry => entry.Reason.ShouldShow(ModData.Settings));

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        List<AbstractModel?> incompleteModels = [];
        List<AbstractModel?> playerChoiceModels = [];
        var cardDrawLimitExceeded = false;
        var orbChannelLimitExceeded = false;

        foreach (var entry in Entries)
        {
            if (!entry.Reason.ShouldShow(ModData.Settings))
            {
                continue;
            }

            switch (entry.Reason)
            {
                case PredictionRiskReason.MethodNotMirrored:
                case PredictionRiskReason.MethodMirrorIncomplete:
                    var trace = entry.Trace?.FindOriginatingEffect() ?? entry.Trace;
                    incompleteModels.Add(trace?.Source);
                    break;

                case PredictionRiskReason.UnresolvedPlayerChoice:
                    playerChoiceModels.Add(entry.Trace?.Source);
                    break;

                case PredictionRiskReason.CardDrawLimitExceeded:
                    cardDrawLimitExceeded = true;
                    break;

                case PredictionRiskReason.OrbChannelLimitExceeded:
                    orbChannelLimitExceeded = true;
                    break;
            }
        }

        List<string> lines = [];

        if (incompleteModels.Count > 0)
        {
            AddModelRiskLine(lines, "incomplete", incompleteModels);
        }

        if (playerChoiceModels.Count > 0)
        {
            AddModelRiskLine(lines, "player_choice", playerChoiceModels);
        }

        if (cardDrawLimitExceeded)
        {
            lines.Add(ModLocalization.Text("drift_warning.card_draw_limit").GetFormattedText());
        }

        if (orbChannelLimitExceeded)
        {
            lines.Add(ModLocalization.Text("drift_warning.orb_channel_limit").GetFormattedText());
        }

        if (lines.Count == 0)
        {
            return [];
        }

        var tip = PredictionHoverTipFactory.Text("drift_warning", description =>
        {
            description.Add("Lines", lines);
        });
        return [tip];
    }

    private static void AddModelRiskLine(List<string> lines, string localizationKey, List<AbstractModel?> models)
    {
        var hasUnknownModels = models.Any(static model => model is null);
        var modelNames = models
            .OfType<AbstractModel>()
            .DistinctBy(static model => model.Id)
            .Select(static entry => entry.GetTitle())
            .ToList();
        if (modelNames.Count == 0)
        {
            lines.Add(ModLocalization.Text($"drift_warning.{localizationKey}_unknown").GetFormattedText());
            return;
        }

        var shownModelNames = modelNames.Take(MaxModelNamesPerLine).ToList();
        var keySuffix = hasUnknownModels || modelNames.Count > MaxModelNamesPerLine ? "_more" : string.Empty;
        var line = ModLocalization.Text($"drift_warning.{localizationKey}{keySuffix}");
        line.Add("Models", shownModelNames);
        lines.Add(line.GetFormattedText());
    }
}
