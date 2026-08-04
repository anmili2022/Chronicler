# 新月岛史官设计记录

本文档记录当前实现的关键设计和仍需验证的项目。

## 目标

新月岛史官用于记录新月岛南征/北征 CE 与 FATE 出现时间，兼容 xyd 分享码和喊话同步，并提供手动导航与全自动导航循环。

## 核心模块

- `BossCatalog`：维护南征/北征 Boss、新月岛史官目标分类、FateId、掉落标记、别名和特殊传送点偏好。
- `BossEntry`：单个 Boss 记录，含掉落标记 `Drop`（南征魂晶颜色 / 北征 αβγ）。
- `FateAppearanceDetector`：从 `FateTable` 检测 FATE 出现并记录时间。
- `CriticalEncounterDetector`：从 `DynamicEventContainer` 检测 CE 出现并记录时间。
- `CurrencyGainTracker`：监听系统消息中的十二城邦白银币/白金币获得文本，统计会话内收益和效率。
- `VnavService`：封装 vnavmesh、Lifestream、亚返回确认、传送点选择、导航目标、下坐骑处理和上岛路线。
- `ChroniclerPlugin.Framework`：每帧驱动自动记录、全自动扫描、目标生命周期、回营地门控、待命点导航和死亡处理。
- `MainWindow`：完整配置、记录、导入导出、调试区、自动导航目标列表和 DEBUG 页签。
- `FloatingStatusWindow`：轻量显示当前新月岛史官目标（含掉落标记），并提供导航、清除导航、回营地、全自动开关、待命点和效率按钮。

触发条件以 `https://xyd.zzmelon.com/js/app.js` 的当前 CE 列表为准；更新时需同步 `BossCatalog` 与 README 的南北征表格。

## BOCCHI 参考

仓库：`https://github.com/OhKannaDuh/BOCCHI`

BOCCHI 的新月岛宝箱和胡萝卜检测实现可作为本插件资源计数的参考，关键源码如下：

- `BOCCHI.Treasure/Services/TreasureTracker.cs`：扫描 `IObjectTable` 中 `ObjectKind.Treasure` 对象，按 `BaseId` 跟踪当前可见宝箱；对象失效、消失或被打开后从列表和计数中移除。
- `BOCCHI.Treasure/Services/TreasureCoffer.cs`：通过 Lumina `Treasure` 表读取对象的 `SGB.RowId`，`1596` 判定为铜宝箱，`1597` 判定为银宝箱；不使用对象名称匹配。
- `BOCCHI.Treasure/Services/CarrotTracker.cs`：扫描 `ObjectKind.EventObj`，使用 `BaseId == 2010139` 判定胡萝卜，并检查对象有效性。
- `BOCCHI.Common/Data/OccultObjectType.cs`：定义胡萝卜 `BaseId` 常量 `2010139`。
- `BOCCHI.Common/Data/Zones/TerritoryExtras.cs`：记录新月岛宝箱相关 BaseId `2014741`、`2014742`、`2014743`，用于宝箱路线安全限制，不是铜/银分类的最终依据。

BOCCHI 还监听 `_WideText` 插件的消息 `10965`，从游戏文本初始化当前地图的银/铜宝箱总数；运行中再结合 `ObjectTable` 的可见宝箱对象和开箱状态维护当前剩余计数。若本插件只需要悬浮窗显示当前已加载对象数量，应采用对象扫描方案；若需要显示地图总剩余数量，则还需要复用该消息解析和宝箱开箱状态检测。

资源检测应先确认当前区域是新月岛，再按以下条件统计：

```text
铜宝箱：ObjectKind.Treasure + Treasure.SGB.RowId == 1596
银宝箱：ObjectKind.Treasure + Treasure.SGB.RowId == 1597
胡萝卜：ObjectKind.EventObj + BaseId == 2010139
```

地图标记坐标还参考 `EurekaTrackerAutoPopper/OccultChests.cs`（MIT License），并保留源项目归属说明。

### 地图标记

