using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Chronicler;

internal sealed class VnavService : IDisposable
{
    private static readonly Version MinimumLifestreamVersion = new(2, 5, 4, 15);
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly PluginConfiguration config;
    private readonly ICallGateSubscriber<bool> isReady;
    private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindAndMoveTo;
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> moveTo;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> nearestPoint;
    private readonly ICallGateSubscriber<Vector3?> flagToPoint;
    private readonly ICallGateSubscriber<object> stop;

    // 可选 Lifestream IPC（不强制依赖）
    private ICallGateSubscriber<uint, bool>? aethernetTeleportById;
    private ICallGateSubscriber<uint, bool>? aethernetTeleportByPlaceNameId;
    private ICallGateSubscriber<bool>? lsIsBusy;
    private ICallGateSubscriber<object>? lsAbort;
    private ICallGateSubscriber<uint>? lsGetActiveAetheryte;
    private ICallGateSubscriber<uint>? lsGetActiveCustomAetheryte;
    private ICallGateSubscriber<uint, byte, bool>? lsTeleport;
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
        moveTo = pi.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
        nearestPoint = pi.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        flagToPoint = pi.GetIpcSubscriber<Vector3?>("vnavmesh.Query.Mesh.FlagToPoint");
        stop = pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop");

        TryInitializeLifestream();

