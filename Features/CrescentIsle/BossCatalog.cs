namespace Chronicler;

internal static class BossCatalog
{
    public static readonly IReadOnlyList<BossEntry> South =
    [
        CreateCe(ExpeditionMap.South, 1, 0, 41, "鲨鱼", "传说中的鲨鱼——尼姆瓣齿鲨", "Lv.7 新月小瓣齿鲨 x:17 y:19", "红", "尼姆瓣齿鲨"),
        CreateCe(ExpeditionMap.South, 2, 1, 43, "罐子", "防卫指令——指令罐", "Lv.7 新月巨像 x:10 y:8", "红"),
        CreateCe(ExpeditionMap.South, 3, 2, 45, "金钱龟", "贩卖诅咒的商贩——金钱龟", "Lv.7 新月刻托斯 x:23 y:6", "红"),
        CreateCe(ExpeditionMap.South, 4, 3, 44, "加鲁拉", "厌鸟巨兽——进化加鲁拉", "Lv.1 新月加鲁拉 x:30 y:8", "黄", "进化加鲁拉"),
        CreateCe(ExpeditionMap.South, 5, 4, 34, "黑陆行鸟", "黑色连队——黑陆行鸟&黑色彗星", "Lv.14 新月猎豹 x:30 y:28", "黄", "黑色彗星"),
        CreateCe(ExpeditionMap.South, 6, 5, 40, "新月骑士", "石质骑士团——新月骑士群", "Lv.4 新月马洛里石 x:32 y:18", "紫", "新月骑士群", "石制骑士团"),
        CreateCe(ExpeditionMap.South, 7, 6, 42, "双足狮人", "双足狮人——跃立狮", "Lv.5 新月风扇 x:32 y:24", "紫", "跃立狮"),
        CreateCe(ExpeditionMap.South, 8, 7, 46, "复原狮像", "城塞守卫——复原狮像", "Lv.20 新月立狮 x:36 y:22", "紫"),
        CreateCe(ExpeditionMap.South, 9, 8, 38, "水晶龙", "拟造使魔——水晶龙", "Lv.19 新月巨钳虾 x:12 y:30", "绿"),
        CreateCe(ExpeditionMap.South, 10, 9, 39, "土偶", "双极的造物——神秘土偶", "Lv.13 新月比布鲁斯 x:4 y:24", "绿", "神秘土偶"),
        CreateCe(ExpeditionMap.South, 11, 10, 47, "拟鸟枝", "昏暗妖魂——鬼火苗", "Lv.19 新月哈耳庇厄鸟妖 x:12 y:15", "绿", "鬼火苗"),
        CreateCe(ExpeditionMap.South, 12, 11, 36, "死亡爪", "潜影撕裂者——死亡爪", "Lv.16 新月黑卫 x:33 y:31", "蓝"),
        CreateCe(ExpeditionMap.South, 13, 12, 35, "狂战士", "愤怒的人造人——新月狂战士", "Lv.17 新月恶魔兵卒 x:32 y:33", "蓝", "新月狂战士"),
        CreateCe(ExpeditionMap.South, 14, 13, 33, "夺心魔", "脑髓爱好者——夺心魔", "Lv.15 新月鬼鱼 x:26 y:34", "蓝"),
        CreateCe(ExpeditionMap.South, 15, 14, 37, "岛主", "挣脱封印的大妖异——回廊恶魔", "Lv.20 新月墨渍 x:12 y:33", "碧", "回廊恶魔"),
        CreateFate(ExpeditionMap.South, 16, 15, 1976, "右上罐", "幸福的魔法罐（右上罐）", "自然刷新 x:26, y:17", "金", "幸福的魔法罐"),
        CreateFate(ExpeditionMap.South, 17, 16, 1977, "左下罐", "瑟瑟发抖的魔法罐（左下罐）", "自然刷新 x:11.8, y:32", "金", "瑟瑟发抖的魔法罐"),
    ];

