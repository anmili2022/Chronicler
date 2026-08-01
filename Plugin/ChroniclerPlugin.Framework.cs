using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Fates;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using System.Numerics;

namespace Chronicler;

public sealed partial class ChroniclerPlugin
{
    private void OnFrameworkUpdate(IFramework framework)
    {
        _ = framework;
        if (isDisposing || !Configuration.Enabled)
            return;

        try
        {
            var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, Configuration);

            if (currentMap.HasValue && Configuration.AutoIslandLeaveByPlayerCount && Configuration.AutoIslandLeavePlayerThreshold > 0)
                instancePopulationProvider.Update(DateTime.UtcNow, Configuration.AutoIslandLeavePlayerThreshold);

            ProcessStandbyNavigation();

            if (Configuration.AutoDetectAppearances)
            {
                appearanceDetector.Update(currentMap);
                criticalEncounterDetector.Update(currentMap, DalamudApi.ClientState.TerritoryType);
            }

            if (ProcessAutoIslandRotation(currentMap))
                return;

            UpdateAutoNavigation(currentMap);
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if (now - lastFrameworkErrorUtc > TimeSpan.FromSeconds(30))
            {
                lastFrameworkErrorUtc = now;
                LogHelper.Warning(ex, "Framework 更新处理失败。 ");
            }
        }
    }

    private unsafe bool ProcessAutoIslandRotation(ExpeditionMap? currentMap)
    {
        if (!Configuration.AutoIslandRotationEnabled)
        {
            autoIslandCycleActive = false;
            autoIslandReentryStarted = false;
            autoIslandLeaveRequestedUtc = DateTime.MinValue;
            autoIslandLeftUtc = null;
            return false;
        }

        var now = DateTime.UtcNow;
        if (autoIslandCycleActive)
        {
            if (currentMap.HasValue)
            {
                if (autoIslandReentryStarted)
                {
                    autoIslandCycleActive = false;
                    autoIslandReentryStarted = false;
                    autoIslandLeaveRequestedUtc = DateTime.MinValue;
                    autoIslandLeftUtc = null;
                    LogHelper.Chat("自动进出岛: 已重新进入新月岛。");
                    return false;
                }

                return true;
            }

            autoIslandLeftUtc ??= now;
            if (now - autoIslandLeftUtc.Value < TimeSpan.FromSeconds(Math.Max(0, Configuration.AutoIslandReenterDelaySeconds)))
                return true;

            if (!autoIslandReentryStarted)
            {
                Configuration.LastSelectedMap = Configuration.AutoIslandTargetMap;
                Configuration.Save();
                autoIslandReentryStarted = true;
                LogHelper.Chat($"自动进出岛: 已离岛，开始进入{GetMapName(Configuration.AutoIslandTargetMap)}。");
                vnav.GoToCrescentIsle();
            }

            return true;
        }

        if (!currentMap.HasValue)
            return false;

        if (now - autoIslandLeaveRequestedUtc < TimeSpan.FromSeconds(10))
            return true;

        var playerCount = instancePopulationProvider.CurrentPopulation;
        var leaveByPlayers = Configuration.AutoIslandLeaveByPlayerCount
                             && Configuration.AutoIslandLeavePlayerThreshold > 0
                             && instancePopulationProvider.IsConfirmedBelow(Configuration.AutoIslandLeavePlayerThreshold);

        var leaveByTime = false;
        var content = PublicContentOccultCrescent.GetInstance();
        if (Configuration.AutoIslandLeaveByTime
            && content != null
            && Configuration.AutoIslandLeaveTimeThresholdMinutes > 0)
        {
            var timeLeft = content->ContentTimeLeft;
            var thresholdSeconds = Configuration.AutoIslandLeaveTimeThresholdMinutes * 60f;
            leaveByTime = timeLeft > 0f && timeLeft < thresholdSeconds;
        }

        if (!leaveByPlayers && !leaveByTime)
            return false;

        var remainingTimeText = content != null ? FormatMinutesSeconds(content->ContentTimeLeft) : "--:--";
        var reason = leaveByPlayers && leaveByTime
            ? $"人数 {playerCount}<{Configuration.AutoIslandLeavePlayerThreshold} 且 任务剩余 {remainingTimeText}<{Configuration.AutoIslandLeaveTimeThresholdMinutes} 分钟"
            : leaveByPlayers
                ? $"人数 {playerCount}<{Configuration.AutoIslandLeavePlayerThreshold}"
                : $"任务剩余 {remainingTimeText}<{Configuration.AutoIslandLeaveTimeThresholdMinutes} 分钟";
        vnav.Stop();        if (!DalamudApi.Commands.ProcessCommand("/pdr leaveduty"))
        {
            LogHelper.Chat("自动进出岛: 未找到 /pdr leaveduty 命令，请确认 PDR 已安装并加载。");
            autoIslandLeaveRequestedUtc = now;
            return true;
        }

        autoIslandCycleActive = true;
        autoIslandReentryStarted = false;
        autoIslandLeaveRequestedUtc = now;
        autoIslandLeftUtc = null;
        LogHelper.Chat($"自动进出岛: {reason}，已执行 /pdr leaveduty，{Configuration.AutoIslandReenterDelaySeconds} 秒后重新进岛。");
        return true;
    }

    private static string FormatMinutesSeconds(float seconds)
    {
        if (seconds <= 0f)
            return "--:--";

        var totalSeconds = Math.Max(0, (int)MathF.Ceiling(seconds));
        return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
    }

    private unsafe void UpdateAutoNavigation(ExpeditionMap? currentMap)
    {
        if (!Configuration.AutoNavigationEnabled)
        {
            activeAutoNavigationKey = string.Empty;
            ClearPendingAutoNavigation();
            autoNavigationReturned = false;
            autoReturnDueUtc = null;
            ClearAutoReturnGate();
            wasDead = false;
            postReturnIdleUtc = null;
            autoNavWasEnabled = false;
            ClearPendingStandbyNavigation();
            return;
        }

        if (!currentMap.HasValue)
            return;

        if (!autoNavWasEnabled)
        {
            if (!DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]
                && !IsAtCampOrStandby(currentMap.Value))
            {
                autoNavWasEnabled = true;
                LogHelper.Chat("全自动: 开启时不在营地，直接扫描目标（不再强制先回营地）。");
                if (Configuration.HasAutoReturnStandbyPoint)
                {
                    pendingStandbyNavStartedUtc = DateTime.UtcNow;
                    pendingStandbyNavUtc = DateTime.UtcNow + TimeSpan.FromSeconds(8);
                }
                return;
            }

            autoNavWasEnabled = true;
        }

        var now = DateTime.UtcNow;
        if (now - lastAutoNavigationUpdateUtc < TimeSpan.FromSeconds(1))
            return;

        lastAutoNavigationUpdateUtc = now;

        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Unconscious])
        {
            ClearPendingAutoNavigation();
            vnav.Stop();
            wasDead = false;
            return;
        }

        if (wasDead)
        {
            wasDead = false;
        }

        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
            return;

        if (ProcessAutoReturnGate(currentMap.Value))
            return;

        if (!string.IsNullOrWhiteSpace(activeAutoNavigationKey) && !IsActiveAutoTargetAvailable(currentMap.Value))
        {
            ReturnAfterAutoTargetEnds(currentMap.Value);
            return;
        }

        if (string.IsNullOrWhiteSpace(activeAutoNavigationKey))
        {
            if (IsAtCampOrStandby(currentMap.Value) && postReturnIdleUtc.HasValue)
            {
                if (DateTime.UtcNow - postReturnIdleUtc.Value < TimeSpan.FromSeconds(10))
                    return;

                postReturnIdleUtc = null;
            }
        }

        if (Configuration.AutoPrioritizeCe)
        {
            if (TryNavigateAutoCe(currentMap.Value) || TryNavigateAutoFate(currentMap.Value))
                return;
        }
        else
        {
            if (TryNavigateAutoFate(currentMap.Value) || TryNavigateAutoCe(currentMap.Value))
                return;
        }
    }

    private void ClearPendingAutoNavigation()
    {
        pendingAutoNavigationKey = string.Empty;
        pendingAutoNavigationDueUtc = null;
    }

    private bool ProcessAutoReturnGate(ExpeditionMap currentMap)
    {
        if (!pendingAutoReturnMap.HasValue)
            return false;

        if (pendingAutoReturnStartedUtc.HasValue
            && DateTime.UtcNow - pendingAutoReturnStartedUtc.Value > TimeSpan.FromSeconds(45))
        {
            if (pendingAutoReturnRetryCount < 1)
            {
                pendingAutoReturnRetryCount++;
                pendingAutoReturnStartedUtc = DateTime.UtcNow;
                pendingAutoReturnBaseCampUtc = null;
                pendingAutoReturnSawBetweenAreas = false;
                LogHelper.Chat("全自动: 等待回营地超时，重试一次。");
                vnav.ReturnToBaseCamp();
                return true;
            }

            ClearAutoReturnGate();
            LogHelper.Chat("全自动: 等待回营地超时，恢复扫描目标。");
            return false;
        }

        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
        {
            pendingAutoReturnSawBetweenAreas = true;
            return true;
        }

        if (!pendingAutoReturnSawBetweenAreas)
            return true;

        if (currentMap != pendingAutoReturnMap.Value)
            return true;

        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return true;

        var baseCamp = GetBaseCampPosition(pendingAutoReturnMap.Value);
        if (HorizontalDistance(playerPos.Value, baseCamp) > 80f)
        {
            pendingAutoReturnBaseCampUtc = null;
            return true;
        }

        pendingAutoReturnBaseCampUtc ??= DateTime.UtcNow;
        if (DateTime.UtcNow - pendingAutoReturnBaseCampUtc.Value < TimeSpan.FromSeconds(2))
            return true;

        ClearAutoReturnGate();
        postReturnIdleUtc = DateTime.UtcNow;
        LogHelper.Chat("全自动: 已回到营地，10 秒后开始扫描目标。");
        return false;
    }

    private void ClearAutoReturnGate()
    {
        pendingAutoReturnMap = null;
        pendingAutoReturnStartedUtc = null;
        pendingAutoReturnBaseCampUtc = null;
        pendingAutoReturnSawBetweenAreas = false;
        pendingAutoReturnRetryCount = 0;
    }

    private bool IsActiveAutoTargetAvailable(ExpeditionMap map)
    {
        if (activeAutoNavigationKey.StartsWith("FATE:", StringComparison.Ordinal)
            && ushort.TryParse(activeAutoNavigationKey[5..], out var fateId))
        {
            return DalamudApi.FateTable
                .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
                .Any(fate => fate!.FateId == fateId
                             && (fate.State == FateState.Preparing
                                 || (fate.State == FateState.Running && fate.Progress < 100)
                                 || (BossCatalog.IsMagicPotFateId(fate.FateId) && fate.State == FateState.Ending)));
        }

        if (activeAutoNavigationKey.StartsWith("CE:", StringComparison.Ordinal)
            && uint.TryParse(activeAutoNavigationKey[3..], out var ceIndex))
            return IsCeAvailable(map, ceIndex);

        return false;
    }

    private unsafe bool IsCeAvailable(ExpeditionMap map, uint ceIndex)
    {
        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null)
            return false;

        foreach (var ev in content->DynamicEventContainer.Events)
        {
            if (ev.State == DynamicEventState.Inactive)
                continue;

            var boss = BossCatalog.MatchCriticalEncounter(map, ev.DynamicEventId, ev.Name.ToString());
            if (boss != null && (uint)boss.Index == ceIndex)
                return true;
        }

        return false;
    }

    private bool TryNavigateAutoFate(ExpeditionMap map)
    {
        var visibleFates = DalamudApi.FateTable
            .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
            .ToList();

        if (visibleFates.Count > 0)
        {
            var fateInfo = string.Join(", ", visibleFates
                .Where(f => f != null)
                .Select(f => $"{f!.FateId}({(int)f.State}:{f.Progress})"));
            LogHelper.Chat($"导航调试: 活跃FATE: {fateInfo}");
        }

        foreach (var boss in BossCatalog.GetFates(map))
        {
            if (!boss.FateId.HasValue || Configuration.DisabledAutoFateIds.Contains(boss.FateId.Value))
                continue;

            var match = visibleFates.FirstOrDefault(f => f!.FateId == boss.FateId.Value);
            if (match == null)
            {
                LogHelper.Chat($"导航调试: 目录 {boss.Abbreviation}(FateId={boss.FateId}) 无匹配");
                continue;
            }

            if (match.State is not FateState.Preparing and not FateState.Running
                && !(BossCatalog.IsMagicPotFateId(match.FateId) && match.State == FateState.Ending))
            {
                LogHelper.Chat($"导航调试: {boss.Abbreviation}(FateId={boss.FateId}) 状态={match.State} 跳过");
                continue;
            }

            var key = $"FATE:{boss.FateId.Value}";
            if (activeAutoNavigationKey != key && ShouldSkipAutoTarget(match.State == FateState.Running, match.Progress))
            {
                LogHelper.Chat($"导航调试: {boss.Abbreviation} 进度 {match.Progress} ≥ {Configuration.AutoSkipProgressPercent} 跳过");
                continue;
            }

            LogHelper.Chat($"导航调试: 匹配到 {boss.Abbreviation}(FateId={boss.FateId}), 开始导航");
            NavigateAutoTargetOnce(key, $"FATE {boss.Abbreviation}", match.Position, VnavService.GetPreferredShardIdForFate(match.FateId), null, dismountOnArrival: true, boss);
            return true;
        }

        return false;
    }

    private unsafe bool TryNavigateAutoCe(ExpeditionMap map)
    {
        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null)
        {
            return false;
        }

        foreach (var ev in content->DynamicEventContainer.Events)
        {
            if (ev.State == DynamicEventState.Inactive)
                continue;

            var boss = BossCatalog.MatchCriticalEncounter(map, ev.DynamicEventId, ev.Name.ToString());
            if (boss == null)
                continue;

            var bossKey = (uint)boss.Index;
            if (Configuration.DisabledAutoCeIds.Contains(bossKey))
                continue;

            var key = $"CE:{bossKey}";
            if (activeAutoNavigationKey != key && ev.State == DynamicEventState.Battle)
                continue;

            if (activeAutoNavigationKey != key && ShouldSkipAutoTarget(ev.State == DynamicEventState.Battle, ev.Progress))
                continue;

            NavigateAutoTargetOnce(key, $"CE {boss.Abbreviation}", ev.MapMarker.Position, VnavService.GetPreferredShardIdForCriticalEncounter(map, boss.Index), ev.MapMarker.Radius, boss: boss);
            return true;
        }

        return false;
    }

    private bool ShouldSkipAutoTarget(bool isBattle, int progress)
        => isBattle && progress >= Math.Clamp(Configuration.AutoSkipProgressPercent, 0, 100);

    private void NavigateAutoTargetOnce(string key, string label, Vector3 pos, uint? preferredShardId = null, float? randomRadius = null, bool dismountOnArrival = false, BossEntry? boss = null)
    {
        if (activeAutoNavigationKey == key)
            return;

        var startDelaySeconds = Math.Max(0, Configuration.AutoNavigationStartDelaySeconds);
        if (startDelaySeconds > 0)
        {
            var now = DateTime.UtcNow;
            if (pendingAutoNavigationKey != key)
            {
                pendingAutoNavigationKey = key;
                pendingAutoNavigationDueUtc = now + TimeSpan.FromSeconds(startDelaySeconds);
                LogHelper.Chat($"全自动: 发现 {label}，{startDelaySeconds} 秒后导航。");
                return;
            }

            if (pendingAutoNavigationDueUtc.HasValue && now < pendingAutoNavigationDueUtc.Value)
                return;
        }

        ClearPendingAutoNavigation();
        ClearPendingStandbyNavigation();
        activeAutoNavigationKey = key;
        autoNavigationReturned = false;
        autoReturnDueUtc = null;
        LogHelper.Chat($"全自动: 导航到 {label}");

        var useTeleport = vnav.ShouldTeleportToTarget(pos, preferredShardId);
        if (!useTeleport)
        {
            if (randomRadius.HasValue)
                vnav.NavigateToRandomInRadius(pos, randomRadius.Value, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
            else
                vnav.NavigateTo(pos, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
            return;
        }

        var routes = boss == null
            ? Array.Empty<BossRouteDto>()
            : RouteCatalog.GetRoutes(boss.Map, boss.Id, Configuration);
        if (routes.Count > 0)
        {
            vnav.NavigateViaRoute(routes, pos, preferredShardId: preferredShardId, randomRadius: randomRadius, dismountOnArrival: dismountOnArrival);
            return;
        }

        if (randomRadius.HasValue)
            vnav.NavigateToRandomInRadius(pos, randomRadius.Value, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
        else
            vnav.NavigateTo(pos, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
    }

    private void ReturnAfterAutoTargetEnds(ExpeditionMap map)
    {
        if (string.IsNullOrWhiteSpace(activeAutoNavigationKey) || autoNavigationReturned)
            return;

        var now = DateTime.UtcNow;
        var delaySeconds = Math.Max(0, Configuration.AutoReturnDelaySeconds);
        if (!autoReturnDueUtc.HasValue)
        {
            autoReturnDueUtc = now + TimeSpan.FromSeconds(delaySeconds);
            vnav.Stop();
            LogHelper.Chat(delaySeconds > 0 ? $"全自动: 目标已结束，延迟 {delaySeconds} 秒后回营地。" : "全自动: 目标已结束，回营地等待下一次。");
        }

        if (now < autoReturnDueUtc.Value)
            return;

        autoNavigationReturned = true;
        activeAutoNavigationKey = string.Empty;
        ClearPendingAutoNavigation();
        autoReturnDueUtc = null;
        vnav.ReturnToBaseCamp();
        pendingAutoReturnMap = map;
        pendingAutoReturnStartedUtc = DateTime.UtcNow;
        pendingAutoReturnBaseCampUtc = null;
        pendingAutoReturnSawBetweenAreas = false;
        pendingAutoReturnRetryCount = 0;

        if (Configuration.HasAutoReturnStandbyPoint)
        {
            pendingStandbyNavStartedUtc = DateTime.UtcNow;
            pendingStandbyNavUtc = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        }
    }

    private void ProcessStandbyNavigation()
    {
        if (!pendingStandbyNavUtc.HasValue)
            return;

        if (!string.IsNullOrWhiteSpace(activeAutoNavigationKey)
            || !string.IsNullOrWhiteSpace(pendingAutoNavigationKey))
        {
            ClearPendingStandbyNavigation();
            return;
        }

        if (pendingStandbyNavStartedUtc.HasValue
            && DateTime.UtcNow - pendingStandbyNavStartedUtc.Value > TimeSpan.FromSeconds(30))
        {
            ClearPendingStandbyNavigation();
            LogHelper.Chat("全自动: 等待回营地超时，取消前往待命点。");
            return;
        }

        if (DateTime.UtcNow < pendingStandbyNavUtc.Value)
            return;

        if (!Configuration.HasAutoReturnStandbyPoint)
        {
            ClearPendingStandbyNavigation();
            return;
        }

        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
            return;

        var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, Configuration);
        if (currentMap != Configuration.AutoReturnStandbyMap)
            return;

        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return;

        var baseCamp = GetBaseCampPosition(Configuration.AutoReturnStandbyMap);
        if (HorizontalDistance(playerPos.Value, baseCamp) > 80f)
        {
            pendingStandbyBaseCampUtc = null;
            return;
        }

        pendingStandbyBaseCampUtc ??= DateTime.UtcNow;
        if (DateTime.UtcNow - pendingStandbyBaseCampUtc.Value < TimeSpan.FromSeconds(2))
            return;

        var pos = new Vector3(Configuration.AutoReturnStandbyX, Configuration.AutoReturnStandbyY, Configuration.AutoReturnStandbyZ);
        ClearPendingStandbyNavigation();
        LogHelper.Chat("全自动: 前往待命点。");
        vnav.Stop();
        vnav.NavigateTo(pos);
    }

    private void ClearPendingStandbyNavigation()
    {
        pendingStandbyNavUtc = null;
        pendingStandbyNavStartedUtc = null;
        pendingStandbyBaseCampUtc = null;
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

    private bool IsAtCampOrStandby(ExpeditionMap map)
    {
        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!playerPos.HasValue)
            return false;

        var camp = GetBaseCampPosition(map);
        if (HorizontalDistance(playerPos.Value, camp) <= 30f)
            return true;

        if (Configuration.HasAutoReturnStandbyPoint
            && map == Configuration.AutoReturnStandbyMap)
        {
            var standby = new Vector3(Configuration.AutoReturnStandbyX, Configuration.AutoReturnStandbyY, Configuration.AutoReturnStandbyZ);
            if (HorizontalDistance(playerPos.Value, standby) <= 30f)
                return true;
        }

        return false;
    }
}
