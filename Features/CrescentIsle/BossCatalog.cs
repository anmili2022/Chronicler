namespace Chronicler;

internal static class BossCatalog
{
    public static readonly IReadOnlyList<BossEntry> South =
    [
        CreateCe(ExpeditionMap.South, 1, 0, "鲨鱼", "传说中的鲨鱼——尼姆瓣齿鲨", "Lv.7 新月小瓣齿鲨 x:17 y:19", "尼姆瓣齿鲨"),
        CreateCe(ExpeditionMap.South, 2, 1, "罐子", "防卫指令——指令罐", "Lv.7 新月巨像 x:10 y:8", "指令罐"),
        CreateCe(ExpeditionMap.South, 3, 2, "金钱龟", "贩卖诅咒的商贩——金钱龟", "Lv.7 新月刻托斯 x:23 y:6"),
        CreateCe(ExpeditionMap.South, 4, 3, "加鲁拉", "厌鸟巨兽——进化加鲁拉", "Lv.1 新月加鲁拉 x:30 y:8", "进化加鲁拉"),
        CreateCe(ExpeditionMap.South, 5, 4, "黑陆行鸟", "黑色连队——黑陆行鸟&黑色彗星", "Lv.14 新月猎豹 x:30 y:28", "黑色彗星"),
        CreateCe(ExpeditionMap.South, 6, 5, "新月骑士", "石质骑士团——新月骑士群", "Lv.4 新月马洛里石 x:32 y:18", "新月骑士群"),
        CreateCe(ExpeditionMap.South, 7, 6, "双足狮人", "双足狮人——跃立狮", "Lv.5 新月风扇 x:32 y:24", "跃立狮"),
        CreateCe(ExpeditionMap.South, 8, 7, "复原狮像", "城塞守卫——复原狮像", "Lv.20 新月立狮 x:36 y:22"),
        CreateCe(ExpeditionMap.South, 9, 8, "水晶龙", "拟造使魔——水晶龙", "Lv.19 新月巨钳虾 x:12 y:30"),
        CreateCe(ExpeditionMap.South, 10, 9, "土偶", "双极的造物——神秘土偶", "Lv.13 新月比布鲁斯 x:4 y:24", "神秘土偶"),
        CreateCe(ExpeditionMap.South, 11, 10, "拟鸟枝", "昏暗妖魂——鬼火苗", "Lv.19 新月哈耳庇厄鸟妖 x:12 y:15", "鬼火苗"),
        CreateCe(ExpeditionMap.South, 12, 11, "死亡爪", "潜影撕裂者——死亡爪", "Lv.16 新月黑卫 x:33 y:31"),
        CreateCe(ExpeditionMap.South, 13, 12, "狂战士", "愤怒的人造人——新月狂战士", "Lv.17 新月恶魔兵卒 x:32 y:33", "新月狂战士"),
        CreateCe(ExpeditionMap.South, 14, 13, "夺心魔", "脑髓爱好者——夺心魔", "Lv.15 新月鬼鱼 x:26 y:34"),
        CreateCe(ExpeditionMap.South, 15, 14, "岛主", "挣脱封印的大妖异——回廊恶魔", "Lv.20 新月墨渍 x:12 y:33", "回廊恶魔"),
        CreateFate(ExpeditionMap.South, 16, 15, "右上罐", "幸福的魔法罐（右上罐）", "自然刷新 x:26, y:17", "幸福的魔法罐"),
        CreateFate(ExpeditionMap.South, 17, 16, "左下罐", "瑟瑟发抖的魔法罐（左下罐）", "自然刷新 x:11.8, y:32", "瑟瑟发抖的魔法罐"),
    ];