    public static readonly IReadOnlyList<BossEntry> North =
    [
        CreateCe(ExpeditionMap.North, 101, 0, 63, "变形法师", "拟态使魔——变形法师", "自然刷新 x:31.4, y:15.2", "γ"),
        CreateCe(ExpeditionMap.North, 102, 1, 57, "亡灵法师", "天道好轮回——魔亡灵法师", "自然刷新 x:25.9, y:4.2", "β"),
        CreateCe(ExpeditionMap.North, 103, 2, 52, "古术魔典", "禁书化形——古术魔典", "自然刷新 x:34.6, y:34.6", "α"),
        CreateCe(ExpeditionMap.North, 104, 3, 54, "阿尔戈尔", "暴食咒鬼——阿尔戈尔", "自然刷新 x:36.7, y:21.4", "β"),
        CreateCe(ExpeditionMap.North, 105, 4, 61, "岛主", "孤岛的绑架犯——诱拐魔", "Lv.27 新月幽灵 x:18.4, y:4.2", "γ", "诱拐魔"),
        CreateCe(ExpeditionMap.North, 106, 5, 51, "雪石膏之剑", "纯白守护者——雪石膏之剑", "Lv.38 新月祸蛛蝎 x:11.1, y:8.6", "β"),
        CreateCe(ExpeditionMap.North, 107, 6, 60, "小小法师", "魔法军团——小小法师", "自然刷新 x:24.5, y:35.7", "β"),
        CreateCe(ExpeditionMap.North, 108, 7, 58, "神木巨人", "求道的人造人——神木巨人", "自然刷新 x:13.6, y:35.4", "γ"),
        CreateCe(ExpeditionMap.North, 109, 8, 62, "魔许德拉", "苏醒的多头龙——魔许德拉", "自然刷新 x:19.8, y:31.1", "α"),
        CreateCe(ExpeditionMap.North, 110, 9, 56, "负隅宝石兽", "叛逆使魔——负隅宝石兽", "自然刷新 x:26.2, y:28.5", "γ"),
        CreateCe(ExpeditionMap.North, 111, 10, 59, "惨白魔人", "诅咒的继承者——惨白魔人", "Lv.29 新月胡瓦西 x:37.6, y:10.2", "α"),
        CreateCe(ExpeditionMap.North, 112, 11, 50, "二重身", "魔女复制体--卡洛菲斯提莉二重身", "Lv39 新月黑卫  x:17.1, y:20.1", "γ", "魔女复制体", "卡洛菲斯提莉二重身"),
        CreateCe(ExpeditionMap.North, 113, 12, 49, "提蔛", "四颚斧花--提蔛", "Lv.46 新月瓦魔蛾 x:4.0, y:10.2", "α"),
        CreateCe(ExpeditionMap.North, 114, 13, 53, "赤龙", "暗红尸骸--赤龙", "Lv.34 新月大角牛 x:7.7, y:24.4", "β", "暗红尸骸"),
        CreateCe(ExpeditionMap.North, 115, 14, 55, "阿剌克涅", "残暴的母蜘蛛--新月阿剌克涅", "Lv.39 新月地狱犬 x:24.8, y:18.6", "α", "新月阿剌克涅"),
        CreateFate(ExpeditionMap.North, 116, 15, 2073, "左下罐", "被吹飞的魔法罐（左下罐）", "自然刷新 x:11.3, y:26.2", "β", "被吹飞的魔法罐"),
        CreateFate(ExpeditionMap.North, 117, 16, 2072, "右上罐", "被欺负的魔法罐（右上罐）", "自然刷新 x:26.1, y:12.1", "γ", "被欺负的魔法罐"),
    ];

