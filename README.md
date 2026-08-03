# Chronicler

Dalamud plugin plan for a local Crescent Isle historian tool. This document is written as a handoff for the next AI or developer taking over the implementation.

## Goal

Build a Dalamud plugin from scratch, using `E:\git\DalamudACT` as the structural reference, to record Crescent Isle CE/FATE appearance times locally and stay compatible with `https://xyd.zzmelon.com/`.

The plugin must support both maps:

- South chapter: `N` share-code prefix.
- North chapter: `B` share-code prefix.

The active chapter should switch automatically based on the current in-game `TerritoryType` ID. If the current territory is unknown, the UI should keep the last selected chapter and allow manual switching.

## Confirmed Requirements

- Local record storage inside the Dalamud plugin configuration.
- Support both South and North boss/CE lists from `xyd.zzmelon.com`.
- Automatically switch South/North based on map/territory ID.
- Record appearance time as local real-world time, exported as `HH:mm` for xyd compatibility.
- Automatically detect CE/FATE appearance through `IFateTable` where possible.
- Import xyd share codes.
- Export xyd share codes.
- Generate normal xyd shout text.
- Generate out-island shout text.
- Listen to game chat and parse existing player-sent shout text to synchronize local state.
- Do not automatically broadcast sync messages to game chat. The user requested listening/parsing existing manual shouts.
- UI should be simple: boss list, import/export controls, shout text box. Do not clone the whole website UI.

## Reference Project

Use `E:\git\DalamudACT` as the template/reference.

Important reference files:

- `E:\git\DalamudACT\DalamudACT\DalamudACT.csproj`
- `E:\git\DalamudACT\DalamudACT\DalamudACT.json`
- `E:\git\DalamudACT\DalamudACT\Plugin\ACT.cs`
- `E:\git\DalamudACT\DalamudACT\Plugin\ACT.Commands.cs`
- `E:\git\DalamudACT\DalamudACT\Plugin\ACT.Chat.cs`
- `E:\git\DalamudACT\DalamudACT\Plugin\ACT.Hooks.cs`
- `E:\git\DalamudACT\DalamudACT\Infrastructure\DalamudApi.cs`
- `E:\git\DalamudACT\DalamudACT\UI\PluginUI.cs`
- `E:\git\DalamudACT\DalamudACT\UI\Windows\MainWindow.cs`

Observed reference traits:

- Uses `Dalamud.NET.Sdk/15.0.0`.
- Targets Dalamud API level 15.
- Uses `IDalamudPlugin` as the plugin entry point.
- Uses a static `DalamudApi` service holder with `[PluginService]` fields.
- Splits main plugin logic with partial classes, e.g. commands, chat, hooks.
- Uses `Dalamud.Interface.Windowing.WindowSystem` for ImGui windows.
- Uses reflection in chat handling for API compatibility. Start simple, but copy this approach if direct `IChatGui.ChatMessage` signatures fail.

## Suggested Project Structure

```text
Chronicler/
  Chronicler.csproj
  Chronicler.json
  README.md
  Plugin/
    ChroniclerPlugin.cs
    ChroniclerPlugin.Commands.cs
    ChroniclerPlugin.Chat.cs
    ChroniclerPlugin.Framework.cs
  Configuration/
    PluginConfiguration.cs
  Infrastructure/
    DalamudApi.cs
    LogHelper.cs
  Features/
    CrescentIsle/
      ExpeditionMap.cs
      BossEntry.cs
      BossCatalog.cs
      BossRecordDto.cs
      CrescentStateService.cs
      XydShareCodeCodec.cs
      XydShoutParser.cs
      XydShoutGenerator.cs
      FateAppearanceDetector.cs
      TerritoryGate.cs
  UI/
    PluginUI.cs
    MainWindow.cs
```

## Suggested csproj

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
  <PropertyGroup>
    <AssemblyVersion>0.1.0.0</AssemblyVersion>
    <InternalName>Chronicler</InternalName>
    <RootNamespace>Chronicler</RootNamespace>
    <Description>Local Crescent Isle historian with xyd.zzmelon.com share-code and shout compatibility.</Description>
  </PropertyGroup>

  <PropertyGroup>
    <Platforms>x64;AnyCPU</Platforms>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <OutputPath>..\output\</OutputPath>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>

  <PropertyGroup>
    <DalamudLibPath>$(APPDATA)\XIVLauncherCN\addon\Hooks\dev\</DalamudLibPath>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <PlatformTarget>AnyCPU</PlatformTarget>
  </PropertyGroup>
