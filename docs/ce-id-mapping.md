# CE DynamicEventId Mapping

`BossEntry.Index` is the fixed xyd 17-slot share-code index. It is not the game
`DynamicEventId`. CE detection, live status, and lifecycle tracking must use
`BossEntry.DynamicEventId`.

Sources used for every mapped row:

- `E:\git\BOCCHI-Kano\BOCCHI\Data\EventData.cs`: authoritative ID and BOCCHI internal name.
- `E:\git\BOCCHI-Kano\Translations\zh\modules.automator.json`: Chinese CE name paired with the BOCCHI internal-name configuration key.

## South (TerritoryType 1252)

| DynamicEventId | BOCCHI internal name | 新月岛史官简称 | xyd display name |
| ---: | --- | --- | --- |
| 33 | Scourge of the Mind | 夺心魔 | 脑髓爱好者——夺心魔 |
| 34 | The Black Regiment | 黑陆行鸟 | 黑色连队——黑陆行鸟&黑色彗星 |
| 35 | The Unbridled | 狂战士 | 愤怒的人造人——新月狂战士 |
| 36 | Crawling Death | 死亡爪 | 潜影撕裂者——死亡爪 |
| 37 | Calamity Bound | 岛主 | 挣脱封印的大妖异——回廊恶魔 |
| 38 | Trial by Claw | 水晶龙 | 拟造使魔——水晶龙 |
| 39 | From Times Bygone | 土偶 | 双极的造物——神秘土偶 |
| 40 | Company of Stone | 新月骑士 | 石质骑士团——新月骑士群 |
| 41 | Shark Attack | 鲨鱼 | 传说中的鲨鱼——尼姆瓣齿鲨 |
| 42 | On the Hunt | 双足狮人 | 双足狮人——跃立狮 |
| 43 | With Extreme Prejudice | 罐子 | 防卫指令——指令罐 |
| 44 | Noise Complaint | 加鲁拉 | 厌鸟巨兽——进化加鲁拉 |
| 45 | Cursed Concern | 金钱龟 | 贩卖诅咒的商贩——金钱龟 |
| 46 | Eternal Watch | 复原狮像 | 城塞守卫——复原狮像 |
| 47 | Flame of Dusk | 拟鸟枝 | 昏暗妖魂——鬼火苗 |

`48` is `The Forked Tower: Blood`; it has no matching xyd South 17-slot CE and is intentionally not added to `BossCatalog`.

## North (TerritoryType 1346)

| DynamicEventId | 新月岛史官简称 | xyd display name |
| ---: | --- | --- |
| 49 | 提蔛 | 四颚斧花——提蔛 |
| 50 | 二重身 | 魔女复制体——卡洛菲斯提莉二重身 |
| 51 | 雪石膏之剑 | 纯白守护者——雪石膏之剑 |
| 52 | 古术魔典 | 禁书化形——古术魔典 |
| 53 | 赤龙 | 暗红尸骸——赤龙 |
| 54 | 阿尔戈尔 | 暴食咒鬼——阿尔戈尔 |
| 55 | 阿剌克涅 | 残暴的母蜘蛛——新月阿剌克涅 |
| 56 | 负隅宝石兽 | 叛逆使魔——负隅宝石兽 |
| 57 | 亡灵法师 | 天道好轮回——魔亡灵法师 |
| 58 | 神木巨人 | 求道的人造人——神木巨人 |
| 59 | 惨白魔人 | 诅咒的继承者——惨白魔人 |
| 60 | 小小法师 | 魔法军团——小小法师 |
| 61 | 岛主 | 孤岛的绑架犯——诱拐魔 |
| 62 | 魔许德拉 | 苏醒的多头龙——魔许德拉 |
| 63 | 变形法师 | 拟态使魔——变形法师 |

`64` and `65` are the two Forked Tower events. They have no matching xyd North 17-slot CE and are intentionally not added to `BossCatalog`.

## Coordinate And Route Coverage

- All catalogued South FATEs and all North FATEs except 2084 have BOCCHI `StartPosition` coverage in `BossPositionCatalog`.
- North FATE 2084 deliberately uses the official `planmap.lgb` position `(140, 37, -708)` because BOCCHI leaves its `StartPosition` unset and falls back to the live `IFate.Position`.
- All 15 North xyd CE entries now use BOCCHI `StartPosition` values in `BossPositionCatalog`.
- BOCCHI defines no `StartPosition` for South CE IDs 33-47. South CE coordinates remain an explicit in-game observation task; no guessed coordinate is used for map flags or fallback navigation.
- Built-in routes currently cover North FATE 2073 / xyd BossId 116 (three routes) and North FATE 2083 (one route). All other targets intentionally fall back to direct navigation until an in-game recorded route is verified.
- South FATE 1967 / 进化的毒鸟——高等魔鸟 explicitly prefers the Crystallized Caverns aethernet (PlaceNameId 4929).