    private static readonly IReadOnlyList<BossEntry> ExtraSouthFates =
    [
        CreateFate(ExpeditionMap.South, 1962, 1962, 1962, "纳木", "涌潮海魔——纳木", "FATE x:24.5, y:34.7", "青", "涌潮海魔"),
        CreateFate(ExpeditionMap.South, 1963, 1963, 1963, "金色石面", "古代怪石——金色石面", "FATE x:29.2, y:31.4", "青"),
        CreateFate(ExpeditionMap.South, 1964, 1964, 1964, "罗普罗斯", "悲鸣收集者——罗普罗斯", "FATE", "黄"),
        CreateFate(ExpeditionMap.South, 1965, 1965, 1965, "巨大鸟", "甲板清扫者——巨大鸟", "FATE", "橙"),
        CreateFate(ExpeditionMap.South, 1966, 1966, 1966, "西西弗斯", "神罚石兽——西西弗斯", "FATE", "碧"),
        CreateFate(ExpeditionMap.South, 1967, 1967, 1967, "高等魔鸟", "进化的毒鸟——高等魔鸟", "FATE", "橙"),
        CreateFate(ExpeditionMap.South, 1968, 1968, 1968, "除湿之火", "湿度猎手——除湿之火", "FATE", "绿"),
        CreateFate(ExpeditionMap.South, 1969, 1969, 1969, "癫泥怪", "土壤守护者——癫泥怪", "FATE", "绿"),
        CreateFate(ExpeditionMap.South, 1970, 1970, 1970, "岛屿监视者", "监视之瞳——岛屿监视者", "FATE", "青"),
        CreateFate(ExpeditionMap.South, 1971, 1971, 1971, "执行者", "美丽的咒杀者——执行者", "FATE", "黄"),
        CreateFate(ExpeditionMap.South, 1972, 1972, 1972, "生命收割者", "凶恶使魔——生命收割者", "FATE", "紫"),
    ];

    private static readonly IReadOnlyList<BossEntry> ExtraNorthFates =
    [
        CreateFate(ExpeditionMap.North, 2074, 2074, 2074, "牛魔", "暴力牛魔——好战弥诺陶洛斯", "FATE x:36.2, y:11.0", "消幻晶α ×3", "好战弥诺陶洛斯"),
        CreateFate(ExpeditionMap.North, 2083, 2083, 2083, "美杜莎", "仿制的蛇人偶——半灵美杜莎", "FATE x:6.9, y:34.2", "消幻晶γ ×3", "半灵美杜莎"),
        CreateFate(ExpeditionMap.North, 2076, 2076, 2076, "奇美拉", "水边暴君——统领奇美拉", "FATE x:23.8, y:10.7", "消幻晶γ ×3", "统领奇美拉"),
        CreateFate(ExpeditionMap.North, 2082, 2082, 2082, "狮鹫", "驾驭自然的巨兽——呼风狮鹫", "FATE x:7.2, y:24.1", "消幻晶α ×3", "呼风狮鹫"),
        CreateFate(ExpeditionMap.North, 2080, 2080, 2080, "冰狼", "狼占狗窝——遗迹冰狼", "FATE x:19.5, y:38.3", "消幻晶β ×3", "遗迹冰狼"),
        CreateFate(ExpeditionMap.North, 2075, 2075, 2075, "邪瞳", "诅咒宝珠——邪瞳", "FATE x:31.5, y:20.4", "消幻晶β ×3", "邪瞳"),
        CreateFate(ExpeditionMap.North, 2081, 2081, 2081, "忍耐基", "腐坏街道的守护者——忍耐基路伯", "FATE x:10.0, y:32.9", "消幻晶α ×3", "忍耐基路伯", "恶耐基"),
        CreateFate(ExpeditionMap.North, 2084, 2084, 2084, "雷兽", "高傲的雷兽——新月女王", "FATE x:27.0, y:2.9", "消幻晶β ×3", "新月女王"),
        CreateFate(ExpeditionMap.North, 2077, 2077, 2077, "水马", "历战水马——凯尔派总领", "FATE x:26.5, y:20.9", "消幻晶α ×3", "凯尔派总领"),
        CreateFate(ExpeditionMap.North, 2079, 2079, 2079, "伊阿姆柏", "自怨自艾的歌手——伊阿姆柏", "FATE x:16.5, y:24.0", "消幻晶γ ×3", "伊阿姆柏"),
        CreateFate(ExpeditionMap.North, 2078, 2078, 2078, "珊迪", "魔界的叹息——妖艳魔花珊迪", "FATE x:9.9, y:27.6", "消幻晶β ×3", "妖艳魔花珊迪"),
    ];

    public static IReadOnlyList<BossEntry> GetBosses(ExpeditionMap map)
        => map == ExpeditionMap.South ? South : North;

    public static IEnumerable<BossEntry> GetCriticalEncounters(ExpeditionMap map)
        => GetBosses(map).Where(boss => boss.Kind == BossEventKind.CriticalEncounter);