- `CrescentMapMarkerController` 管理新月岛标记，类别包括铜宝箱、银宝箱、魔法罐、第二次机会宝箱、胡萝卜和调查点。铜/银宝箱必须保持独立开关；魔法罐的南征/北征搜索点由同一个开关控制。
- `KamiToolKit.MapOverlay` 用于北征浮空岛、地下区域等跨子地图覆盖标记；插件加载时必须先调用 `KamiToolKitLibrary.Initialize(pluginInterface, pluginName)`，卸载时在控制器释放后调用 `KamiToolKitLibrary.Dispose()`。
- `KamiToolKit.dll` 是地图覆盖层的运行时依赖。GitHub Release 的 `Chronicler.zip` 必须同时包含 `Chronicler.dll`、`KamiToolKit.dll`、`Chronicler.json` 和 `Chronicler.deps.json`；发布后应下载并展开 ZIP 核对这四个文件。
- 若覆盖层不可用，当前已打开的子地图仍通过 `AgentMap.AddMapMarker` 和 `AddMiniMapMarker` 显示标记。覆盖层或单个节点异常不得中断 `IFramework.Update`。
- 每次刷新需清除本控制器放置的原生地图/小地图标记后再重绘，以便关闭类别立即生效。
- `MapMarkerSwitcherWindow` 仅在新月岛且 `AreaMap` 可见时显示在地图上方。按钮顺序为铜、银、魔法罐、第二次机会宝箱、胡萝卜、调查点；绿色表示启用，灰色表示禁用。
- 设置页的“地图”页签提供类别、快速切换条、接近提示和自动 Flag 开关。

### 地图提示与 Flag

- 接近实际加载的铜宝箱、银宝箱或胡萝卜（80 距离内）时，可显示 `[地图提示] 附近发现...`；该消息不属于导航通知。
- 自动 Flag 启用时，先清除当前 Flag，再调用 `AgentMap.SetFlagMapMarker(ClientState.TerritoryType, ClientState.MapId, position)`。必须使用运行时 `ClientState.MapId`，不能用固定南/北主地图 ID，否则北征地下等子地图会失败。
- 修改接近提示或自动 Flag 开关后应清除资源提示缓存，允许当前附近对象立即重新触发。

## 掉落标记

- 北征 CE 使用 `α β γ`（对应 xyd 网页 `typeLabel`）。
- 南征 CE 使用魂晶颜色 `红 黄 紫 绿 蓝 碧 金`（对应 xyd 网页 `crystal`）；魔法罐标记为 `金`。
- 北征普通 FATE 的消幻晶掉落由外部危命任务资料维护，BOCCHIOK 与 xyd `app.js` 均不包含该映射；每个 FATE 固定掉落对应消幻晶 `×3`。
  - `α`：2074 牛魔、2077 水马、2081 恶耐基、2082 狮鹫。
  - `β`：2073 左下罐、2075 邪瞳、2078 珊迪、2080 冰狼、2084 雷兽。
  - `γ`：2072 右上罐、2076 奇美拉、2079 伊阿姆柏、2083 美杜莎。
- 两个北征魔法罐除消幻晶外还会掉落 `调查记录：撒娇罐`。
- 掉落标记在 Boss 表格按颜色着色显示；悬浮窗 CE 与 FATE 名称后都会显示独立的彩色标签，如 `[α]`、`[β]`、`[γ]`、`[红]`。
- `MainWindow.DrawDropMark` 与 `FloatingStatusWindow.DrawDropMark` 各自维护同一套颜色映射，修改时需同步两处。

## 调查笔记与悬浮窗

- `调查笔记` 页签提供“与悬浮窗联动”开关，配置会持久化保存。
- 开启后，悬浮窗 CE 条目会查询游戏内已解锁的调查笔记；对应调查笔记已解锁时，不显示该 CE 名称后的 `[笔]` 标签。未解锁、无法匹配笔记或关闭开关时，仍显示 `[笔]`。

## 宏命令

