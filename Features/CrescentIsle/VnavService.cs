using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Chronicler;

internal sealed class VnavService : IDisposable
{
    private static readonly Version MinimumLifestreamVersion = new(2, 5, 4, 15);
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly PluginConfiguration config;
    private readonly ICallGateSubscriber<bool> isReady;
    private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindAndMoveTo;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> nearestPoint;
    private readonly ICallGateSubscriber<object> stop;

    // 可选 Lifestream IPC（不强制依赖）
    private ICallGateSubscriber<uint, bool>? aethernetTeleportById;
    private ICallGateSubscriber<uint, bool>? aethernetTeleportByPlaceNameId;
    private ICallGateSubscriber<bool>? lsIsBusy;
    private ICallGateSubscriber<object>? lsAbort;
    private ICallGateSubscriber<uint>? lsGetActiveAetheryte;
    private ICallGateSubscriber<uint>? lsGetActiveCustomAetheryte;
    private bool lifestreamAvailable;
    private string lifestreamStatus = "未安装";

    // 全岛传送点 (PlaceNameId=0 表示暂未找到 ID，跳过传送)
    private static readonly List<(ExpeditionMap Map, Vector3 Pos, uint Id, bool IsPlaceNameId)> Shards = new()
    {
        // 南岛
        (ExpeditionMap.South, new Vector3(830.75f, 72.98f, -695.98f), 4944, true), // BaseCamp
        (ExpeditionMap.South, new Vector3(-173.02f, 8.19f, -611.14f), 4936, true), // TheWanderersHaven
        (ExpeditionMap.South, new Vector3(-358.14f, 101.98f, -120.96f), 4929, true), // CrystallizedCaverns
        (ExpeditionMap.South, new Vector3(306.94f, 105.18f, 305.65f), 4930, true), // Eldergrowth
        (ExpeditionMap.South, new Vector3(-384.12f, 99.20f, 281.42f), 4942, true), // Stonemarsh
        // 北岛
        (ExpeditionMap.North, new Vector3(880.00f, 259.74f, 880.06f), 69420405, false), // 北部调查队营地
        (ExpeditionMap.North, new Vector3(451.68f, 70.93f, 528.84f), 69420406, false),  // 卡纳克城塞
        (ExpeditionMap.North, new Vector3(357.67f, 45.77f, -554.31f), 69420407, false), // 沉没圣堂前
        (ExpeditionMap.North, new Vector3(-547.25f, 68.00f, 594.40f), 69420408, false), // 浮游遗迹
        (ExpeditionMap.North, new Vector3(-388.57f, 41.22f, -440.52f), 69420409, false),// 腐坏的街道前
        (ExpeditionMap.North, new Vector3(-13.36f, 3.14f, -40.51f), 69420410, false),   // 妖火渔村
    };