</Project>
```

## Suggested Plugin Manifest

```json
{
  "Author": "YourName",
  "Name": "新月岛史官",
  "Punchline": "本地记录新月岛 CE/FATE 出现时间，并兼容 xyd 分享码和喊话。",
  "Description": "进入新月岛后自动记录南征/北征 CE/FATE 出现时间，支持导入/导出 xyd.zzmelon.com 分享码，生成喊话，并监听游戏内喊话自动同步状态。",
  "Tags": [
    "crescent",
    "xyd",
    "boss",
    "timer",
    "sharecode"
  ],
  "CategoryTags": [
    "Utility"
  ],
  "InternalName": "Chronicler",
  "ApplicableVersion": "any",
  "AssemblyVersion": "0.1.0.0",
  "DalamudApiLevel": 15
}
```

## xyd Compatibility Details

The website was inspected at:

- `https://xyd.zzmelon.com/`
- `https://xyd.zzmelon.com/js/app.js`

The current app is a Vue app and contains all CE lists, share-code encode/decode logic, shout generation, and import parsing.

### Share Code Format

Format:

```text
{map}{ver}{base_min_2chars}{bitmap_3chars}[{delta_2chars}...]
```

Fields:

- `map`: `N` for South, `B` for North.
- `ver`: currently `0`.
- `base_min`: earliest recorded time as minutes since 00:00, Base62 encoded with length 2.
- `bitmap`: 17-bit bitmap, Base62 encoded with length 3.
- `delta`: for each recorded CE, minute offset from `base_min`, Base62 encoded with length 2.

Base62 alphabet:

```text
0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz
```

Regex for finding share codes in arbitrary text:

```regex
[NB]0[A-Za-z0-9]{5,}
```

Important behavior:

- Empty South records encode to `N0` + `00` + `000`.
- Empty North records encode to `B0` + `00` + `000`.
- Decode by reading the map prefix and applying records to that map's 17-item list by index.
- The order of the boss list must never change without intentionally changing the codec compatibility.

### Shout Formats

Normal shout:

```text
/sh 鲨鱼 [08:00] 罐子 [09:15] ...
/sh 变形法师 [08:00] 亡灵法师 [--:--] ...
```

Out-island shout:

```text
/sh 当前史官准备离岛，复制本信息到xyd.zzmelon.com继承我的记录吧->鲨鱼 [08:00] 罐子 [09:15] ...
```

Share-code out-island shout:

```text
/sh 当前史官准备离岛，复制以下分享码到xyd.zzmelon.com继承我的南征记录吧->N0xxxxx
/sh 当前史官准备离岛，复制以下分享码到xyd.zzmelon.com继承我的北征记录吧->B0xxxxx
```

Time-entry regex copied from the website logic:

```regex
([^\s]+)\s*\[((?:[01]\d|2[0-3]):[0-5]\d|--:--)\]
```

Parsing priority:

1. If a share code exists, decode and apply by share-code map prefix.
2. Otherwise parse shout time entries against the active map.
3. If the active map is unknown, try to infer from unique abbreviations.
4. Do not apply ambiguous abbreviations across maps when the active map is unknown. Examples include `岛主`, `左下罐`, `右上罐`.

## South Boss List

This list must keep the website order.

