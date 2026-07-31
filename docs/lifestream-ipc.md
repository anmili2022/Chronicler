# Lifestream IPC 参考

Lifestream (`E:\git\Lifestream\Lifestream\IPC\IPCProvider.cs`) 通过 EzIPC 自动注册，IPC 名称为 `Lifestream.{MethodName}`。

## 常用 IPC

| IPC 名称 | 签名 | 参数 |
|----------|------|------|
| `Lifestream.Teleport` | `ICallGateSubscriber<uint, byte, bool>` | `(uint aetheryteId, byte subIndex)` → `bool` 是否成功 |
| `Lifestream.AethernetTeleportByPlaceNameId` | `ICallGateSubscriber<uint, bool>` | `(uint placeNameRowId)` → `bool` |
| `Lifestream.AethernetTeleportById` | `ICallGateSubscriber<uint, bool>` | `(uint aethernetSheetRowId)` → `bool` |
| `Lifestream.IsBusy` | `ICallGateSubscriber<bool>` | `()` → `bool` 是否忙 |
| `Lifestream.Abort` | `ICallGateSubscriber<object>` | `()` → `void` 取消当前操作 |
| `Lifestream.GetActiveAetheryte` | `ICallGateSubscriber<uint>` | `()` → `uint` 当前激活的 AetheryteId |
| `Lifestream.GetActiveCustomAetheryte` | `ICallGateSubscriber<uint>` | `()` → `uint` 当前激活的自定义 AetheryteId |

### 使用示例

```csharp
using Dalamud.Plugin.Ipc;

// 订阅
var teleport = pi.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
var aethernetById = pi.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportById");
var isBusy = pi.GetIpcSubscriber<bool>("Lifestream.IsBusy");
var abort = pi.GetIpcSubscriber<object>("Lifestream.Abort");

// 传送到图莱优菈（AetheryteId = 216）
var ok = teleport.InvokeFunc(216, 0);

// 都市传送（需在同一城市范围内）
aethernetById.InvokeFunc(239); // 幻境村

// 检查是否忙
if (isBusy.InvokeFunc())
    abort.InvokeAction();
```

## 安全初始化

Lifestream 可能比你的插件后加载，建议按需初始化：

```csharp
private ICallGateSubscriber<uint, byte, bool>? teleport;
private ICallGateSubscriber<uint, bool>? aethernetById;
private ICallGateSubscriber<bool>? lifestreamIsBusy;

private bool EnsureLifestreamIpc()
{
    if (teleport != null && aethernetById != null && lifestreamIsBusy != null)
        return true;

    if (!pluginInterface.InstalledPlugins.Any(p => p.InternalName == "Lifestream" && p.IsLoaded))
        return false;

    teleport ??= pluginInterface.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
    aethernetById ??= pluginInterface.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportById");
    lifestreamIsBusy ??= pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
    return true;
}
```

## 新月岛路线

### 关键常量

```csharp
private const uint TuliyollalTerritoryType = 1185;
private const uint OccultVillageTerritoryType = 1278;
private const uint TuliyollalAetheryteId = 216;
private const uint OccultVillageAethernetId = 239;
private static readonly Vector3 CrescentIsleEntrance = new(-76.86f, 5f, -14.54f);
```

### 多步骤流程

1. **图莱优菈**：`Teleport(216, 0)` — 传送到图莱优菈
2. **等待读图完成**：`TerritoryType == 1185` 且 `IsBusy() == false`
3. **幻境村**：`AethernetTeleportById(239)` — 都市传送到幻境村
4. **等待传送完成**：`TerritoryType == 1278` 且 `IsBusy() == false`
5. **导航**：`vnavmesh.PathfindAndMoveTo((-76.86, 5, -14.54), false)` — 步行到新月岛入口

### 注意事项

- 传图莱优菈用 `Lifestream.Teleport(216, 0)`，旧的 ID `13` 已废弃
- 幻境村传送用 `Lifestream.AethernetTeleportById(239)`，不要用 `AethernetTeleport("珠串万货街")`
- 幻境村内必须使用步行导航（`fly = false`），不应上坐骑
- 用 `Framework.Update` 驱动状态机，不要在按钮回调里同步等待
- 建议用 `Lifestream.IsBusy` 配合 `BetweenAreas` 判断传送是否完成