    public static readonly IReadOnlyList<BossEntry> North =
    [
        CreateCe(ExpeditionMap.North, 101, 0, "变形法师", "拟态使魔——变形法师", "待收集 x:31.4, y:15.2"),
        CreateCe(ExpeditionMap.North, 102, 1, "亡灵法师", "天道好轮回——亡灵法师", "待收集 x:25.9, y:4.2"),
        CreateCe(ExpeditionMap.North, 103, 2, "古术魔典", "禁书化形——古术魔典", "待收集 x:34.6, y:34.6"),
        CreateCe(ExpeditionMap.North, 104, 3, "阿尔戈尔", "暴食咒鬼——阿尔戈尔", "待收集 x:36.7, y:21.4"),
        CreateCe(ExpeditionMap.North, 105, 4, "岛主", "孤岛的绑架犯——诱拐魔", "Lv.27 新月幽灵 x:18.4, y:4.2", "诱拐魔"),
        CreateCe(ExpeditionMap.North, 106, 5, "雪石膏之剑", "纯白守护者——雪石膏之剑", "待收集 x:11.1, y:8.6"),
        CreateCe(ExpeditionMap.North, 107, 6, "小小法师", "魔法军团——小小法师", "待收集 x:24.5, y:35.7"),
        CreateCe(ExpeditionMap.North, 108, 7, "神木巨人", "求道的人造人——神木巨人", "待收集 x:13.6, y:35.4"),
        CreateCe(ExpeditionMap.North, 109, 8, "魔许德拉", "苏醒的多头龙——魔许德拉", "待收集 x:19.8, y:31.1"),
        CreateCe(ExpeditionMap.North, 110, 9, "负隅宝石兽", "叛逆使魔——负隅宝石兽", "待收集 x:26.2, y:28.5"),
        CreateCe(ExpeditionMap.North, 111, 10, "惨白魔人", "诅咒的继承者——惨白魔人", "待收集 x:37.6, y:10.2"),
        CreateCe(ExpeditionMap.North, 112, 11, "二重身", "魔女复制体--卡洛菲斯提莉二重身", "新月帕尔忒诺珀(推测1)/Lv39 黑卫(推测2) x:17.1, y:20.1", "卡洛菲斯提莉二重身"),
        CreateCe(ExpeditionMap.North, 113, 12, "提蔛", "四颚斧花--提蔛", "Lv.46 新月瓦魔蛾 x:4.0, y:10.2"),
        CreateCe(ExpeditionMap.North, 114, 13, "赤龙", "暗红尸骸--赤龙", "Lv.34 大角牛 x:7.7, y:24.4"),
        CreateCe(ExpeditionMap.North, 115, 14, "阿剌克涅", "残暴的母蜘蛛--新月阿剌克涅", "待收集 x:24.8, y:18.6(推测)", "新月阿剌克涅"),
        CreateFate(ExpeditionMap.North, 116, 15, "左下罐", "被吹飞的魔法罐（左下罐）", "待收集 x:11.3, y:26.2", "被吹飞的魔法罐"),
        CreateFate(ExpeditionMap.North, 117, 16, "右上罐", "被欺负的魔法罐（右上罐）", "待收集 x:26.1, y:12.1", "被欺负的魔法罐"),
    ];

    public static IReadOnlyList<BossEntry> GetBosses(ExpeditionMap map)
        => map == ExpeditionMap.South ? South : North;

    public static IEnumerable<BossEntry> GetCriticalEncounters(ExpeditionMap map)
        => GetBosses(map).Where(boss => boss.Kind == BossEventKind.CriticalEncounter);

    public static IEnumerable<BossEntry> GetFates(ExpeditionMap map)
        => GetBosses(map).Where(boss => boss.Kind == BossEventKind.Fate);

    public static BossEntry? FindByAbbreviation(ExpeditionMap map, string abbreviation)
        => GetBosses(map).FirstOrDefault(boss => boss.Abbreviation == abbreviation);

    public static BossEntry? FindByIndex(ExpeditionMap map, int index)
        => GetBosses(map).FirstOrDefault(boss => boss.Index == index);

    public static BossEntry? MatchCriticalEncounter(ExpeditionMap map, uint dynamicEventId, string eventName)
    {
        var bosses = GetCriticalEncounters(map);

        var byId = bosses.FirstOrDefault(boss => boss.Index == dynamicEventId);
        if (byId != null)
            return byId;

        if (string.IsNullOrWhiteSpace(eventName))
            return null;

        return bosses.FirstOrDefault(boss =>
            boss.ObjectNameAliases.Any(alias => eventName.StartsWith(alias, StringComparison.Ordinal))
            || boss.Name.Equals(eventName, StringComparison.Ordinal));
    }

    public static bool MatchesCriticalEncounter(BossEntry boss, uint dynamicEventId, string eventName)
    {
        if (boss.Kind != BossEventKind.CriticalEncounter)
            return false;

        if (boss.Index == dynamicEventId)
            return true;

        if (string.IsNullOrWhiteSpace(eventName))
            return false;

        return boss.ObjectNameAliases.Any(alias => eventName.StartsWith(alias, StringComparison.Ordinal))
               || boss.Name.Equals(eventName, StringComparison.Ordinal);
    }

    private static BossEntry CreateCe(ExpeditionMap map, int id, int index, string abbreviation, string name, string trigger, params string[] aliases)
        => Create(map, id, index, BossEventKind.CriticalEncounter, abbreviation, name, trigger, null, aliases);

    private static BossEntry CreateFate(ExpeditionMap map, int id, int index, string abbreviation, string name, string trigger, params string[] aliases)
        => Create(map, id, index, BossEventKind.Fate, abbreviation, name, trigger, (ushort?)index, aliases);

    private static BossEntry Create(ExpeditionMap map, int id, int index, BossEventKind kind, string abbreviation, string name, string trigger, ushort? fateId, params string[] aliases)
    {
        var allAliases = new[] { abbreviation, GetNameTail(name) }.Concat(aliases).Distinct().ToArray();
        return new BossEntry(map, id, index, kind, abbreviation, name, trigger, fateId, allAliases);
    }

    private static string GetNameTail(string name)
    {
        var dashIndex = name.LastIndexOf('—');
        if (dashIndex >= 0 && dashIndex + 1 < name.Length)
            return name[(dashIndex + 1)..];

        var asciiDashIndex = name.LastIndexOf("--", StringComparison.Ordinal);
        return asciiDashIndex >= 0 && asciiDashIndex + 2 < name.Length ? name[(asciiDashIndex + 2)..] : name;
    }
}
