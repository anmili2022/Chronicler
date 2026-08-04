namespace Chronicler;

internal sealed record InvestigationNoteEntry(int Number, string Source, SurveyPoint? Point = null);

internal static class InvestigationNoteCatalog
{
    public const string WikiUrl = "https://ff14.huijiwiki.com/wiki/%E6%96%B0%E6%9C%88%E5%B2%9B%E8%B0%83%E6%9F%A5%E7%AC%94%E8%AE%B0";

    public static readonly IReadOnlyList<InvestigationNoteEntry> South =
    [
        Note(1, "任务：柯坦拉姆最后的冒险"), Note(2, "幻境村 (X: 6.1, Y: 4.8)"),
        Note(3, "任务：无人的村落"), Note(4, "任务：不中用的使魔"),
        NoteAt(5, "新月岛南部 (X: 38.6, Y: 7.6)", ExpeditionMap.South, 0), Note(6, "任务：古代的战斗技术"),
        NoteAt(7, "新月岛南部 (X: 31.3, Y: 17.0)", ExpeditionMap.South, 1), Note(8, "CE：贩卖诅咒的商贩——金钱龟"),
        NoteAt(9, "新月岛南部 (X: 20.2, Y: 12.3)", ExpeditionMap.South, 2), Note(10, "CE：传说中的鲨鱼——尼姆瓣齿鲨"),
        Note(11, "魔法罐 FATE"), NoteAt(12, "新月岛南部 (X: 23.2, Y: 21.5)", ExpeditionMap.South, 3),
        NoteAt(13, "新月岛南部 (X: 10.2, Y: 22.5)", ExpeditionMap.South, 4), Note(14, "CE：双极的造物——神秘土偶"),
        NoteAt(15, "新月岛南部 (X: 24.1, Y: 33.0)", ExpeditionMap.South, 5), Note(16, "CE：黑色连队"),
        Note(17, "CE：愤怒的人造人——新月狂战士"), NoteAt(18, "新月岛南部 (X: 18.6, Y: 33.8)", ExpeditionMap.South, 6),
        NoteAt(19, "新月岛南部 (X: 15.6, Y: 29.4)", ExpeditionMap.South, 7), Note(20, "CE：挣脱封印的大妖异——回廊恶魔"),
        NoteAt(21, "新月岛南部 (X: 36.5, Y: 33.7)", ExpeditionMap.South, 8), NoteAt(22, "新月岛南部 (X: 36.0, Y: 22.6)", ExpeditionMap.South, 9),
        NoteAt(23, "新月岛南部 (X: 3.7, Y: 5.8)", ExpeditionMap.South, 10), NoteAt(24, "新月岛南部 (X: 8.7, Y: 35.8)", ExpeditionMap.South, 11),
        Note(25, "任务：毁灭的文明"), Note(26, "两歧塔 力之塔 Boss1 / 魔之护符 x3"),
        Note(27, "两歧塔 力之塔 Boss2 / 魔之护符 x3"), Note(28, "两歧塔 力之塔 Boss3 / 魔之护符 x3"),
        Note(29, "两歧塔 力之塔 Boss4 / 魔之护符 x3"), Note(30, "通关两歧塔 力之塔后的避世书库"),
    ];