### 状态机参考

```csharp
private enum RouteStep { None, WaitingTuliyollal, WaitingOccultVillage, MovingToEntrance }
private RouteStep routeStep;
private DateTime stepStartedUtc;

public void Go()
{
    if (!EnsureLifestreamIpc()) { /* 报错 */ return; }
    teleport.InvokeFunc(TuliyollalAetheryteId, 0);
    routeStep = RouteStep.WaitingTuliyollal;
    stepStartedUtc = DateTime.UtcNow;
}

// 在 Framework.Update 中驱动
private void ProcessRoute()
{
    if (routeStep == RouteStep.None) return;
    if (DateTime.UtcNow - stepStartedUtc > TimeSpan.FromSeconds(90))
        { /* 超时，重置 */ return; }
    if (BetweenAreas) return;

    switch (routeStep)
    {
        case RouteStep.WaitingTuliyollal:
            if (TerritoryType == TuliyollalTerritoryType && !lifestreamIsBusy.InvokeFunc())
            {
                aethernetById.InvokeFunc(OccultVillageAethernetId);
                routeStep = RouteStep.WaitingOccultVillage;
                stepStartedUtc = DateTime.UtcNow;
            }
            break;

        case RouteStep.WaitingOccultVillage:
            if (TerritoryType == OccultVillageTerritoryType && !lifestreamIsBusy.InvokeFunc())
            {
                pathfindAndMoveTo.InvokeFunc(CrescentIsleEntrance, false);
                routeStep = RouteStep.MovingToEntrance;
            }
            break;
    }
}
```

## 新月岛内部传送点

| 地图 | 名称 | PlaceNameId / AetheryteId | 坐标 |
|------|------|---------------------------|------|
| 南征 | 营地 | PlaceNameId 4944 | (830.75, 72.98, -695.98) |
| 南征 | 浪人营 | PlaceNameId 4936 | (-173.02, 8.19, -611.14) |
| 南征 | 结晶洞窟 | PlaceNameId 4929 | (-358.14, 101.98, -120.96) |
| 南征 | 古木林地 | PlaceNameId 4930 | (306.94, 105.18, 305.65) |
| 南征 | 石沼 | PlaceNameId 4942 | (-384.12, 99.20, 281.42) |
| 北征 | 北部调查队营地 | 69420405 | (880.00, 259.74, 880.06) |
| 北征 | 卡纳克城塞 | 69420406 | (451.68, 70.93, 528.84) |
| 北征 | 沉没圣堂前 | 69420407 | (357.67, 45.77, -554.31) |
| 北征 | 浮游遗迹 | 69420408 | (-547.25, 68.00, 594.40) |
| 北征 | 腐坏的街道前 | 69420409 | (-388.57, 41.22, -440.52) |
| 北征 | 妖火渔村 | 69420410 | (-13.36, 3.14, -40.51) |

南征使用 `AethernetTeleportByPlaceNameId`（PlaceNameId），北征使用 `AethernetTeleportById`（自定义 AetheryteId）。

## TerritoryType

| 区域 | TerritoryType |
|------|--------------|
| 图莱优菈 | 1185 |
| 幻境村 | 1278 |
| 新月岛（南征） | 1252（可配置） |
| 新月岛（北征） | 1346（可配置） |

## 关键 IPC 注册源码

来源：`E:\git\Lifestream\Lifestream\IPC\IPCProvider.cs`

```csharp
[EzIPC] public bool Teleport(uint destination, byte subIndex)
    => S.TeleportService.TeleportToAetheryte(destination, subIndex);

[EzIPC] public bool AethernetTeleportByPlaceNameId(uint placeNameRowId)
    => TeleportService.AethernetTeleport(TelepoService.GetAethernet(placeNameRowId, TerritoryId), false);

[EzIPC] public bool AethernetTeleportById(uint aethernetSheetRowId)
    => TeleportService.AethernetTeleportRow(aethernetSheetRowId, false);

[EzIPC] public bool IsBusy() => TeleportService.CurrentState != 0;

[EzIPC] public void Abort() => TeleportService.Abort();

[EzIPC] public uint GetActiveAetheryte() { ... }

[EzIPC] public uint GetActiveCustomAetheryte() { ... }
```