| Index | Id | Abbreviation | Name | Trigger |
| ---: | ---: | --- | --- | --- |
| 0 | 1 | 鲨鱼 | 传说中的鲨鱼——尼姆瓣齿鲨 | Lv.7 新月小瓣齿鲨 x:17 y:19 |
| 1 | 2 | 罐子 | 防卫指令——指令罐 | Lv.7 新月巨像 x:10 y:8 |
| 2 | 3 | 金钱龟 | 贩卖诅咒的商贩——金钱龟 | Lv.7 新月刻托斯 x:23 y:6 |
| 3 | 4 | 加鲁拉 | 厌鸟巨兽——进化加鲁拉 | Lv.1 新月加鲁拉 x:30 y:8 |
| 4 | 5 | 黑陆行鸟 | 黑色连队——黑陆行鸟&黑色彗星 | Lv.14 新月猎豹 x:30 y:28 |
| 5 | 6 | 新月骑士 | 石质骑士团——新月骑士群 | Lv.4 新月马洛里石 x:32 y:18 |
| 6 | 7 | 双足狮人 | 双足狮人——跃立狮 | Lv.5 新月风扇 x:32 y:24 |
| 7 | 8 | 复原狮像 | 城塞守卫——复原狮像 | Lv.20 新月立狮 x:36 y:22 |
| 8 | 9 | 水晶龙 | 拟造使魔——水晶龙 | Lv.19 新月巨钳虾 x:12 y:30 |
| 9 | 10 | 土偶 | 双极的造物——神秘土偶 | Lv.13 新月比布鲁斯 x:4 y:24 |
| 10 | 11 | 拟鸟枝 | 昏暗妖魂——鬼火苗 | Lv.19 新月哈耳庇厄鸟妖 x:12 y:15 |
| 11 | 12 | 死亡爪 | 潜影撕裂者——死亡爪 | Lv.16 新月黑卫 x:33 y:31 |
| 12 | 13 | 狂战士 | 愤怒的人造人——新月狂战士 | Lv.17 新月恶魔兵卒 x:32 y:33 |
| 13 | 14 | 夺心魔 | 脑髓爱好者——夺心魔 | Lv.15 新月鬼鱼 x:26 y:34 |
| 14 | 15 | 岛主 | 挣脱封印的大妖异——回廊恶魔 | Lv.20 新月墨渍 x:12 y:33 |
| 15 | 16 | 右上罐 | 幸福的魔法罐（右上罐） | 自然刷新 x:26, y:17 |
| 16 | 17 | 左下罐 | 瑟瑟发抖的魔法罐（左下罐） | 自然刷新 x:11.8, y:32 |

## North Boss List

This list must keep the website order.

| Index | Id | Abbreviation | Name | Trigger |
| ---: | ---: | --- | --- | --- |
| 0 | 101 | 变形法师 | 拟态使魔——变形法师 | 自然刷新 x:31.4, y:15.2 |
| 1 | 102 | 亡灵法师 | 天道好轮回——亡灵法师 | 自然刷新 x:25.9, y:4.2 |
| 2 | 103 | 古术魔典 | 禁书化形——古术魔典 | 自然刷新 x:34.6, y:34.6 |
| 3 | 104 | 阿尔戈尔 | 暴食咒鬼——阿尔戈尔 | 自然刷新 x:36.7, y:21.4 |
| 4 | 105 | 岛主 | 孤岛的绑架犯——诱拐魔 | Lv.27 新月幽灵 x:18.4, y:4.2 |
| 5 | 106 | 雪石膏之剑 | 纯白守护者——雪石膏之剑 | Lv.38 新月祸蛛蝎 x:11.1, y:8.6 |
| 6 | 107 | 小小法师 | 魔法军团——小小法师 | 自然刷新 x:24.5, y:35.7 |
| 7 | 108 | 神木巨人 | 求道的人造人——神木巨人 | 自然刷新 x:13.6, y:35.4 |
| 8 | 109 | 魔许德拉 | 苏醒的多头龙——魔许德拉 | 自然刷新 x:19.8, y:31.1 |
| 9 | 110 | 负隅宝石兽 | 叛逆使魔——负隅宝石兽 | 自然刷新 x:26.2, y:28.5 |
| 10 | 111 | 惨白魔人 | 诅咒的继承者——惨白魔人 | Lv.29 新月胡瓦西 x:37.6, y:10.2 |
| 11 | 112 | 二重身 | 魔女复制体--卡洛菲斯提莉二重身 | 新月帕尔忒诺珀(推测1)/Lv39 黑卫(推测2) x:17.1, y:20.1 |
| 12 | 113 | 提蔛 | 四颚斧花--提蔛 | Lv.46 新月瓦魔蛾(有报告，位于x7.5, y2.5) x:4.0, y:10.2 |
| 13 | 114 | 赤龙 | 暗红尸骸--赤龙 | Lv.34 大角牛 x:7.7, y:24.4 |
| 14 | 115 | 阿剌克涅 | 残暴的母蜘蛛--新月阿剌克涅 | 待收集 x:24.8, y:18.6(推测) |
| 15 | 116 | 左下罐 | 被吹飞的魔法罐（左下罐） | 自然刷新 x:11.3, y:26.2 |
| 16 | 117 | 右上罐 | 被欺负的魔法罐（右上罐） | 自然刷新 x:26.1, y:12.1 |