    public static IEnumerable<BossEntry> GetFates(ExpeditionMap map)
        => map switch
        {
            ExpeditionMap.South => GetBosses(map).Where(boss => boss.Kind == BossEventKind.Fate).Concat(ExtraSouthFates),
            ExpeditionMap.North => GetBosses(map).Where(boss => boss.Kind == BossEventKind.Fate).Concat(ExtraNorthFates),
            _ => GetBosses(map).Where(boss => boss.Kind == BossEventKind.Fate),
        };

    public static BossEntry? FindByAbbreviation(ExpeditionMap map, string abbreviation)
        => GetBosses(map).FirstOrDefault(boss => boss.Abbreviation == abbreviation);

    public static BossEntry? FindByIndex(ExpeditionMap map, int index)
        => GetBosses(map).FirstOrDefault(boss => boss.Index == index);

    public static BossEntry? MatchCriticalEncounter(ExpeditionMap map, uint dynamicEventId, string eventName)
    {
        var bosses = GetCriticalEncounters(map);

        var byId = bosses.FirstOrDefault(boss => boss.DynamicEventId == dynamicEventId);
        if (byId != null)
            return byId;

        if (string.IsNullOrWhiteSpace(eventName))
            return null;

        return bosses.FirstOrDefault(boss =>
            boss.ObjectNameAliases.Any(alias => eventName.StartsWith(alias, StringComparison.Ordinal))
            || NormalizeEventName(boss.Name).Equals(NormalizeEventName(eventName), StringComparison.Ordinal));
    }

    public static bool MatchesCriticalEncounter(BossEntry boss, uint dynamicEventId, string eventName)
    {
        if (boss.Kind != BossEventKind.CriticalEncounter)
            return false;

        if (boss.DynamicEventId == dynamicEventId)
            return true;

        if (string.IsNullOrWhiteSpace(eventName))
            return false;

        return boss.ObjectNameAliases.Any(alias => eventName.StartsWith(alias, StringComparison.Ordinal))
               || NormalizeEventName(boss.Name).Equals(NormalizeEventName(eventName), StringComparison.Ordinal);
    }

    public static bool IsMagicPot(BossEntry boss)
        => boss.Kind == BossEventKind.Fate && boss.ObjectNameAliases.Any(alias => alias.Contains("魔法罐", StringComparison.Ordinal));

    public static bool IsMagicPotFateId(ushort fateId)
        => fateId is 1976 or 1977 or 2072 or 2073;

    private static BossEntry CreateCe(ExpeditionMap map, int id, int index, uint dynamicEventId, string abbreviation, string name, string trigger, string drop, params string[] aliases)
        => Create(map, id, index, BossEventKind.CriticalEncounter, abbreviation, name, trigger, null, dynamicEventId, drop, aliases);

    private static BossEntry CreateFate(ExpeditionMap map, int id, int index, string abbreviation, string name, string trigger, string drop, params string[] aliases)
        => Create(map, id, index, BossEventKind.Fate, abbreviation, name, trigger, (ushort?)index, null, drop, aliases);

    private static BossEntry CreateFate(ExpeditionMap map, int id, int index, ushort fateId, string abbreviation, string name, string trigger, string drop, params string[] aliases)
        => Create(map, id, index, BossEventKind.Fate, abbreviation, name, trigger, fateId, null, drop, aliases);

    private static BossEntry Create(ExpeditionMap map, int id, int index, BossEventKind kind, string abbreviation, string name, string trigger, ushort? fateId, uint? dynamicEventId, string drop, params string[] aliases)
    {
        var allAliases = new[] { abbreviation, GetNameTail(name) }.Concat(aliases).Distinct().ToArray();
        return new BossEntry(map, id, index, kind, abbreviation, name, trigger, fateId, dynamicEventId, drop, allAliases);
    }

    private static string GetNameTail(string name)
    {
        var dashIndex = name.LastIndexOf('—');
        if (dashIndex >= 0 && dashIndex + 1 < name.Length)
            return name[(dashIndex + 1)..];

        var asciiDashIndex = name.LastIndexOf("--", StringComparison.Ordinal);
        return asciiDashIndex >= 0 && asciiDashIndex + 2 < name.Length ? name[(asciiDashIndex + 2)..] : name;
    }

    private static string NormalizeEventName(string name)
        => name.Replace("—", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim();
}