- `/shiguan record <简称>` / `/史官 记录 <简称>`：把 Boss 出现时间记录为当前时间。
- `/shiguan set <简称> <HH:mm>` / `/史官 设置 <简称> <HH:mm>`：把 Boss 出现时间修改为指定时间，格式校验 `HH:mm`。
- `/shiguan clear <简称>` / `/史官 清除 <简称>`：清除 Boss 出现时间记录。
- 以上命令作用于当前列表对应的地图（`Configuration.LastSelectedMap`）。
- `/shiguan import <文本>`：从分享码或喊话文本导入记录。
- `/shiguan shout`、`/shiguan code`：生成喊话 / 分享码。
- `/shiguan enter` / `/史官 上岛`：一键上岛（见上岛路线）。

## 界面页签

- `新月岛史官`：南征/北征切换、清空所有、Boss 表格、导入导出共享文本框。
- `自动寻路`：全自动导航设置。
- `设置`：紧凑的通用选项与地图 ID 设置。首行提供插件、悬浮窗和锁定开关；第二行是聊天同步与自动记录；第三行是自动记录、导航通知和全自动提示。各行间用分割线隔开，地图 ID 设置默认折叠。
- `DEBUG`：显示调试区、导航调试、路线调试开关、显示当前位置/新月岛史官目标距离、复制全部调试信息，以及调试区内容。
- `设置` 中每个开关提供悬停说明。聊天同步只读取和解析聊天中的 xyd 分享码或 `简称 [HH:mm]` 记录，绝不发送聊天消息。
- 地图 ID 设置在 `设置` 页签的“地图识别设置”折叠区；调试区在 `DEBUG` 页签。
- 列表不受所在地图限制：进入南岛/北岛不再强制切换 `LastSelectedMap`，由顶部 `南征`/`北征` 按钮控制。

## 消息通知

- 自动记录、导航、全自动与调试消息使用统一的方括号前缀：`[自动记录]`、`[导航通知]`、`[全自动]`、`[导航调试]`、`[路线调试]`。
- `自动记录提示`只控制 `[自动记录]` 消息；关闭后仍会自动记录 FATE/CE 出现时间。
- `导航通知`控制下坐骑目标设定和到达目标附近等常规导航状态消息。
- `全自动提示`控制全自动扫描、导航、回营地和自动进出岛的状态消息。
- `导航调试`和`路线调试`仅控制诊断输出，位于 `DEBUG` 页签。
- 导航失败、超时、依赖缺失等异常消息不受上述通知开关影响。

## 导入导出

- 导入与导出共用同一个文本框：`生成喊话` / `生成出岛喊话` / `生成分享码` 写入该框并复制到剪贴板，`应用导入` 从同一框读取解析。
- 解析支持 xyd 分享码（`N0...`/`B0...`）和 `简称 [HH:mm]` 喊话格式。

## 全自动导航流程

- `全自动模式` 是运行期状态，不写入配置；插件重载或重新登录后默认关闭。
1. 开启全自动后，如果角色不在营地或待命点，且不在战斗中，先使用亚返回回营地。
2. 如果设置了待命点，初始回营地或目标结束回营地后，会等待读图和营地稳定，再前往待命点。
3. 只有角色位于营地或待命点附近，且没有当前目标时，才扫描新月岛史官目标。
4. 扫描优先级由 `AutoPrioritizeCe` 控制，默认 CE 优先。
5. 新目标出现后默认等待 5 秒；到期时目标仍有效才导航。
6. 普通 FATE 处于 `Running` 且进度达到 100 时视为结束；魔法罐 FATE 的 `Ending` 状态仍视为有效。
7. 目标结束后立即停止当前 vnav 导航，提示结束，按配置延迟后回营地。
8. 回营地后必须观察读图、确认地图正确、角色在营地附近稳定 2 秒，再清除回营地门控。
9. 回营地门控清除后默认等待 10 秒再恢复扫描。
10. 如果需要岛内传送，优先使用玩家水平距离 60 码内最近的水晶；附近没有水晶时才使用亚返回，并在回营地后前往营地大水晶传送。
11. FATE 扫描直接遍历当前 `FateTable` 的可见条目，再按 FateId、名称或别名匹配 `BossCatalog`；不要反向遍历目录后查找单个 FATE，避免新出现的北征普通 FATE 漏掉导航候选。

## 死亡处理

