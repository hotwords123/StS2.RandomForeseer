using System.Reflection;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib;
using STS2RitsuLib.Utils;

namespace RandomForeseer.Localization;

internal static class ModLocalization
{
    private static readonly string UiLocTableId = RitsuLibFramework.GetI18NLocTableId(Entry.ModId);

    public static I18N UiLocalization { get; } = CreateI18N("ui");

    public static I18N SettingsLocalization { get; } = CreateI18N("settings");

    public static void Register()
    {
        RitsuLibFramework.RegisterI18NLocTableBridge(Entry.ModId, UiLocalization);
    }

    public static LocString Text(string key)
    {
        return new LocString(UiLocTableId, key);
    }

    private static I18N CreateI18N(string tableId)
    {
        return RitsuLibFramework.CreateModLocalization(
            Entry.ModId,
            tableId,
            pckFolders: [$"{Entry.ResPath}/localization/{tableId}"],
            resourceAssembly: Assembly.GetExecutingAssembly());
    }
}
