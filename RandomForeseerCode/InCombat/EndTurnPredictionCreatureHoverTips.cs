using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class EndTurnPredictionCreatureHoverTips
{
    private const string BlockStatusNone = "none";
    private const string BlockStatusBlocked = "blocked";
    private const string BlockStatusPartial = "partial";

    private static readonly Dictionary<Creature, IReadOnlyList<IHoverTip>> TipsByTarget = [];

    public static void Set(DamagePredictionResult prediction)
    {
        TipsByTarget.Clear();

        var warningTips = new List<IHoverTip>();
        PredictionHoverTips.AddDriftWarningIfNeeded(warningTips, "end_turn", prediction.Risk);

        foreach (var target in prediction.Targets)
        {
            if (target.DamageLines.Count == 0)
            {
                continue;
            }

            var detailTip = PredictionHoverTips.Text("end_turn_damage_details", description =>
            {
                description.Add("Creature", target.Target.Name);
                description.Add("TotalDamage", target.TotalDamage);
                description.Add("TotalUnblockedDamage", target.TotalUnblockedDamage);
                description.Add("BlockStatus", GetBlockStatus(target.TotalDamage, target.TotalUnblockedDamage));
                description.Add("WasTargetKilled", target.WasTargetKilled);
                description.Add("Lines", target.DamageLines.Select(FormatDamageLine).ToList());
            });

            TipsByTarget[target.Target] = warningTips.Prepend(detailTip).ToArray();
        }
    }

    public static void Clear()
    {
        TipsByTarget.Clear();
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Creature creature)
    {
        return TipsByTarget.TryGetValue(creature, out var tips)
            ? tips
            : [];
    }

    private static string FormatDamageLine(DamagePredictionLine line)
    {
        var loc = PredictionLocalization.Text("end_turn_damage_details.line");
        loc.Add("Source", GetSourceName(line.SourceModel));
        loc.Add("Damage", line.Damage);
        loc.Add("UnblockedDamage", line.UnblockedDamage);
        loc.Add("BlockStatus", GetBlockStatus(line.Damage, line.UnblockedDamage));
        return loc.GetFormattedText();
    }

    private static string GetSourceName(AbstractModel? sourceModel)
    {
        return sourceModel != null
            ? PredictionHoverTips.GetModelName(sourceModel)
            : PredictionLocalization.Text("end_turn_damage_details.unknown_source").GetFormattedText();
    }

    private static string GetBlockStatus(decimal totalDamage, decimal totalUnblockedDamage)
    {
        if (totalDamage == totalUnblockedDamage)
        {
            return BlockStatusNone;
        }

        return totalUnblockedDamage == 0
            ? BlockStatusBlocked
            : BlockStatusPartial;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.HoverTips), MethodType.Getter)]
internal static class EndTurnPredictionCreatureHoverTipsPatch
{
    private static void Postfix(Creature __instance, ref IEnumerable<IHoverTip> __result)
    {
        var predictionTips = EndTurnPredictionCreatureHoverTips.GetHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