## Data Model

Recommended enum:

```csharp
internal enum ExpeditionMap
{
    South,
    North,
}
```

Recommended boss record:

```csharp
internal sealed record BossEntry(
    ExpeditionMap Map,
    int Id,
    int Index,
    BossEventKind Kind,
    string Abbreviation,
    string Name,
    string Trigger,
    ushort? FateId,
    string[] ObjectNameAliases);

internal enum BossEventKind
{
    CriticalEncounter,
    Fate,
}
```

Recommended saved record:

```csharp
[Serializable]
public sealed class BossRecordDto
{
    public string Map { get; set; } = "";
    public int BossId { get; set; }
    public DateTime? AppearedAtLocal { get; set; }
}
```

Store full local `DateTime?` internally. Export only `HH:mm` to xyd-compatible shout/share code.

## Territory Switching

Known Territory IDs:

- North: `1346` (confirmed)
- South: `1252` (confirmed)

Add config fields:

```csharp
public List<uint> SouthTerritoryIds = new();
public List<uint> NorthTerritoryIds = new();
public ExpeditionMap LastSelectedMap = ExpeditionMap.South;
```

Recommended gate:

```csharp
internal static class TerritoryGate
{
    public static ExpeditionMap? ResolveMap(uint territoryType, PluginConfiguration config)
    {
        if (config.SouthTerritoryIds.Contains(territoryType))
            return ExpeditionMap.South;

        if (config.NorthTerritoryIds.Contains(territoryType))
            return ExpeditionMap.North;

        return null;
    }
}
```

UI should show current `TerritoryType` so the developer can enter each island and record the IDs.

## Automatic Appearance Detection

Recommended MVP approach: use `IFramework.Update` plus `IFateTable` polling.

Reasoning:

- `IFateTable` is the Dalamud API 15 service for currently available FATE events.
- It exposes `FateId`, name, state, start epoch, duration, remaining time, territory, position and level.
- CE/FATE appearance time can be recorded from `IFate.StartTimeEpoch` where available, or estimated from duration/time remaining.

Detector behavior:

- Run only when `TerritoryGate.ResolveMap(...)` returns South or North.
- Poll every 250-500 ms.
- Scan `DalamudApi.FateTable` for `IFate` entries in `Preparing` or `Running` state.
- Match by known `FateId` first once IDs are collected, then by name/alias fallback.
- Record the first time a matching active FATE appears in the current map.
- Do not record completion time; the xyd-compatible timestamp is the CE/FATE appearance time.
- Add Fate IDs to catalog after in-game verification to avoid name-matching false positives.

If some CE entries are not exposed through `IFateTable`, investigate whether they require addon/event/network observation later. Do not reintroduce HP/death-based detection for xyd timestamps.

## Chat Synchronization

Start with direct `IChatGui.ChatMessage` if the current API signature compiles. If not, copy/adapt the reflection-based compatibility layer from `DalamudACT\Plugin\ACT.Chat.cs`.

Rules:

- Never set `isHandled = true`; do not hide player chat.
- Only parse if `Configuration.ListenChat` is enabled.
- If text contains `N0...` or `B0...`, decode and apply by prefix.
- If text contains shout time entries, parse against current active map.
- Print a local plugin message after successful sync, e.g. `[新月岛史官] 已从聊天同步 8 个记录。`
- Do not automatically send `/sh` or other public chat commands.

## UI Requirements

The UI should be simple and pragmatic.

Suggested top area:

- Current territory ID.
- Resolved map: South, North, or unknown.
- Manual map switch buttons.
- Toggles: enable plugin, listen chat, auto detect appearances.

Suggested table columns:

- Abbreviation.
- Name.
- Appearance time, shown as `HH:mm` or `--:--`.
- Trigger.
- Buttons: record now, clear.

Suggested import/export area:

- Multiline import text box.
- Apply import button.
- Generated output text box.
- Buttons: generate normal shout, generate out-island shout, generate share code, generate share-code out-island shout.

## Commands

Commands:

```text
/shiguan
/shiguan show
/shiguan clear
/shiguan import <share code or shout text>
/shiguan shout
/shiguan code
```

Behavior:

- `/shiguan` and `/shiguan show`: open the main UI.
- `/shiguan shout`: print generated normal shout locally.
- `/shiguan code`: print active map share code locally.
- `/shiguan import ...`: parse text and update local records.

The main UI includes a `FATE/CE 调试区` section for in-game data collection. Enter the target map, wait for CE/FATE entries to appear, then inspect or output current `IFateTable` rows: `FateId`, name, state, start epoch/local time, duration, remaining time, level, position, map icon and territory.

The UI also includes an `已观测 FATE` section. This records every active `IFateTable` row seen in a recognized map, including entries that are not part of the fixed xyd 17-slot share-code list.

Critical Engagements do not appear in `IFateTable`. Following BOCCHI's implementation, the plugin reads `PublicContentOccultCrescent.GetInstance()->DynamicEventContainer.Events` from `FFXIVClientStructs.FFXIV.Client.Game.InstanceContent`. Non-inactive `DynamicEvent` rows are shown in `CE 动态事件记录`, including `DynamicEventId`, `Name`, `State`, `StartTimestamp`, remaining/duration seconds, progress, participants, map marker and event types. CE appearance should be taken from the `Register` state.

Known observed North FATE rows:

| FateId | Name | Territory | Notes |
| ---: | --- | ---: | --- |
| 2075 | 诅咒宝珠——邪瞳 | 1346 | Observed running at 16:46, duration 1200, icon 60502. |
| 2082 | 驾驭自然的巨兽——呼风狮鹫 | 1346 | Observed running at 16:48. |

## Implementation Order

1. Create the minimal Dalamud project using the reference project style.
2. Add manifest and confirm the plugin loads.
3. Add `/xyd` command and simple `WindowSystem` UI.
4. Add `ExpeditionMap`, `BossEntry`, and `BossCatalog` with both lists.
5. Add configuration and state service with persistent records per map.
6. Implement xyd share-code encode/decode and test against known generated values from the website.
7. Implement shout generation and shout parser.
8. Add UI import/export controls.
9. Add chat listening and local synchronization.
10. Add territory display and territory-based South/North switching.
11. Enter both maps in game, record actual Territory IDs into defaults or config.
12. Add `IFateTable`-based automatic appearance detection.
13. Enter both maps in game, verify Fate IDs/names and add catalog IDs/aliases.
14. Add addon/event/network detection only if `IFateTable` is insufficient.

## Known Risks And Open Items

- Current local build needed explicit Dalamud DLL references in `Chronicler.csproj` because `Dalamud.NET.Sdk` reported unresolved automatic references on this machine even though DLLs exist in `XIVLauncherCN\addon\Hooks\dev`. `dotnet build Chronicler.csproj` succeeds, but emits MSBuild reference warnings due to this workaround.
- Actual South/North `TerritoryType` IDs are not known yet and must be captured in game.
- FATE IDs and names for every CE/FATE need in-game verification; website display names may not exactly match game data names.
- Some entries may not appear in `IFateTable` or may need separate CE-specific detection if the game does not expose them as normal FATE rows.
- Share codes store only `HH:mm`, not a date. Local state should keep full `DateTime`, but imported xyd times can only be reconstructed for the current local date.
- Website compatibility depends on list order and share-code version. If `xyd.zzmelon.com` changes its data or codec, update this plugin.
- Avoid aggressive disappear-only detection until tested, because CE objects can unload due to distance/phasing.

## Minimal MVP Definition

The first usable version should include:

- Both South and North lists.
- Manual South/North switch plus automatic switch by configured territory IDs.
- Persistent local records.
- xyd share-code import/export for `N0` and `B0`.
- xyd shout import/generation.
- Chat listening and synchronization.
- Simple UI.
- Current territory ID display.

Automatic appearance detection is implemented with `IFateTable` polling, but still needs in-game verification of Fate IDs and names.

## Session Log — 2026-07-29

### Changes Made

1. **LogHelper** (`Infrastructure/LogHelper.cs`)
   - Prefix `CH` → `SH` (BoxedLetterC → BoxedLetterS)
   - Color `14` (yellow) → `37` (blue)

2. **Floating Status Window** (`UI/FloatingStatusWindow.cs`)
   - `BgAlpha` 0.88 → 0.8 (semi-transparent background)
   - Text color scheme: names in yellow (`Vector4(1f, 0.85f, 0.3f, 1f)`), section headers and status/progress/time in white
   - Removed `#ID` prefix from displayed names
   - Fixed `Utf8String` → `string` conversion via `.ToString()`

3. **Commands** (`Plugin/ChroniclerPlugin.Commands.cs`)
   - Added command alias `/史官` registered alongside `/shiguan`

4. **Floating Status Window Toggles** (already existed in `MainWindow.cs`)
   - "显示 FATE/CE 悬浮窗" checkbox at `MainWindow.cs:86`
   - "锁定悬浮窗" checkbox at `MainWindow.cs:94`

### Pending / Next Steps

1. **Auto flag + vnav navi** ✓ Done — `VnavService` wraps IPC calls, flag button shows on each FATE/CE row in floating window. When vnav is loaded, shows "导航" button (place flag + navigate). When vnav not loaded, shows "插旗" button (place flag only).
2. Continue collecting North (1346) FATE IDs and CE DynamicEventIds
3. Verify if CE appears in IFateTable for magic pot-type CEs

### Build

- `dotnet build Chronicler.csproj -c Release -o output`
- Output: `E:\git\Chronicler\output\Chronicler.dll`
- Known: Dalamud SDK reference warnings are expected on this machine; compilation succeeds with 0 errors.

## Session Log — 2026-07-29 Continued

### Changes Made

1. **CE/FATE detection and matching**
   - Split `BossEntry` into `BossEventKind.CriticalEncounter` and `BossEventKind.Fate` so CE and FATE auto-navigation lists no longer show identical content.
   - CE detection and auto-navigation now use `DynamicEventId` exact matching first, then strict name/alias fallback via `BossCatalog.MatchCriticalEncounter`.
   - FATE detection only scans catalog entries marked as FATE.
   - This prevents pot FATE entries from being recorded through the CE detector while still allowing CE navigation when game `DynamicEventId` differs from the xyd list index.

2. **Auto navigation and dependency UX**
   - Added `VnavService` integration for vnavmesh and Lifestream.
   - Navigation flow supports walking to a source shard, Lifestream aethernet teleport, mount roulette, then walking to the target.
   - Added all-auto mode with FATE priority, CE fallback, and return-to-camp after target end.
   - Added dependency status labels for `vnavmesh` and `Lifestream`.

3. **Floating and main UI**
   - Floating window shows current FateTable FATEs and DynamicEventContainer CEs with manual navigation, return-to-camp, and all-auto toggle.
   - Auto-navigation CE/FATE folds now show the full configured Boss lists by type, with live state looked up from real game sources.
   - Moved observed FATE/debug sections out of the record/share fold so they are visible whenever `显示调试区` is enabled.
   - Added a `新月岛：北征之章 信息整理` button linking to `https://bbs.nga.cn/read.php?tid=47269383`.

4. **Chat and debug output**
   - Added island ID capture from chat and display in the UI.
   - Navigation step messages are now gated by the `导航调试` toggle. Failure/error messages remain visible.

### Build

- `dotnet build "E:\git\Chronicler\Chronicler.csproj" -c Release -o "E:\git\Chronicler\output"`
- Output: `E:\git\Chronicler\output\Chronicler.dll`
- Known: Dalamud SDK reference warnings are expected on this machine; compilation succeeds with 0 errors.