        DalamudApi.Framework.Update += OnFrameworkUpdate;
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnSelectYesnoPostSetup);
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", OnSelectYesnoPostSetup);
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectString", OnSelectStringPostSetup);
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectString", OnSelectStringPostSetup);
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnContentsFinderConfirmPostSetup);
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "ContentsFinderConfirm", OnContentsFinderConfirmPostSetup);
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
                lsTeleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
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
    private DateTime? pendingTeleportReturnConfirmedUtc;
    private bool pendingTeleportSawBetweenAreas;
    private DateTime? pendingTeleportBaseCampUtc;
    private bool pendingTeleportWalkingToSource;
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

    private enum GoToCrescentStep { None, WaitingTuliyollal, WaitingOccultVillage, MovingToEntrance, WaitingEntranceMenu, WaitingEnterConfirm, WaitingContentsFinderConfirm }
    private GoToCrescentStep goToCrescentStep;
    private DateTime goToCrescentStepStartedUtc;
    private DateTime goToCrescentMoveStartedUtc;
    private Vector3 goToCrescentLastPosition;
    private int goToCrescentRetryCount;
    private bool goToCrescentStartedInTuliyollal;
    private DateTime goToCrescentLastEntranceInteractionUtc = DateTime.MinValue;

    // 路线导航状态机
    private BossRoutePointDto[]? routePoints;
    private int routePointIndex;
    private DateTime routePointStartedUtc;
    private DateTime lastRouteCheckUtc = DateTime.MinValue;
    private Vector3 routeLastPosition;
    private int routeStuckRetryCount;
    private Vector3 routeFinalTarget;
    private float? routeRandomRadius;
    private uint? routePreferredShardId;
    private bool routeDismountOnArrival;

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

    public static bool RollCriticalEncounterDismount()
        => Random.Shared.Next(2) == 0;

    public unsafe void NavigateTo(Vector3 dest, bool fly = false, uint? preferredShardId = null, bool dismountOnArrival = false)
        => NavigateToInternal(dest, fly, preferredShardId, dismountOnArrival, clearRouteNavigation: true);

    /// <summary>直接使用 vnavmesh 前往目标，不选择水晶、不传送、不回营地。</summary>
    public void NavigateDirectTo(Vector3 dest, bool fly = false)
    {
        ClearPendingNavigation();
        ClearRouteNavigation();
        StartMove(dest, fly);
    }

    private unsafe void NavigateToInternal(Vector3 dest, bool fly, uint? preferredShardId, bool dismountOnArrival, bool clearRouteNavigation, Vector3? teleportSelectionTarget = null)
    {
        ClearPendingNavigation();
        if (clearRouteNavigation)
            ClearRouteNavigation();
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
                var teleportTarget = teleportSelectionTarget ?? target;
                if (playerPos.HasValue && ShouldTeleportToTarget(teleportTarget, preferredShardId))
                {
                    var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
                    if (!currentMap.HasValue)
                    {
                        StartMove(target, fly);
                        return;
                    }

                    var map = currentMap.Value;
                    var nearest = preferredShardId.HasValue ? FindShardById(map, preferredShardId.Value) : FindNearestShard(map, teleportTarget);
                    nearest ??= FindNearestShard(map, teleportTarget);
                    if (nearest.HasValue && nearest.Value.Id != 0)
                    {
                        var playerToTarget = Vector3.Distance(playerPos.Value, teleportTarget);
                        var targetDistToShard = Vector3.Distance(teleportTarget, nearest.Value.Pos);
                        DebugChat($"导航调试: 地图={map} 玩家距目标={playerToTarget:F1} 目标传送点 ID={nearest.Value.Id} 目标最近的水晶距目标={targetDistToShard:F1}");
                        var camp = GetCampShard(map);
                        if (camp.HasValue)
                        {
                            var nearbySource = FindNearestShardWithin(map, playerPos.Value, 60f);
                            var source = nearbySource ?? camp.Value;
                            StartMoveThenTeleport(source.Pos, nearest.Value.Id, nearest.Value.IsPlaceNameId, target, fly, nearbySource.HasValue);
                            return;
                        }
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

    /// <summary>统一目标导航入口：存在有效路线时优先走路线，否则退化单点。</summary>
    public void NavigateToTarget(Vector3 pos, IReadOnlyList<BossRouteDto>? routes, uint? preferredShardId = null, float? randomRadius = null, bool dismountOnArrival = false)
    {
        if (routes != null && routes.Any(route => route.Points.Count >= 2))
        {
            NavigateViaRoute(routes, pos, fly: false, preferredShardId, randomRadius, dismountOnArrival);
            return;
        }

        if (randomRadius.HasValue)
            NavigateToRandomInRadius(pos, randomRadius.Value, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
        else
            NavigateTo(pos, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
    }

    public bool NavigateToFlag()
    {
        try
        {
            var position = flagToPoint.InvokeFunc();
            if (!position.HasValue)
            {
                LogHelper.Chat("当前地图没有可用的 Flag 坐标。");
                return false;
            }

            NavigateTo(position.Value);
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Chat($"读取 Flag 坐标失败: {ex.Message}");
            return false;
        }
    }

    public bool NavigateForcedTo(Vector3 target)
    {
        Stop();
        return StartForcedMove(target);
    }

    /// <summary>按路线导航：随机选一条有效路线逐点前往，走完最后用单点导航收尾。</summary>
    public void NavigateViaRoute(IReadOnlyList<BossRouteDto> routes, Vector3 finalTarget, bool fly = false, uint? preferredShardId = null, float? randomRadius = null, bool dismountOnArrival = false)
    {
        ClearPendingNavigation();
        ClearRouteNavigation();

        var valid = routes.Where(route => route.Points.Count >= 2).ToList();
        if (valid.Count == 0)
        {
            NavigateTo(finalTarget, fly, preferredShardId, dismountOnArrival);
            return;
        }

        var chosen = valid[Random.Shared.Next(valid.Count)];
        routePoints = chosen.Points.ToArray();
        routePointIndex = 0;
        routeStuckRetryCount = 0;
        routeFinalTarget = finalTarget;
        routeRandomRadius = randomRadius;
        routePreferredShardId = preferredShardId;
        routeDismountOnArrival = dismountOnArrival;
        RouteDebugChat($"使用 {chosen.RouteIndex + 1} 号路线，共 {routePoints.Length} 个航点。");
        StartRoutePoint();
    }

    private void StartRoutePoint()
    {
        var point = routePoints![routePointIndex];
        var target = point.ToVector3();
        if (point.Kind == BossRoutePointKind.Forced)
        {
            routePointStartedUtc = DateTime.UtcNow;
            routeLastPosition = DalamudApi.ObjectTable.LocalPlayer?.Position ?? target;
            RouteDebugChat($"强制前往航点 {routePointIndex + 1}/{routePoints.Length} ({target.X:F1}, {target.Y:F1}, {target.Z:F1})");
            StartForcedMove(target);
            return;
        }

        var snapped = SnapToNavmesh(target);
        if (Vector3.Distance(snapped, target) > 8f)
        {
            RouteDebugChat($"航点 {routePointIndex + 1} 附近无网格，跳过。");
            AdvanceRoutePoint();
            return;
        }

        routePointStartedUtc = DateTime.UtcNow;
        routeLastPosition = DalamudApi.ObjectTable.LocalPlayer?.Position ?? target;
        RouteDebugChat($"前往航点 {routePointIndex + 1}/{routePoints.Length} ({snapped.X:F1}, {snapped.Y:F1}, {snapped.Z:F1})");
        Vector3? teleportSelectionTarget = routePointIndex == 0 ? routeFinalTarget : null;
        NavigateToInternal(snapped, false, routePreferredShardId, false, clearRouteNavigation: false, teleportSelectionTarget: teleportSelectionTarget);
    }

    private void AdvanceRoutePoint()
    {
        routePointIndex++;
        if (routePointIndex >= routePoints!.Length)
        {
            RouteDebugChat("全部航点已走完，前往目标。");
            var finalTarget = routeFinalTarget;
            var radius = routeRandomRadius;
            var preferred = routePreferredShardId;
            var dismount = routeDismountOnArrival;
            ClearRouteNavigation();
            if (radius.HasValue)
                NavigateToRandomInRadius(finalTarget, radius.Value, false, preferred, dismount);
            else
                NavigateTo(finalTarget, false, preferred, dismount);
            return;
        }

        StartRoutePoint();
    }

    private void ProcessRouteNavigation()
    {
        if (routePoints == null || routePoints.Length == 0)
            return;

        var now = DateTime.UtcNow;
        if (now - lastRouteCheckUtc < TimeSpan.FromMilliseconds(100))
            return;

        lastRouteCheckUtc = now;
        if (pendingTeleportTarget.HasValue || pendingTarget.HasValue || pendingMoveTarget.HasValue)
            return;

        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return;

        var point = routePoints[routePointIndex];
        var current = point.ToVector3();
        var arrivalDistance = point.Kind == BossRoutePointKind.Forced ? 4f : 8f;
        if (HorizontalDistance(playerPos.Value, current) <= arrivalDistance)
        {
            RouteDebugChat($"到达航点 {routePointIndex + 1}/{routePoints.Length}。");
            AdvanceRoutePoint();
            return;
        }

        if (now - routePointStartedUtc < TimeSpan.FromSeconds(7))
            return;

        if (Vector3.Distance(playerPos.Value, routeLastPosition) >= 2.5f)
        {
            routePointStartedUtc = now;
            routeLastPosition = playerPos.Value;
            return;
        }

        if (routeStuckRetryCount >= 3)
        {
            LogHelper.Chat("路线导航: 多次重试后仍未移动，放弃路线直接前往目标。");
            var finalTarget = routeFinalTarget;
            var radius = routeRandomRadius;
            var preferred = routePreferredShardId;
            var dismount = routeDismountOnArrival;
            ClearRouteNavigation();
            if (radius.HasValue)
                NavigateToRandomInRadius(finalTarget, radius.Value, false, preferred, dismount);
            else
                NavigateTo(finalTarget, false, preferred, dismount);
            return;
        }

        routeStuckRetryCount++;
        RouteDebugChat($"航点未移动，重试 {routeStuckRetryCount}/3。");
        StartRoutePoint();
    }

    private bool StartForcedMove(Vector3 target)
    {
        try { stop.InvokeAction(); } catch { }
        try
        {
            moveTo.InvokeAction([target], false);
            DebugChat("导航调试: 开始强制直线移动。 ");
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Chat($"强制移动失败: {ex.Message}");
            return false;
        }
    }

    private void ClearRouteNavigation()
    {
        routePoints = null;
        routePointIndex = 0;
        routePointStartedUtc = DateTime.MinValue;
        lastRouteCheckUtc = DateTime.MinValue;
        routeLastPosition = default;
        routeStuckRetryCount = 0;
        routeFinalTarget = default;
        routeRandomRadius = null;
        routePreferredShardId = null;
        routeDismountOnArrival = false;
    }

    /// <summary>从任意地图导航到新月岛入口。</summary>
    public void GoToCrescentIsle()
    {
        NormalizeCrescentRouteConfig();

        var territory = DalamudApi.ClientState.TerritoryType;
        if (config.SouthTerritoryIds.Contains(territory) || config.NorthTerritoryIds.Contains(territory))
        {
            LogHelper.Chat("已在新月岛内。");
            return;
        }

        if (territory == config.SolutionNineTerritoryType)
        {
            StartCrescentEntranceMove();
            return;
        }

        TryInitializeLifestream();
        if (lsTeleport == null)
        {
            LogHelper.Chat("无法自动前往新月岛。请先传送到图莱优菈，再使用 /shiguan enter 继续导航。");
            return;
        }

        ClearGoToCrescentIsle();
        LogHelper.Chat($"正在传送到图莱优菈(AetheryteId={config.TuliyollalAetheryteId})…");
        try
        {
            ClearPendingNavigation();
            ClearPendingTeleport();
            ClearPendingMove();
            ClearPendingDismount();
            try { stop.InvokeAction(); } catch { }

            if (!lsTeleport.InvokeFunc(config.TuliyollalAetheryteId, 0))
            {
                LogHelper.Chat("Lifestream 传送失败，请手动传到图莱优菈。");
                return;
            }

            goToCrescentStep = GoToCrescentStep.WaitingTuliyollal;
            goToCrescentStepStartedUtc = DateTime.UtcNow;
            goToCrescentStartedInTuliyollal = territory == config.TuliyollalTerritoryType;
        }
        catch (Exception ex)
        {
            LogHelper.Chat($"Lifestream 传送失败: {ex.Message}");
        }
    }

    private void NormalizeCrescentRouteConfig()
    {
        var changed = false;
        if (config.TuliyollalTerritoryType != 1185)
        {
            config.TuliyollalTerritoryType = 1185;
            changed = true;
        }

        if (config.TuliyollalAetheryteId == 13)
        {
            config.TuliyollalAetheryteId = 216;
            changed = true;
        }

        if (config.SolutionNineTerritoryType == 1187)
        {
            config.SolutionNineTerritoryType = 1278;
            changed = true;
        }

        if (Math.Abs(config.CrescentIsleEntranceX - -77.03f) < 0.01f
            && Math.Abs(config.CrescentIsleEntranceZ - -14.84f) < 0.01f)
        {
            config.CrescentIsleEntranceX = -76.86f;
            config.CrescentIsleEntranceY = 5f;
            config.CrescentIsleEntranceZ = -14.54f;
            changed = true;
        }

        if (changed)
        {
            config.Save();
            LogHelper.Chat("已修正旧版新月岛入口传送配置。");
        }
    }

    private void StartCrescentEntranceMove()
    {
        var entrance = new Vector3(config.CrescentIsleEntranceX, config.CrescentIsleEntranceY, config.CrescentIsleEntranceZ);
        if (!TryGetCrescentEntranceNavmeshPoint(entrance, out var destination))
        {
            LogHelper.Chat("前往新月岛入口失败：vnavmesh 在入口附近找不到可走网格点。");
            ClearGoToCrescentIsle();
            return;
        }

        LogHelper.Chat("正在步行导航到新月岛入口。");
        goToCrescentStep = GoToCrescentStep.MovingToEntrance;
        goToCrescentStepStartedUtc = DateTime.UtcNow;
        goToCrescentRetryCount = 0;
        StartCrescentEntrancePathfind(destination);
    }

    private bool TryGetCrescentEntranceNavmeshPoint(Vector3 entrance, out Vector3 destination)
    {
        destination = default;
        try
        {
            if (!isReady.InvokeFunc())
                return false;

            var snapped = nearestPoint.InvokeFunc(entrance, 120f, 300f)
                          ?? nearestPoint.InvokeFunc(entrance, 180f, 600f)
                          ?? nearestPoint.InvokeFunc(entrance, 260f, 1000f);
            if (!snapped.HasValue)
                return false;

            destination = snapped.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartCrescentEntrancePathfind(Vector3 destination)
    {
        goToCrescentMoveStartedUtc = DateTime.UtcNow;
        goToCrescentLastPosition = DalamudApi.ObjectTable.LocalPlayer?.Position ?? default;
        StartPathfind(destination, false);
    }

    private void StartOccultVillageAethernet()
    {
        ClearGoToCrescentIsle();
        TryInitializeLifestream();
        try
        {
            if (aethernetTeleportById == null || !aethernetTeleportById.InvokeFunc(config.OccultVillageAethernetId))
            {
                LogHelper.Chat("幻境村传送失败。");
                return;
            }

            goToCrescentStep = GoToCrescentStep.WaitingOccultVillage;
            goToCrescentStepStartedUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            LogHelper.Chat($"幻境村传送失败: {ex.Message}");
        }
    }

    private void ProcessGoToCrescentIsle()
    {
        if (goToCrescentStep == GoToCrescentStep.None)
            return;

        var now = DateTime.UtcNow;
        if (now - goToCrescentStepStartedUtc > TimeSpan.FromSeconds(90))
        {
            LogHelper.Chat("前往新月岛超时，已取消。");
            ClearGoToCrescentIsle();
            return;
        }

        if (DalamudApi.Condition[ConditionFlag.BetweenAreas] || DalamudApi.Condition[ConditionFlag.BetweenAreas51])
            return;

        var territory = DalamudApi.ClientState.TerritoryType;

        switch (goToCrescentStep)
        {
            case GoToCrescentStep.WaitingTuliyollal:
            {
                if (territory != config.TuliyollalTerritoryType)
                    return;

                if (DalamudApi.ObjectTable.LocalPlayer == null)
                    return;

                bool busy;
                try { busy = lsIsBusy?.InvokeFunc() == true; }
                catch { busy = false; }

                if (busy)
                    return;

                if (DalamudApi.Condition[ConditionFlag.BetweenAreas] || DalamudApi.Condition[ConditionFlag.BetweenAreas51])
                    return;

                if (now - goToCrescentStepStartedUtc < TimeSpan.FromSeconds(2))
                    return;

                if (goToCrescentStartedInTuliyollal && now - goToCrescentStepStartedUtc < TimeSpan.FromSeconds(8))
                    return;

                uint activeAetheryteId;
                try
                {
                    activeAetheryteId = lsGetActiveAetheryte?.InvokeFunc() ?? 0;
                }
                catch
                {
                    activeAetheryteId = 0;
                }

                if (activeAetheryteId != 216)
                    return;

                if (aethernetTeleportById == null)
                {
                    LogHelper.Chat("Lifestream 都市传送不可用，请手动传送到幻境村。");
                    ClearGoToCrescentIsle();
                    return;
                }

                LogHelper.Chat("正在传送到幻境村…");
                try
                {
                    if (!aethernetTeleportById.InvokeFunc(config.OccultVillageAethernetId))
                    {
                        LogHelper.Chat("幻境村传送失败。");
                        ClearGoToCrescentIsle();
                        return;
                    }

                    goToCrescentStep = GoToCrescentStep.WaitingOccultVillage;
                    goToCrescentStepStartedUtc = now;
                }
                catch (Exception ex)
                {
                    LogHelper.Chat($"幻境村传送失败: {ex.Message}");
                    ClearGoToCrescentIsle();
                }

                break;
            }

            case GoToCrescentStep.WaitingOccultVillage:
            {
                if (territory != config.SolutionNineTerritoryType)
                    return;

                bool busy;
                try { busy = lsIsBusy?.InvokeFunc() == true; }
                catch { busy = false; }

                if (busy)
                    return;

                if (now - goToCrescentStepStartedUtc < TimeSpan.FromSeconds(2))
                    return;

                StartCrescentEntranceMove();
                break;
            }

            case GoToCrescentStep.MovingToEntrance:
                ProcessCrescentEntranceMove();
                break;

            case GoToCrescentStep.WaitingEntranceMenu:
                ProcessEntranceMenu();
                break;

            case GoToCrescentStep.WaitingEnterConfirm:
            case GoToCrescentStep.WaitingContentsFinderConfirm:
                if (now - goToCrescentStepStartedUtc > TimeSpan.FromSeconds(45))
                {
                    ClearGoToCrescentIsle();
                    LogHelper.Chat("等待进岛确认超时，已取消自动进岛。");
                }

                break;
        }
    }

    private void ProcessCrescentEntranceMove()
    {
        var entrance = new Vector3(config.CrescentIsleEntranceX, config.CrescentIsleEntranceY, config.CrescentIsleEntranceZ);
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
            return;

        if (Vector3.Distance(player.Position, entrance) <= 4f)
        {
            try { stop.InvokeAction(); } catch { }
            goToCrescentStep = GoToCrescentStep.WaitingEntranceMenu;
            goToCrescentStepStartedUtc = DateTime.UtcNow;
            goToCrescentLastEntranceInteractionUtc = DateTime.MinValue;
            TryInteractWithEntranceNpc();
            return;
        }

        if (DalamudApi.ClientState.TerritoryType != config.SolutionNineTerritoryType)
        {
            ClearGoToCrescentIsle();
            try { stop.InvokeAction(); } catch { }
            LogHelper.Chat("已离开幻境村，取消前往新月岛入口。");
            return;
        }

        var now = DateTime.UtcNow;
        if (now - goToCrescentStepStartedUtc > TimeSpan.FromSeconds(60))
        {
            ClearGoToCrescentIsle();
            try { stop.InvokeAction(); } catch { }
            LogHelper.Chat("前往新月岛入口超时，已取消导航。");
            return;
        }

        if (now - goToCrescentMoveStartedUtc < TimeSpan.FromSeconds(7))
            return;

        if (Vector3.Distance(player.Position, goToCrescentLastPosition) >= 2.5f)
        {
            goToCrescentMoveStartedUtc = now;
            goToCrescentLastPosition = player.Position;
            return;
        }

        if (goToCrescentRetryCount >= 3)
        {
            ClearGoToCrescentIsle();
            try { stop.InvokeAction(); } catch { }
            LogHelper.Chat("前往新月岛入口失败：多次重试后仍未移动。");
            return;
        }

        if (!TryGetCrescentEntranceNavmeshPoint(entrance, out var destination))
        {
            goToCrescentMoveStartedUtc = now;
            goToCrescentLastPosition = player.Position;
            return;
        }

        goToCrescentRetryCount++;
        LogHelper.Chat($"新月岛入口导航未移动，重试 {goToCrescentRetryCount}/3。");
        StartCrescentEntrancePathfind(destination);
    }

    private void ProcessEntranceMenu()
    {
        var now = DateTime.UtcNow;
        if (now - goToCrescentLastEntranceInteractionUtc < TimeSpan.FromSeconds(2))
            return;

        goToCrescentLastEntranceInteractionUtc = now;
        TryInteractWithEntranceNpc();
    }

    private unsafe bool TryInteractWithEntranceNpc()
    {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
            return false;

        var npc = DalamudApi.ObjectTable.FirstOrDefault(obj =>
            obj is ICharacter
            && obj.Name.TextValue.Equals("杰弗瑞", StringComparison.Ordinal)
            && HorizontalDistance(obj.Position, player.Position) <= 8f);
        if (npc == null)
            return false;

        try
        {
            var targetSystem = TargetSystem.Instance();
            if (targetSystem == null)
                return false;

            targetSystem->InteractWithObject((GameObject*)npc.Address);
            LogHelper.Chat("正在与杰弗瑞交互，等待选择进岛地图。");
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning(ex, "与杰弗瑞交互失败。");
            return false;
        }
    }

    private void ClearGoToCrescentIsle()
    {
        goToCrescentStep = GoToCrescentStep.None;
        goToCrescentMoveStartedUtc = DateTime.MinValue;
        goToCrescentLastPosition = default;
        goToCrescentRetryCount = 0;
        goToCrescentStartedInTuliyollal = false;
        goToCrescentLastEntranceInteractionUtc = DateTime.MinValue;
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
        ProcessRouteNavigation();
        ProcessGoToCrescentIsle();

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

    private unsafe void StartMoveThenTeleport(Vector3 sourcePos, uint destinationId, bool destinationIsPlaceNameId, Vector3 target, bool fly, bool useNearbySource)
    {
        pendingTeleportTarget = target;
        pendingTeleportFly = fly;
        pendingTeleportSourcePos = sourcePos;
        pendingTeleportDestinationId = destinationId;
        pendingTeleportDestinationIsPlaceNameId = destinationIsPlaceNameId;
        pendingTeleportStartedUtc = DateTime.UtcNow;
        pendingTeleportReturnConfirmedUtc = null;
        pendingTeleportSawBetweenAreas = false;
        pendingTeleportBaseCampUtc = null;
        pendingTeleportWalkingToSource = false;
        lastTeleportCheckUtc = DateTime.MinValue;
        lastTeleportDebugUtc = DateTime.MinValue;
        DebugChat(useNearbySource
            ? "导航调试: 60 码内有传送水晶，直接前往水晶后传送。"
            : "导航调试: 附近无传送水晶，先回营地再传送。");

        if (useNearbySource)
        {
            pendingTeleportSawBetweenAreas = true;
            return;
        }

        try
        {
            pendingReturnConfirm = true;
            pendingReturnConfirmStartedUtc = DateTime.UtcNow;
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 8);
            DebugChat("导航调试: 使用亚返回回营地。");
        }
        catch (Exception ex)
        {
            ClearPendingTeleport();
            pendingReturnConfirm = false;
            LogHelper.Warning(ex, "使用亚返回失败。");
            LogHelper.Chat($"使用亚返回失败，改为直接步行导航: {ex.Message}");
            StartMove(target, fly);
        }
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

        if (DalamudApi.Condition[ConditionFlag.BetweenAreas]
            || DalamudApi.Condition[ConditionFlag.BetweenAreas51])
        {
            pendingTeleportSawBetweenAreas = true;
            return;
        }

        var confirmedLongEnough = pendingTeleportReturnConfirmedUtc.HasValue
                                  && now - pendingTeleportReturnConfirmedUtc.Value >= TimeSpan.FromSeconds(8);
        if (!pendingTeleportSawBetweenAreas && !confirmedLongEnough)
        {
            PrintTeleportDebug(now, HorizontalDistance(playerPos.Value, pendingTeleportSourcePos), GetActiveAethernetId());
            return;
        }

        var distToSource = HorizontalDistance(playerPos.Value, pendingTeleportSourcePos);
        var activeAethernetId = GetActiveAethernetId();
        if (distToSource > 4f)
        {
            pendingTeleportBaseCampUtc = null;
            if (!pendingTeleportWalkingToSource)
            {
                pendingTeleportWalkingToSource = true;
                DebugChat("导航调试: 前往传送水晶。");
                StartPathfind(pendingTeleportSourcePos, false);
            }

            PrintTeleportDebug(now, distToSource, activeAethernetId);
            return;
        }

        pendingTeleportWalkingToSource = false;
        pendingTeleportBaseCampUtc ??= now;
        if (now - pendingTeleportBaseCampUtc.Value < TimeSpan.FromSeconds(2))
            return;

        if (activeAethernetId == 0)
            DebugChat("导航调试: 已到传送水晶附近但未检测到 active，尝试 Lifestream 传送。");

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

    private void ClearPendingTeleport()
    {
        pendingTeleportTarget = null;
        pendingTeleportReturnConfirmedUtc = null;
        pendingTeleportSawBetweenAreas = false;
        pendingTeleportBaseCampUtc = null;
        pendingTeleportWalkingToSource = false;
    }

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
            LogHelper.Chat(message.Replace("导航调试: ", string.Empty, StringComparison.Ordinal), PluginMessageKind.NavigationDebug);
    }

    private void RouteDebugChat(string message)
    {
        if (config.ShowRouteNavigationDebug)
            LogHelper.Chat(message, PluginMessageKind.RouteDebug);
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

    private static (Vector3 Pos, uint Id, bool IsPlaceNameId)? FindNearestShardWithin(ExpeditionMap map, Vector3 pos, float maxDistance)
    {
        return Shards
            .Where(shard => shard.Map == map && HorizontalDistance(pos, shard.Pos) <= maxDistance)
            .OrderBy(shard => HorizontalDistance(pos, shard.Pos))
            .Select(shard => ((Vector3 Pos, uint Id, bool IsPlaceNameId)?)(shard.Pos, shard.Id, shard.IsPlaceNameId))
            .FirstOrDefault();
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

    private static (Vector3 Pos, uint Id, bool IsPlaceNameId)? GetCampShard(ExpeditionMap map)
    {
        var camp = Shards.FirstOrDefault(s => s.Map == map);
        return (camp.Pos, camp.Id, camp.IsPlaceNameId);
    }

    /// <summary>判定目标是否值得走传送：当前位置到目标较远，且目标附近水晶到目标明显更近。</summary>
    public bool ShouldTeleportToTarget(Vector3 target, uint? preferredShardId = null)
    {
        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return false;

        var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
        if (!currentMap.HasValue)
            return false;

        var nearest = preferredShardId.HasValue
            ? FindShardById(currentMap.Value, preferredShardId.Value)
            : FindNearestShard(currentMap.Value, target);
        nearest ??= FindNearestShard(currentMap.Value, target);
        if (!nearest.HasValue || nearest.Value.Id == 0)
            return false;

        var playerToTarget = Vector3.Distance(playerPos.Value, target);
        var shardToTarget = Vector3.Distance(target, nearest.Value.Pos);

        // 以玩家附近水晶为锚：附近 60m 内有水晶，且目标最近水晶与其不同，就进入走传送路径；
        // 是否真正走到水晶 4m 内才触发传送，由 ProcessPendingTeleport 的 distToSource 判定负责。
        var anchor = FindNearestShardWithin(currentMap.Value, playerPos.Value, 60f);
        if (anchor.HasValue)
        {
            var shouldTeleport = nearest.Value.Id != anchor.Value.Id;
            DebugChat($"导航调试: 近水晶锚 玩家距目标={playerToTarget:F1} 目标最近水晶距目标={shardToTarget:F1} 走水={shouldTeleport}");
            return shouldTeleport;
        }

        return playerToTarget + config.AutoNavigationTeleportThreshold > shardToTarget;
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
        ClearRouteNavigation();
        ClearGoToCrescentIsle();
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
        LogHelper.Chat($"下坐骑: 已设置目标点 ({target.X:F1}, {target.Y:F1}, {target.Z:F1})", PluginMessageKind.Navigation);
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
            LogHelper.Chat($"已到达目标点附近({dist:F1}y)", PluginMessageKind.Navigation);
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
        DebugChat("导航调试: 收到回营地请求，正在清除现有导航状态。");
        ClearPendingNavigation();
        ClearPendingMove();
        ClearPendingReturnNavigation();
        ClearPendingDismount();
        try { stop.InvokeAction(); } catch { }

        try
        {
            pendingReturnConfirm = true;
            pendingReturnConfirmStartedUtc = DateTime.UtcNow;
            var used = ActionManager.Instance()->UseAction(ActionType.GeneralAction, 8);
            DebugChat($"导航调试: 使用亚返回回营地，UseAction={used}。");
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
        LogHelper.Chat("回营地后将前往待命点。", PluginMessageKind.Navigation);
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
        LogHelper.Chat("前往待命点。", PluginMessageKind.Navigation);
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
        var isReturnConfirm = pendingReturnConfirm && DateTime.UtcNow - pendingReturnConfirmStartedUtc <= TimeSpan.FromSeconds(8);
        var isEnterConfirm = goToCrescentStep == GoToCrescentStep.WaitingEnterConfirm
                             && DateTime.UtcNow - goToCrescentStepStartedUtc <= TimeSpan.FromSeconds(45);
        if (pendingReturnConfirm && !isReturnConfirm)
            pendingReturnConfirm = false;

        if (!isReturnConfirm && !isEnterConfirm)
        {
            return;
        }

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible)
            return;

        if (isReturnConfirm)
            pendingReturnConfirm = false;
        addon->FireCallbackInt(0);
        if (isReturnConfirm && pendingReturnNavigationTarget.HasValue)
            pendingReturnNavigationConfirmedUtc = DateTime.UtcNow;
        if (isReturnConfirm && pendingTeleportTarget.HasValue)
            pendingTeleportReturnConfirmedUtc = DateTime.UtcNow;
        if (isEnterConfirm)
        {
            goToCrescentStep = GoToCrescentStep.WaitingContentsFinderConfirm;
            goToCrescentStepStartedUtc = DateTime.UtcNow;
            LogHelper.Chat("已确认进入新月岛，等待出发确认。");
            return;
        }

        DebugChat("导航调试: 已确认回营地。");
    }

    private unsafe void OnSelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        _ = type;
        if (goToCrescentStep is not GoToCrescentStep.WaitingEntranceMenu and not GoToCrescentStep.WaitingEnterConfirm
            || DateTime.UtcNow - goToCrescentStepStartedUtc > TimeSpan.FromSeconds(45))
            return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible)
            return;

        if (goToCrescentStep == GoToCrescentStep.WaitingEnterConfirm)
        {
            addon->FireCallbackInt(0);
            goToCrescentStep = GoToCrescentStep.WaitingContentsFinderConfirm;
            goToCrescentStepStartedUtc = DateTime.UtcNow;
            LogHelper.Chat("已确认进入新月岛，等待出发确认。");
            return;
        }

        // 杰弗瑞菜单：北征=0，北征两岐塔=1，南征=2。
        var selection = config.LastSelectedMap == ExpeditionMap.North ? 0 : 2;
        addon->FireCallbackInt(selection);
        LogHelper.Chat(config.LastSelectedMap == ExpeditionMap.North
            ? "已选择进入北征。"
            : "已选择进入南征。");
        goToCrescentStep = GoToCrescentStep.WaitingEnterConfirm;
        goToCrescentStepStartedUtc = DateTime.UtcNow;
    }

    private unsafe void OnContentsFinderConfirmPostSetup(AddonEvent type, AddonArgs args)
    {
        _ = type;
        if (goToCrescentStep is not GoToCrescentStep.WaitingEnterConfirm and not GoToCrescentStep.WaitingContentsFinderConfirm
            || DateTime.UtcNow - goToCrescentStepStartedUtc > TimeSpan.FromSeconds(45))
            return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible)
            return;

        addon->FireCallbackInt(8);

        LogHelper.Chat("已点击出发，等待进入新月岛。");
        ClearGoToCrescentIsle();
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", OnSelectYesnoPostSetup);
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "SelectYesno", OnSelectYesnoPostSetup);
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectString", OnSelectStringPostSetup);
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "SelectString", OnSelectStringPostSetup);
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnContentsFinderConfirmPostSetup);
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "ContentsFinderConfirm", OnContentsFinderConfirmPostSetup);
        ClearPendingNavigation();
        ClearPendingTeleport();
        ClearPendingMove();
        ClearPendingReturnNavigation();
        ClearRouteNavigation();
    }
}
