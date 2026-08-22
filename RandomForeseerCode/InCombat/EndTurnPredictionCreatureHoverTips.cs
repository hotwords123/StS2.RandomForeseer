using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Localization;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class EndTurnPredictionCreatureHoverTips
{
    private const string BlockStatusNone = "none";
    private const string BlockStatusBlocked = "blocked";
    private const string BlockStatusPartial = "partial";

    private static readonly Dictionary<Creature, IReadOnlyList<IHoverTip>> TipsByTarget = [];

    public static void Set(DamagePrediction prediction, PredictionRisk risk)
    {
        TipsByTarget.Clear();

        IHoverTip[] warningTips = [.. risk.ToHoverTips()];

        foreach (var target in prediction.Targets)
        {
            if (target.DamageLines.Count == 0)
            {
                continue;
            }

            var detailTip = PredictionHoverTipFactory.Text("end_turn_damage_details", description =>
            {
                description.Add("Creature", target.Target.Name);
                description.Add("TotalDamage", target.TotalDamage);
                description.Add("TotalUnblockedDamage", target.TotalUnblockedDamage);
                description.Add("BlockStatus", GetBlockStatus(target.TotalDamage, target.TotalUnblockedDamage));
                description.Add("WasTargetKilled", target.WasTargetKilled);
                description.Add("Lines", [.. target.DamageLines.Select(FormatDamageLine)]);
            });

            TipsByTarget[target.Target] = [detailTip, .. warningTips];
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
        var loc = ModLocalization.Text("end_turn_damage_details.line");
        loc.Add("Source", line.Source.GetTitle());
        loc.Add("Damage", line.Damage);
        loc.Add("UnblockedDamage", line.UnblockedDamage);
        loc.Add("BlockStatus", GetBlockStatus(line.Damage, line.UnblockedDamage));
        return loc.GetFormattedText();
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