- 新月岛史官目标期间死亡时，只停止当前 vnav 导航并清除待导航延迟。
- 不清除当前 `activeAutoNavigationKey`。
- 复活后不主动回营地，不提示“已复活，返回营地重新扫描”。
- 当前目标正常结束后，才按目标结束流程回营地和恢复扫描。

## 待命点设计

- 待命点记录角色当前位置、地图和坐标。
- 南征和北征待命点不会混用。
- 主窗口提供记录/更新/清除待命点。
- 悬浮窗提供单个 `待命点` 按钮，点击即记录或更新当前位置，tooltip 为“记录、更新待命点”。
- 手动点击悬浮窗 `回营地` 时，如果已设置待命点，会在回营地完成后前往待命点；未设置待命点时只执行回营地，不会自动选择其他位置。
- 手动回营地的“回营地后前往待命点”条件由 `HasAutoReturnStandbyPoint` 决定，南征和北征待命点不会混用。
- 开启“导航调试”后，点击回营地会记录当前地图和待命点状态，随后记录清理导航状态、`UseAction` 结果和回营地确认结果；日志中的 `待命点=False` 表示本次只回营地。
- 待命点回转不会抢占正在进行的自动新月岛史官目标导航；新的自动目标开始时会清理待命点延迟任务。
- 自动目标结束时，会先按“结束后回营地延迟”等待，再执行亚返回；回营地后需确认读图完成、营地附近稳定 2 秒，并至少等待 8 秒才前往待命点。任意阶段出现新的可导航 CE/FATE 时，新目标优先，待命点回转会被取消。
- 导航延迟和结束后回营地延迟会在每次排定时随机抖动上下 1 秒，最低为 0；例如设为 5 秒时，本次实际等待为 4、5 或 6 秒，并以实际秒数输出全自动日志。
- 回营后扫描延迟可在自动寻路页签配置，默认 10 秒；每次确认回到营地后同样随机抖动上下 1 秒，并以实际秒数输出全自动日志。
- `自动寻路` 页签提供持久化的 CE/FATE 导航偏移（码）配置，范围为 0-30，默认均为 15，设为 0 可关闭对应目标的随机偏移。两类目标均以中心点为圆心随机选择最终落点；配置了多路线时，先完成路线航点，再随机选择最终落点收尾。

## 依赖与限制

- vnavmesh 用于寻路。
- Lifestream 2.5.4.15 以上用于传送点 IPC。
- 本地构建环境可能出现 Dalamud 引用 warning；只要 `0 errors` 即视为构建通过。
- 导航调试关闭时，`导航调试:` 消息会被过滤。
- 全自动提示关闭时，`全自动:` 和 `全自动模式` 状态消息会被过滤。
- 异常和失败提示仍显示，不受以上两个开关影响。

## 导航与传送细节

- 新月岛内从营地点击目标导航时，不再比较“当前点到目标”的距离收益；以玩家附近 60 码内最近的水晶为锚，只要“目标推荐水晶 ≠ 锚水晶”就进入岛内传送流程。若玩家距锚水晶超过水平 4 码则先步行靠近，进入水平 4 码范围并稳定等待 2 秒后再传送；目标推荐水晶与锚水晶相同时直接步行。
- 多路线导航的首个航点按最终 Boss 目标选择传送点，避免首航点的距离判断覆盖已经确定的营地传送流程。
- 决定使用岛内传送后，若玩家水平距离 60 码内有传送水晶，直接步行到该水晶传送；附近没有水晶时才使用亚返回前往营地水晶。
- 附近水晶链路是“到 60 码内最近水晶 -> 进入水晶 4 码范围 -> 稳定等待 2 秒 -> Lifestream 传送 -> 继续步行导航”。
- 无附近水晶时的回营地链路是“亚返回 -> 到营地大水晶 -> 进入水晶 4 码范围 -> 稳定等待 2 秒 -> Lifestream 传送 -> 继续步行导航”，避免在营地落点附近直接步行到目标。
- 传送点 active 查询失败时，不会阻塞整个链路，只影响日志可见性。
- 自动新月岛史官目标导航与待命点流程互斥，前者优先。

## 上岛路线