    public VnavService(IDalamudPluginInterface pi, PluginConfiguration config)
    {
        pluginInterface = pi;
        this.config = config;
        isReady = pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        pathfindAndMoveTo = pi.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        nearestPoint = pi.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        stop = pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop");

        TryInitializeLifestream();

        DalamudApi.Framework.Update += OnFrameworkUpdate;
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnSelectYesnoPostSetup);
    }

    private void TryInitializeLifestream()
    {
        if (lifestreamAvailable)
            return;

        var lifestreamPlugin = pluginInterface.InstalledPlugins.FirstOrDefault(plugin => plugin.InternalName == "Lifestream");
        if (lifestreamPlugin == null)
        {
            SetLifestreamStatus("未安装", "Lifestream 未安装，不使用传送导航");
        }
        else if (!lifestreamPlugin.IsLoaded)
        {
            SetLifestreamStatus($"已安装 {lifestreamPlugin.Version}，未加载", "Lifestream 未加载，不使用传送导航");
        }
        else if (lifestreamPlugin.Version < MinimumLifestreamVersion)
        {
            SetLifestreamStatus($"版本过低 {lifestreamPlugin.Version}，需要 {MinimumLifestreamVersion}+", $"Lifestream 版本过低: {lifestreamPlugin.Version}，需要 {MinimumLifestreamVersion}+，不使用传送导航");
        }
        else
        {
            try
            {
                lsAbort = pluginInterface.GetIpcSubscriber<object>("Lifestream.Abort");
                lsIsBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
                lsGetActiveAetheryte = pluginInterface.GetIpcSubscriber<uint>("Lifestream.GetActiveAetheryte");
                lsGetActiveCustomAetheryte = pluginInterface.GetIpcSubscriber<uint>("Lifestream.GetActiveCustomAetheryte");
                aethernetTeleportById = pluginInterface.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportById");
                aethernetTeleportByPlaceNameId = pluginInterface.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportByPlaceNameId");
                var dummy = lsIsBusy.InvokeFunc();
                lifestreamAvailable = true;
                SetLifestreamStatus($"已加载 {lifestreamPlugin.Version}", $"Lifestream IPC 可用，版本 {lifestreamPlugin.Version}");
            }
            catch
            {
                SetLifestreamStatus("IPC 不可用", "Lifestream IPC 不可用，不使用传送导航");
            }
        }
    }

    private void SetLifestreamStatus(string status, string logMessage)
    {
        if (lifestreamStatus == status)
            return;

        lifestreamStatus = status;
        LogHelper.Info(logMessage);
    }

    private Vector3? pendingTarget;
    private bool pendingFly;
    private DateTime pendingStartedUtc;
    private DateTime lastPendingCheckUtc = DateTime.MinValue;
    private DateTime lastPendingDebugUtc = DateTime.MinValue;
    private Vector3? pendingMoveTarget;
    private bool pendingMoveFly;
    private DateTime pendingMoveStartedUtc;
    private DateTime lastMoveCheckUtc = DateTime.MinValue;
    private DateTime lastMountAttemptUtc = DateTime.MinValue;
    private Vector3? pendingDismountTarget;
    private DateTime pendingDismountStartedUtc;
    private DateTime lastDismountAttemptUtc = DateTime.MinValue;
    private bool pendingDismountArrivedLogged;
    private bool pendingDismountFired;
    private Vector3? pendingTeleportTarget;
    private bool pendingTeleportFly;
    private Vector3 pendingTeleportSourcePos;
    private uint pendingTeleportDestinationId;
    private bool pendingTeleportDestinationIsPlaceNameId;
    private DateTime pendingTeleportStartedUtc;
    private DateTime lastTeleportCheckUtc = DateTime.MinValue;
    private DateTime lastTeleportDebugUtc = DateTime.MinValue;
    private bool pendingReturnConfirm;
    private DateTime pendingReturnConfirmStartedUtc;
    private Vector3? pendingReturnNavigationTarget;
    private ExpeditionMap pendingReturnNavigationMap;
    private DateTime pendingReturnNavigationStartedUtc;
    private DateTime? pendingReturnNavigationBaseCampUtc;
    private bool pendingReturnNavigationSawBetweenAreas;
    private DateTime? pendingReturnNavigationConfirmedUtc;

    public bool IsReady
    {
        get
        {
            try { return isReady.InvokeFunc(); }
            catch { return false; }
        }
    }

    public bool IsLifestreamAvailable
    {
        get
        {
            TryInitializeLifestream();
            return lifestreamAvailable;
        }
    }

    public string LifestreamStatus
    {
        get
        {
            TryInitializeLifestream();
            return lifestreamStatus;
        }
    }

    public static uint? GetPreferredShardIdForFate(ushort fateId)
        => fateId == 2075 ? 69420406u : null;

    public static uint? GetPreferredShardIdForCriticalEncounter(ExpeditionMap map, int bossIndex)
        => map == ExpeditionMap.North && bossIndex == 3 ? 69420406u : null;

    public unsafe void NavigateTo(Vector3 dest, bool fly = false, uint? preferredShardId = null, bool dismountOnArrival = false)
    {
        ClearPendingNavigation();
        TryInitializeLifestream();

        try
        {
            Vector3 target = dest;
            var snapped = nearestPoint.InvokeFunc(dest, 5f, 5f);
            if (snapped.HasValue)
                target = snapped.Value;

            ConfigureDismountOnArrival(target, dismountOnArrival);

            if (aethernetTeleportById != null || aethernetTeleportByPlaceNameId != null)
            {
                var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
                if (playerPos.HasValue && Vector3.Distance(playerPos.Value, target) > 80f)
                {
                    var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
                    if (!currentMap.HasValue)
                    {
                        StartMove(target, fly);
                        return;
                    }

                    var map = currentMap.Value;
                    var nearest = preferredShardId.HasValue ? FindShardById(map, preferredShardId.Value) : FindNearestShard(map, target);
                    nearest ??= FindNearestShard(map, target);
                    if (nearest.HasValue && nearest.Value.Id != 0)
                    {
                        var distToShard = Vector3.Distance(playerPos.Value, nearest.Value.Pos);
                        var targetDistToShard = Vector3.Distance(target, nearest.Value.Pos);
                        DebugChat($"导航调试: 地图={map} 最近传送点 ID={nearest.Value.Id} 类型={(nearest.Value.IsPlaceNameId ? "PlaceName" : "Aetheryte")} 玩家距={distToShard:F1} 目标距={targetDistToShard:F1}");
                        if (distToShard > 30f)
                        {
                            var source = FindNearestShard(map, playerPos.Value);
                            if (!source.HasValue)
                            {
                                StartMove(target, fly);
                                return;
                            }

                            StartMoveThenTeleport(source.Value.Pos, nearest.Value.Id, nearest.Value.IsPlaceNameId, target, fly);
                            return;
                        }

                        if (TryTeleportToShard(nearest.Value.Id, nearest.Value.IsPlaceNameId, target, fly))
                            return;
                    }
                }
            }

            StartMove(target, fly);
        }
        catch (Exception ex)
        {
            LogHelper.Warning(ex, "vnav 导航失败，请确认已安装 vnavmesh 插件并已加载 navmesh。");
        }
    }

    public void NavigateToRandomInRadius(Vector3 center, float radius, bool fly = false, uint? preferredShardId = null, bool dismountOnArrival = false)
    {
        NavigateTo(PickRandomPointInRadius(center, radius), fly, preferredShardId, dismountOnArrival);
    }

    private Vector3 PickRandomPointInRadius(Vector3 center, float radius)
    {
        var capped = radius > 0f ? radius : 15f;
        var safeRadius = Math.Min(15f, Math.Max(1f, capped));
        for (var i = 0; i < 16; i++)
        {
            var angle = Random.Shared.NextDouble() * Math.Tau;
            var distance = Math.Sqrt(Random.Shared.NextDouble()) * safeRadius;
            var candidate = new Vector3(
                center.X + (float)Math.Cos(angle) * (float)distance,
                center.Y,
                center.Z + (float)Math.Sin(angle) * (float)distance);

            var snapped = SnapToNavmesh(candidate);
            if (IsWithinHorizontalRadius(snapped, center, capped))
                return snapped;
        }

        return center;
    }

    private Vector3 SnapToNavmesh(Vector3 point)
    {
        try
        {
            return nearestPoint.InvokeFunc(point, 5f, 5f) ?? point;
        }
        catch
        {
            return point;
        }
    }

    private static bool IsWithinHorizontalRadius(Vector3 point, Vector3 center, float radius)
    {
        var dx = point.X - center.X;
        var dz = point.Z - center.Z;
        return dx * dx + dz * dz <= radius * radius;
    }

    private void WaitAndNavigate(Vector3 target, bool fly)
    {
        pendingTarget = target;
        pendingFly = fly;
        pendingStartedUtc = DateTime.UtcNow;
        lastPendingCheckUtc = DateTime.MinValue;
        lastPendingDebugUtc = DateTime.MinValue;
        LogHelper.Info($"等待传送完成后导航到 ({target.X:F1}, {target.Y:F1}, {target.Z:F1})");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        _ = framework;
        ProcessPendingMove();
        ProcessPendingTeleport();
        ProcessPendingReturnNavigation();
        ProcessPendingDismount();

        if (!pendingTarget.HasValue)
            return;

        var now = DateTime.UtcNow;
        if (now - lastPendingCheckUtc < TimeSpan.FromMilliseconds(500))
            return;

        lastPendingCheckUtc = now;

        if (now - pendingStartedUtc < TimeSpan.FromSeconds(2))
            return;

        var target = pendingTarget.Value;
        var fly = pendingFly;

        if (now - pendingStartedUtc > TimeSpan.FromSeconds(30))
        {
            ClearPendingNavigation();
            LogHelper.Chat("等待传送或 vnavmesh 就绪超时，改为直接步行导航。");
            StartMove(target, fly);
            return;
        }

        bool busy;
        try
        {
            busy = lsIsBusy?.InvokeFunc() == true;
            if (busy)
            {
                PrintPendingDebug(now, busy, null);
                return;
            }
        }
        catch (Exception ex)
        {
            DebugChat($"导航调试: Lifestream busy 查询失败: {ex.Message}");
            busy = false;
        }

        bool vnavReady;
        try
        {
            vnavReady = isReady.InvokeFunc();
            if (!vnavReady)
            {
                PrintPendingDebug(now, busy, vnavReady);
                return;
            }
        }
        catch (Exception ex)
        {
            DebugChat($"导航调试: vnav ready 查询失败: {ex.Message}");
            return;
        }

        PrintPendingDebug(now, busy, vnavReady);

        ClearPendingNavigation();

        var snapped = nearestPoint.InvokeFunc(target, 5f, 5f);
        var finalTarget = snapped.HasValue ? snapped.Value : target;
        if (pendingDismountTarget.HasValue)
            pendingDismountTarget = finalTarget;
        DebugChat("导航调试: 传送完成，继续步行导航。");
        StartMove(finalTarget, fly);
    }

    private void ClearPendingNavigation() => pendingTarget = null;

    private void StartMoveThenTeleport(Vector3 sourcePos, uint destinationId, bool destinationIsPlaceNameId, Vector3 target, bool fly)
    {
        pendingTeleportTarget = target;
        pendingTeleportFly = fly;
        pendingTeleportSourcePos = sourcePos;
        pendingTeleportDestinationId = destinationId;
        pendingTeleportDestinationIsPlaceNameId = destinationIsPlaceNameId;
        pendingTeleportStartedUtc = DateTime.UtcNow;
        lastTeleportCheckUtc = DateTime.MinValue;
        lastTeleportDebugUtc = DateTime.MinValue;
        DebugChat("导航调试: 先导航到最近传送点，再 Lifestream 传送。");
        StartPathfind(sourcePos, false);
    }

    private void ProcessPendingTeleport()
    {
        if (!pendingTeleportTarget.HasValue)
            return;

        var now = DateTime.UtcNow;
        if (now - lastTeleportCheckUtc < TimeSpan.FromMilliseconds(500))
            return;

        lastTeleportCheckUtc = now;
        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return;

        if (now - pendingTeleportStartedUtc > TimeSpan.FromSeconds(90))
        {
            var target = pendingTeleportTarget.Value;
            var fly = pendingTeleportFly;
            ClearPendingTeleport();
            LogHelper.Chat("前往传送点超时，改为直接导航到目标。");
            StartMove(target, fly);
            return;
        }

        var distToSource = Vector3.Distance(playerPos.Value, pendingTeleportSourcePos);
        var activeAethernetId = GetActiveAethernetId();
        if (distToSource > 30f || activeAethernetId == 0)
        {
            PrintTeleportDebug(now, distToSource, activeAethernetId);
            return;
        }

        try { stop.InvokeAction(); } catch { }
        var finalTarget = pendingTeleportTarget.Value;
        var finalFly = pendingTeleportFly;
        var destinationId = pendingTeleportDestinationId;
        var isPlaceNameId = pendingTeleportDestinationIsPlaceNameId;
        ClearPendingTeleport();

        TryTeleportToShard(destinationId, isPlaceNameId, finalTarget, finalFly);
    }

    private bool TryTeleportToShard(uint id, bool isPlaceNameId, Vector3 target, bool fly)
    {
        DebugChat("导航调试: 传送到最近传送点再导航。");
        try { lsAbort?.InvokeAction(); } catch { }

        bool teleportStarted;
        try
        {
            teleportStarted = isPlaceNameId
                ? aethernetTeleportByPlaceNameId?.InvokeFunc(id) == true
                : aethernetTeleportById?.InvokeFunc(id) == true;
        }
        catch (Exception ex)
        {
            DebugChat($"导航调试: Lifestream 传送调用异常: {ex.Message}");
            StartMove(target, fly);
            return true;
        }

        DebugChat($"导航调试: Lifestream 传送返回={teleportStarted}");
        if (!teleportStarted)
        {
            LogHelper.Chat("传送失败，改为直接步行导航。");
            StartMove(target, fly);
            return true;
        }

        WaitAndNavigate(target, fly);
        return true;
    }

    private void ClearPendingTeleport() => pendingTeleportTarget = null;

    private uint GetActiveAethernetId()
    {
        try
        {
            var id = lsGetActiveAetheryte?.InvokeFunc() ?? 0;
            if (id != 0)
                return id;
        }
        catch { }

        try { return lsGetActiveCustomAetheryte?.InvokeFunc() ?? 0; }
        catch { return 0; }
    }

    private void PrintTeleportDebug(DateTime now, float distToSource, uint activeAethernetId)
    {
        if (now - lastTeleportDebugUtc < TimeSpan.FromSeconds(2))
            return;

        lastTeleportDebugUtc = now;
        DebugChat($"导航调试: 前往当前传送点 距离={distToSource:F1} active={activeAethernetId}");
    }

    private void PrintPendingDebug(DateTime now, bool busy, bool? vnavReady)
    {
        if (now - lastPendingDebugUtc < TimeSpan.FromSeconds(2))
            return;

        lastPendingDebugUtc = now;
        DebugChat($"导航调试: 等待状态 busy={busy} vnav={(vnavReady.HasValue ? vnavReady.Value.ToString() : "未检查")}");
    }

    private void DebugChat(string message)
    {
        if (config.ShowNavigationDebug)
            LogHelper.Chat(message);
    }

    private void StartMove(Vector3 target, bool fly)
    {
        if (QueueMountBeforeMove(target, fly))
            return;

        StartPathfind(target, fly);
    }

    private void StartPathfind(Vector3 target, bool fly)
    {
        LogHelper.Info($"导航到 ({target.X:F1}, {target.Y:F1}, {target.Z:F1}) fly={fly}");
        var ok = pathfindAndMoveTo.InvokeFunc(target, fly);
        if (ok)
            DebugChat("导航调试: 开始步行导航。");
        else
            LogHelper.Chat("vnavmesh 未能开始导航。");
        LogHelper.Info($"导航结果: {ok}");
    }

    private bool QueueMountBeforeMove(Vector3 target, bool fly)
    {
        if (DalamudApi.Condition[ConditionFlag.Mounted]
            || DalamudApi.Condition[ConditionFlag.InCombat]
            || DalamudApi.ObjectTable.LocalPlayer is not { IsDead: false })
            return false;

        var now = DateTime.UtcNow;
        pendingMoveTarget = target;
        pendingMoveFly = fly;
        pendingMoveStartedUtc = now;
        lastMoveCheckUtc = DateTime.MinValue;
        lastMountAttemptUtc = DateTime.MinValue;
        DebugChat("导航调试: 等待上坐骑，坐骑完成后继续导航。");
        return true;
    }

    private unsafe void ProcessPendingMove()
    {
        if (!pendingMoveTarget.HasValue)
            return;

        var now = DateTime.UtcNow;
        if (now - lastMoveCheckUtc < TimeSpan.FromMilliseconds(500))
            return;

        lastMoveCheckUtc = now;
        var target = pendingMoveTarget.Value;
        var fly = pendingMoveFly;

        if (DalamudApi.Condition[ConditionFlag.Mounted])
        {
            ClearPendingMove();
            StartPathfind(target, fly);
            return;
        }

        if (DalamudApi.Condition[ConditionFlag.InCombat]
            || DalamudApi.ObjectTable.LocalPlayer is not { IsDead: false })
        {
            ClearPendingMove();
            StartPathfind(target, fly);
            return;
        }

        if (DalamudApi.Condition[ConditionFlag.BetweenAreas]
            || DalamudApi.Condition[ConditionFlag.BetweenAreas51])
            return;

        if (now - pendingMoveStartedUtc > TimeSpan.FromSeconds(8))
        {
            ClearPendingMove();
            DebugChat("导航调试: 上坐骑超时，直接开始导航。");
            StartPathfind(target, fly);
            return;
        }

        if (now - lastMountAttemptUtc > TimeSpan.FromSeconds(3))
        {
            lastMountAttemptUtc = now;
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
            DebugChat("导航调试: 开始上坐骑，坐骑完成后继续导航。");
        }
    }

    private void ClearPendingMove() => pendingMoveTarget = null;

    private static (Vector3 Pos, uint Id, bool IsPlaceNameId)? FindNearestShard(ExpeditionMap map, Vector3 pos)
    {
        (Vector3 Pos, uint Id, bool IsPlaceNameId)? best = null;
        var bestDist = float.MaxValue;
        foreach (var s in Shards.Where(s => s.Map == map))
        {
            var d = Vector3.Distance(pos, s.Pos);
            if (d < bestDist)
            {
                bestDist = d;
                best = (s.Pos, s.Id, s.IsPlaceNameId);
            }
        }
        return best;
    }

    private static (Vector3 Pos, uint Id, bool IsPlaceNameId)? FindShardById(ExpeditionMap map, uint id)
    {
        foreach (var s in Shards.Where(s => s.Map == map))
        {
            if (s.Id == id)
                return (s.Pos, s.Id, s.IsPlaceNameId);
        }

        return null;
    }

    /// <summary>检测当前所在传送点的 PlaceNameId（需已安装 Lifestream）。</summary>
    public uint? GetCurrentAetheryteId()
    {
        try { return GetActiveAethernetId(); }
        catch { return null; }
    }

    public void Stop()
    {
        ClearPendingNavigation();
        ClearPendingTeleport();
        ClearPendingMove();
        ClearPendingReturnNavigation();
        ClearPendingDismount();
        try { stop.InvokeAction(); }
        catch { }
    }

    private void ConfigureDismountOnArrival(Vector3 target, bool dismountOnArrival)
    {
        if (!dismountOnArrival)
        {
            ClearPendingDismount();
            return;
        }

        pendingDismountTarget = target;
        pendingDismountStartedUtc = DateTime.UtcNow;
        lastDismountAttemptUtc = DateTime.MinValue;
        pendingDismountArrivedLogged = false;
        pendingDismountFired = false;
        LogHelper.Chat($"下坐骑: 已设置目标点 ({target.X:F1}, {target.Y:F1}, {target.Z:F1})");
    }

    private unsafe void ProcessPendingDismount()
    {
        if (!pendingDismountTarget.HasValue)
            return;

        if (!DalamudApi.Condition[ConditionFlag.Mounted])
        {
            if (pendingDismountFired)
                ClearPendingDismount();
            return;
        }

        var now = DateTime.UtcNow;
        if (now - pendingDismountStartedUtc > TimeSpan.FromMinutes(10))
        {
            ClearPendingDismount();
            return;
        }

        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return;

        var dist = HorizontalDistance(playerPos.Value, pendingDismountTarget.Value);
        if (dist > 10f)
            return;

        if (!pendingDismountArrivedLogged)
        {
            pendingDismountArrivedLogged = true;
            LogHelper.Chat($"已到达目标点附近({dist:F1}y)");
        }

        if (now - lastDismountAttemptUtc < TimeSpan.FromMilliseconds(500))
            return;

        lastDismountAttemptUtc = now;
        pendingDismountFired = true;
        try { stop.InvokeAction(); } catch { }
        ActionManager.Instance()->UseAction(ActionType.Mount, 0);
        DebugChat($"导航调试: 到达目标附近({dist:F1}y)，自动下坐骑。");
    }

    private void ClearPendingDismount()
    {
        pendingDismountTarget = null;
        pendingDismountArrivedLogged = false;
        pendingDismountFired = false;
    }

    public unsafe void ReturnToBaseCamp()
    {
        ClearPendingNavigation();
        ClearPendingMove();
        ClearPendingReturnNavigation();
        ClearPendingDismount();
        try { stop.InvokeAction(); } catch { }

        try
        {
            pendingReturnConfirm = true;
            pendingReturnConfirmStartedUtc = DateTime.UtcNow;
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 8);
            DebugChat("导航调试: 使用亚返回回营地。");
        }
        catch (Exception ex)
        {
            LogHelper.Warning(ex, "使用亚返回失败。");
            LogHelper.Chat($"使用亚返回失败: {ex.Message}");
        }
    }

    public void ReturnToBaseCampThenNavigateTo(Vector3 target, ExpeditionMap map)
    {
        ReturnToBaseCamp();
        pendingReturnNavigationTarget = target;
        pendingReturnNavigationMap = map;
        pendingReturnNavigationStartedUtc = DateTime.UtcNow;
        pendingReturnNavigationBaseCampUtc = null;
        pendingReturnNavigationSawBetweenAreas = false;
        pendingReturnNavigationConfirmedUtc = null;
        LogHelper.Chat("回营地后将前往待命点。");
    }

    private void ProcessPendingReturnNavigation()
    {
        if (!pendingReturnNavigationTarget.HasValue)
            return;

        var now = DateTime.UtcNow;
        if (now - pendingReturnNavigationStartedUtc > TimeSpan.FromSeconds(45))
        {
            ClearPendingReturnNavigation();
            LogHelper.Chat("等待回营地超时，取消前往待命点。");
            return;
        }

        if (now - pendingReturnNavigationStartedUtc < TimeSpan.FromSeconds(8))
            return;

        if (DalamudApi.Condition[ConditionFlag.BetweenAreas]
            || DalamudApi.Condition[ConditionFlag.BetweenAreas51])
        {
            pendingReturnNavigationSawBetweenAreas = true;
            return;
        }

        var confirmedLongEnough = pendingReturnNavigationConfirmedUtc.HasValue
                                  && now - pendingReturnNavigationConfirmedUtc.Value >= TimeSpan.FromSeconds(8);
        if (!pendingReturnNavigationSawBetweenAreas && !confirmedLongEnough)
            return;

        var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
        if (currentMap != pendingReturnNavigationMap)
            return;

        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return;

        if (HorizontalDistance(playerPos.Value, GetBaseCampPosition(pendingReturnNavigationMap)) > 80f)
        {
            pendingReturnNavigationBaseCampUtc = null;
            return;
        }

        pendingReturnNavigationBaseCampUtc ??= now;
        if (now - pendingReturnNavigationBaseCampUtc.Value < TimeSpan.FromSeconds(2))
            return;

        var target = pendingReturnNavigationTarget.Value;
        ClearPendingReturnNavigation();
        try { stop.InvokeAction(); } catch { }
        LogHelper.Chat("前往待命点。");
        NavigateTo(target);
    }

    private void ClearPendingReturnNavigation()
    {
        pendingReturnNavigationTarget = null;
        pendingReturnNavigationBaseCampUtc = null;
        pendingReturnNavigationSawBetweenAreas = false;
        pendingReturnNavigationConfirmedUtc = null;
    }

    private static Vector3 GetBaseCampPosition(ExpeditionMap map)
        => map == ExpeditionMap.North
            ? new Vector3(880.00f, 259.74f, 880.06f)
            : new Vector3(830.75f, 72.98f, -695.98f);

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private unsafe void OnSelectYesnoPostSetup(AddonEvent type, AddonArgs args)
    {
        _ = type;
        if (!pendingReturnConfirm || DateTime.UtcNow - pendingReturnConfirmStartedUtc > TimeSpan.FromSeconds(8))
        {
            pendingReturnConfirm = false;
            return;
        }

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible)
            return;

        pendingReturnConfirm = false;
        addon->FireCallbackInt(0);
        if (pendingReturnNavigationTarget.HasValue)
            pendingReturnNavigationConfirmedUtc = DateTime.UtcNow;
        DebugChat("导航调试: 已确认回营地。");
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", OnSelectYesnoPostSetup);
        ClearPendingNavigation();
        ClearPendingTeleport();
        ClearPendingMove();
        ClearPendingReturnNavigation();
    }
}