    public static readonly IReadOnlyList<InvestigationNoteEntry> North =
    [
        NoteAt(31, "新月岛北部 (X: 39.1, Y: 38.2)", ExpeditionMap.North, 0), NoteAt(32, "新月岛北部 (X: 36.6, Y: 31.6)", ExpeditionMap.North, 1),
        Note(33, "CE：禁书化形——古术魔典"), Note(34, "CE：魔法军团——小小法师"),
        Note(35, "CE：暴食咒鬼——阿尔戈尔"), NoteAt(36, "新月岛北部 (X: 27.6, Y: 26.3)", ExpeditionMap.North, 2),
        NoteAt(37, "新月岛北部 (X: 39.7, Y: 22.6)", ExpeditionMap.North, 3), Note(38, "CE：拟态使魔——变形法师"),
        NoteAt(39, "新月岛北部 (X: 27.0, Y: 14.3)", ExpeditionMap.North, 4), NoteAt(40, "新月岛北部 (X: 40.3, Y: 3.4)", ExpeditionMap.North, 5),
        Note(41, "CE：诅咒的继承者——惨白魔人"), Note(42, "CE：天道好轮回——魔亡灵法师"),
        NoteAt(43, "新月岛北部 (X: 17.1, Y: 5.0)", ExpeditionMap.North, 6), Note(44, "CE：孤岛的绑架犯——诱拐魔"),
        NoteAt(45, "新月岛北部 (X: 11.2, Y: 38.9)", ExpeditionMap.North, 7), NoteAt(46, "新月岛北部 (X: 4.5, Y: 36.0)", ExpeditionMap.North, 8),
        NoteAt(47, "新月岛北部 (X: 3.2, Y: 24.4)", ExpeditionMap.North, 9), Note(48, "CE：暗红尸骸——赤龙"),
        NoteAt(49, "新月岛北部 (X: 7.4, Y: 14.0)", ExpeditionMap.North, 10), Note(50, "CE：纯白守护者——雪石膏之剑"),
        NoteAt(51, "新月岛北部 (X: 3.8, Y: 3.4)", ExpeditionMap.North, 11), NoteAt(52, "新月岛北部 (X: 21.1, Y: 19.7)", ExpeditionMap.North, 12),
        Note(53, "CE：魔女复制体——卡洛菲斯提莉二重身"), NoteAt(54, "新月岛北部 (X: 22.7, Y: 24.1)", ExpeditionMap.North, 13),
        Note(55, "两歧塔 魔之塔 Boss1"), Note(56, "两歧塔 魔之塔 Boss2"),
        Note(57, "两歧塔 魔之塔 Boss3"), Note(58, "两歧塔 魔之塔 Boss4"),
        Note(59, "任务：最后的知识"), Note(60, "通关两歧塔 魔之塔后的避世收藏库"),
    ];

    private static readonly HashSet<(ExpeditionMap Map, int BossId)> CeNotes =
    [
        (ExpeditionMap.South, 1), (ExpeditionMap.South, 3), (ExpeditionMap.South, 5),
        (ExpeditionMap.South, 10), (ExpeditionMap.South, 13), (ExpeditionMap.South, 15),
        (ExpeditionMap.North, 101), (ExpeditionMap.North, 102), (ExpeditionMap.North, 103),
        (ExpeditionMap.North, 104), (ExpeditionMap.North, 105), (ExpeditionMap.North, 106),
        (ExpeditionMap.North, 107), (ExpeditionMap.North, 111), (ExpeditionMap.North, 112),
        (ExpeditionMap.North, 114),
    ];

    public static bool HasNote(BossEntry boss)
        => boss.Kind == BossEventKind.CriticalEncounter && CeNotes.Contains((boss.Map, boss.Id));

    public static int? GetNoteNumber(BossEntry boss)
        => boss.Map switch
        {
            ExpeditionMap.South => boss.Id switch
            {
                3 => 8,
                1 => 10,
                10 => 14,
                5 => 16,
                13 => 17,
                15 => 20,
                _ => null,
            },
            ExpeditionMap.North => boss.Id switch
            {
                103 => 33,
                107 => 34,
                104 => 35,
                101 => 38,
                111 => 41,
                102 => 42,
                105 => 44,
                114 => 48,
                106 => 50,
                112 => 53,
                _ => null,
            },
            _ => null,
        };

    private static InvestigationNoteEntry Note(int number, string source) => new(number, source);
    private static InvestigationNoteEntry NoteAt(int number, string source, ExpeditionMap map, int pointIndex)
        => new(number, source, CrescentMapPointCatalog.GetSurveyPoints(map)[pointIndex]);
}