- `/shiguan enter` / `/史官 上岛`：从任意地图导航到新月岛入口。
- 关键常量：图莱优菈 TerritoryType 1185、幻境村 1278、图莱优菈 AetheryteId 216、幻境村 AethernetId 239、入口坐标 `(-76.86, 5, -14.54)`。
- 流程：`Lifestream.Teleport(216, 0)` 回图莱优菈 → 判断读图结束、`IsBusy == false`、至少 2 秒、`GetActiveAetheryte() == 216`、玩家存在 → `AethernetTeleportById(239)` 到幻境村 → 最后一段步行导航（`fly=false`，不走自动上坐骑）到入口。
- 旧配置自动迁移：`TuliyollalAetheryteId 13→216`、`SolutionNineTerritoryType 1187→1278`、旧坐标 → 新坐标，`TuliyollalTerritoryType` 强制 1185。
- 图莱优菈内启动时另加 8 秒等待防同秒误传幻境村。
- 最后一段带 navmesh 三级吸附回退（120/300、180/600、260/1000）、4y 到达判定、离开地图取消、60s 超时、7s 未移动最多重试 3 次。
- 幻境村实际 TerritoryType 为 1278（旧文档 1187 是错的）；Lifestream IPC 详细签名见 `docs/lifestream-ipc.md`。

## 已完成的验证与清理

- 实机验证死亡复活流程：新月岛史官目标期间死亡只停止导航，复活后不回营地，目标结束后再回营地。
- 实机验证普通 FATE 到达前结束时 `Progress >= 100` 能立即触发目标结束流程。
- 实机验证导航途中 `currentMap` 短暂为空时不再误触发“开启时不在营地，先回营地”。
- 实机验证悬浮窗 `待命点` 按钮点击区域、tooltip 和聊天提示。
- 实机验证上岛状态机完整链路：图莱优菈内和岛外两种场景均正常。
- 实机验证 `ActiveAetheryte == 216` 门控 + 8 秒等待能稳定进入幻境村，不再出现 `Destination could not be found (3)`。
- 实机验证新月岛史官目标普通导航修复后能正常上坐骑。
- 清理 v0.2.0.7 引入的 FATE 扫描调试日志。
- 修复全自动 FATE 候选扫描：已自动记录的北征普通 FATE（例如 FateId 2076 奇美拉）可进入全自动导航候选，仍遵守目标勾选、导航延迟与战斗进度跳过设置。

## 未完成与待验证

- （多路线已实现，见下节）剩余待实机验证：路线导航到达判定、卡住重试、内置路线随版本分发。

## 自动进出岛

- 设置项位于自动寻路页签：启用开关、人数阈值、任务剩余时间阈值（分钟）、重新进岛延迟、目标南岛/北岛。
- `启用自动进出岛` 是运行期状态，不写入配置；插件重载或重新登录后默认关闭。
- 自动进出岛两个条件有独立勾选框，位置在该设置块内启用开关下方，可单独启用，也可同时启用。
- 条件使用“或”语义：任一已启用的条件满足时，停止当前导航并执行 `/pdr leaveduty`；两项都未勾选时不会自动离岛。
- 检测到已经离开新月岛后开始延迟；延迟结束后把目标地图写入 `LastSelectedMap`，前往图莱优菈/幻境村/入口。
- 到达入口后自动寻找 NPC `杰弗瑞` 并交互；`SelectString` 菜单按截图顺序选择：北征索引 `0`、北征两岐塔索引 `1`、南征索引 `2`。
- 重新进入新月岛后清理本轮状态，等待下一次阈值触发。
- 人数统计当前使用 ObjectTable 中 `Player`/`PlayerCharacter` 类型对象；任务时间使用 `PublicContentOccultCrescent.ContentTimeLeft`。

## 多路线方案（设计）

### 状态

已实现（v0.2.0.13+）：`BossRouteDto/BossRoutePointDto`、`config.BossRoutes`、`RouteCatalog`（内置预设 + 合并解析）、`/shiguan route export`、`VnavService.NavigateViaRoute` 状态机、自动导航集成、自动寻路页签"路线"配置区。待实机验证到达判定与卡住重试。

