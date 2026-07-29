namespace Chronicler;

internal static class XydShoutGenerator
{
    public static string GenerateNormal(ExpeditionMap map, CrescentStateService state)
        => "/sh " + string.Join(" ", BossCatalog.GetBosses(map).Select(boss => $"{boss.Abbreviation} [{FormatTime(state.GetAppearedAt(boss))}]"));

    public static string GenerateOutIsland(ExpeditionMap map, CrescentStateService state)
        => "/sh 当前史官准备离岛，复制本信息到xyd.zzmelon.com继承我的记录吧->"
           + string.Join(" ", BossCatalog.GetBosses(map).Select(boss => $"{boss.Abbreviation} [{FormatTime(state.GetAppearedAt(boss))}]"));

    public static string GenerateShareCodeOutIsland(ExpeditionMap map, CrescentStateService state)
        => $"/sh 当前史官准备离岛，复制以下分享码到xyd.zzmelon.com继承我的{GetMapName(map)}记录吧->"
           + XydShareCodeCodec.Encode(map, state.Snapshot(map));

    private static string FormatTime(DateTime? time)
        => time.HasValue ? time.Value.ToString("HH:mm") : "--:--";

    private static string GetMapName(ExpeditionMap map)
        => map == ExpeditionMap.South ? "南征" : "北征";
}
