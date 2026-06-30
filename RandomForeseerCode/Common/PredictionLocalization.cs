using System.Reflection;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib;
using STS2RitsuLib.Utils;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PredictionLocalization
{
    private static readonly string UiLocTableId = RitsuLibFramework.GetI18NLocTableId(Entry.ModId);

    private static readonly I18N UiLocalization =
        RitsuLibFramework.CreateModLocalization(
            Entry.ModId,
            "ui",
            pckFolders: [$"{Entry.ResPath}/localization/ui"],
            resourceAssembly: Assembly.GetExecutingAssembly());

    public static void Register()
    {
        RitsuLibFramework.RegisterI18NLocTableBridge(Entry.ModId, UiLocalization);
    }

    public static LocString Text(string key)
    {
        return new LocString(UiLocTableId, key);
    }
}