### 目标

为每个 Boss 手动配置 2~3 条路线（有序航点），自动/手动导航该 Boss 时随机选一条执行，以提高复杂地形（悬崖、水域、大门）下的寻路成功率。没有路线时回退现有单点导航。

### 数据模型

```
BossRoutePointDto { float X, float Y, float Z }
BossRoutePointKind { Normal, Forced }
BossRouteDto {
    ExpeditionMap Map;      // 南征/北征
    int BossId;             // 绑定 Boss
    int RouteIndex;         // 路线编号；自定义 UI 提供 3 条槽位，内置路线可以更多
    List<BossRoutePointDto> Points;   // 有序航点，首个应接近常用出发点；强制点绕过 Pathfind 直线移动
}
PluginConfiguration.BossRoutes: List<BossRouteDto>
```

`BossId` 与 `BossCatalog.GetBosses(map).Id` 一致（南征 1~17 / 北征 101~117）。

### 配置 UI（自动寻路页签新增"路线"区）

- 路线配置按 `CE 路线` / `FATE 路线` 分组，两个分组各自有 Boss 下拉选择（显示简称）；北征普通 FATE 位于 `FATE 路线` 分组。
- Boss 下拉框右侧提供 `标记` 按钮：按 `BossPositionCatalog` 内置坐标在游戏地图上放置 `<flag>`，方便录制路线时确认目标方位。
- 对所选 Boss 显示 `路线 1` / `路线 2` / `路线 3` 三个按钮（按钮上显示航点数量），点击按钮切换当前编辑路线。
- 每条路线操作按钮：`添加当前位置`（把 `LocalPlayer.Position` 追加为普通航点）、`测试路线`、`删除最后`、`清空`。
- 航点表操作列提供 `导` / `强` / `更` / `删`：`导` 从当前位置前往该航点；`强` 在普通/强制之间切换；`更` 把该航点坐标更正为当前角色坐标；`删` 删除该航点（仅自定义路线）。强制航点执行时不走 `Pathfind`，直接用 vnavmesh `Path.MoveTo` 向该坐标移动，适合高低差需要直接跳下去的位置。
- 改动即时 `config.Save()`，无需单独保存按钮。
- 建议提示：按"出发点 → 途经点 → 目标附近"顺序录制，首航点尽量靠近常用起点。

### Boss 坐标标记

- `BossPositionCatalog` 保存已验证的 Boss 世界坐标，数据来源为 DEBUG 页签导出的新月岛史官目标观测记录。
- 坐标格式为游戏世界坐标 `(X, Y, Z)`；FATE 记录有完整三维坐标，CE 动态事件记录当前只导出平面坐标 `(X, Z)`，内置时临时把高度 `Y` 记为 `0`。地图 `<flag>` 只依赖平面位置，高度不影响标记。
- 当前已内置北征普通 FATE、魔法罐与多数 CE 坐标；未采集到坐标的 Boss 点击 `标记` 会提示无固定坐标。
- 标记实现使用 `AgentMap.SetFlagMapMarker(TerritoryType, MapId, worldPosition)`；`MapId` 运行时从 Lumina `TerritoryType` 表读取，北征坐标当前使用 TerritoryType `1346`。

### 导航执行（VnavService）

状态机字段：
```
Vector3[]? routePoints;
int routePointIndex;
DateTime routePointStartedUtc;
DateTime lastRouteCheckUtc;
Vector3 routeLastPosition;
int routeStuckRetryCount;
Vector3 routeFinalTarget;
float? routeRandomRadius;
uint? routePreferredShardId;
bool routeDismountOnArrival;
```

`NavigateViaRoute(IReadOnlyList<BossRouteDto> routes, Vector3 finalTarget, bool fly, uint? preferredShardId, float? randomRadius, bool dismountOnArrival)`：

1. 从 `RouteCatalog.GetRoutes` 取该 Boss 的有效路线（Points 数 >= 2），随机选一条。
2. 无有效路线 → 回退现有 `NavigateTo` / `NavigateToRandomInRadius`。
3. 逐点执行：普通航点先 `SnapToNavmesh` 吸附（吸附偏差 > 8f 视为无网格，跳点），再由 `NavigateToInternal` 前往；强制航点直接调用 `Path.MoveTo`。首个普通航点按最终 Boss 目标完成传送判断和传送点选择，传送后再继续前往该航点；`OnFrameworkUpdate` 每 100ms 轮询。
4. 到达判定：普通航点 `HorizontalDistance(player, 当前航点) <= 8f` 时提前衔接下一航点，减少停顿；强制航点保持 `<= 4f`，到达后 `AdvanceRoutePoint` 推进下一航点。路线状态每 100ms 检查一次。
5. 卡住重试：当前航点超过 7 秒水平位移 < 2.5f → 重试当前点，最多 3 次；仍失败 → 放弃路线，直接导航到最终目标。
6. 全部航点走完后，CE 目标用 `NavigateToRandomInRadius`，普通目标用 `NavigateTo`。
7. 清空导航 `vnav.Stop()` / 新 `NavigateTo` 都会清路线状态机。

### 集成点

- `ChroniclerPlugin.Framework.NavigateAutoTargetOnce`：新增 `BossEntry? boss` 参数；存在该 Boss 路线时调用 `vnav.NavigateViaRoute`，否则保持原逻辑。
- `VnavService.NavigateToTarget` 统一手动导航判定：与全自动相同，仅在需要岛内传送且该 Boss 有路线时调用 `NavigateViaRoute`，否则回退单点导航。
- 主窗口 Boss `导航`、悬浮窗 FATE/CE `导航` 均接入 `NavigateToTarget`，可以使用自定义或内置路线。
- 清空导航 `vnav.Stop()` 需同时清理路线状态机。

### 注意事项

- 路线导航复用 `NavigateToInternal` 的 Lifestream 传送逻辑；首个普通航点使用最终 Boss 目标选择目的水晶，路线无需录制“去传送点 -> 传送”的航点。
- 随机选路线在每次开始导航时决定，导航过程中不切换。
- 配置为手动点选，不用文本导入导出（本阶段不做分享码兼容）。

### 内置预设路线（新用户开箱即用）

手动在 UI 里设置的路线只存在本地 `config.BossRoutes`，新用户拿到插件仍是空的。因此需要把验证过的路线"固化"进插件代码，随版本分发：

**双层数据源 + 合并解析**

- `RouteCatalog`（新增静态类，类似 `BossCatalog`）：保存内置预设路线，硬编码在插件里，随版本发布，所有用户自带。
  ```
  RouteCatalog.BuiltInRoutes: List<BossRouteDto>   // 内置（代码写死）
  ```
- `config.BossRoutes`：用户手动增补/覆盖路线（优先级高于内置）。
- 运行时解析按 `(Map, BossId, RouteIndex)` 合并：
  - 用户存在该键 → 用用户路线；
  - 否则用内置路线；
  - 都没有 → 无路线，回退单点导航。
- 合并逻辑收敛到 `RouteCatalog.GetRoutes(map, bossId)`，导航只调用这一个入口，不关心数据来自哪里。

**内置来源（避免手写坐标）**

1. 开发者在游戏里用 UI 录制路线，落到 `config.BossRoutes`。
2. 在自动寻路页签"路线"配置区的底部点击"复制内置路线代码"按钮，把当前全部路线序列化为 C# 代码片段（`Route(map, bossId, routeIndex, (x,y,z), ...)` 形式），一键复制到剪贴板；也可用命令 `/shiguan route export` 导出、`/shiguan route clear` 清空自定义路线。
3. 将复制的代码粘贴进 `RouteCatalog.cs` 的 `BuildRoutes()`，提交发布。
4. 新用户安装后即带内置路线，无需再录制；仍可手动增补覆盖（按 `(Map, BossId, RouteIndex)` 覆盖内置）。

**版本归属**

- 内置路线属于代码数据，随插件版本演进；不单独做路线文件下载。
- `RouteCatalog` 与 `BossCatalog` 一样参与发布，路线坐标以地面实测为准，注意坐标随游戏补丁可能漂移，需要时重新录制并更新内置数据。
