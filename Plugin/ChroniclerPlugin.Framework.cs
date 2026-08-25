using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Fates;
using FFXIVClientStructs.FFXIV.Client.Game;
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

            if (currentMap.HasValue)
            {
                var lowPopulationThreshold = Math.Max(1, Configuration.AutoIslandLeavePlayerThreshold);
                instancePopulationProvider.Update(DateTime.UtcNow, lowPopulationThreshold);
            }
            else
            {
                instancePopulationProvider.Reset();
            }

            ProcessStandbyNavigation();
            ProcessAchievementRespawnNavigation(currentMap);
            ProcessActiveAchievementRespawnNavigation();
            ProcessAchievementTargetSelection();

            if (Configuration.AutoDetectAppearances)
            {
                appearanceDetector.Update(currentMap);
                criticalEncounterDetector.Update(currentMap, DalamudApi.ClientState.TerritoryType);
            }

            if (ProcessAutoIslandRotation(currentMap))
                return;

            UpdateAutoNavigation(currentMap);
            achievementProgress.Update(force: false);
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
                    LogHelper.Chat("自动进出岛: 已重新进入新月岛。", PluginMessageKind.AutoNavigation);
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
                LogHelper.Chat($"自动进出岛: 已离岛，开始进入{GetMapName(Configuration.AutoIslandTargetMap)}。", PluginMessageKind.AutoNavigation);
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

        if (!IsAtBaseCamp(currentMap.Value))
            return false;

        var remainingTimeText = content != null ? FormatMinutesSeconds(content->ContentTimeLeft) : "--:--";
        var reason = leaveByPlayers && leaveByTime
            ? $"人数 {playerCount}<{Configuration.AutoIslandLeavePlayerThreshold} 且 任务剩余 {remainingTimeText}<{Configuration.AutoIslandLeaveTimeThresholdMinutes} 分钟"
            : leaveByPlayers
                ? $"人数 {playerCount}<{Configuration.AutoIslandLeavePlayerThreshold}"
                : $"任务剩余 {remainingTimeText}<{Configuration.AutoIslandLeaveTimeThresholdMinutes} 分钟";
        vnav.Stop();
        if (!DalamudApi.Commands.ProcessCommand("/pdr leaveduty"))
        {
            LogHelper.Chat("自动进出岛: 未找到 /pdr leaveduty 命令，请确认 PDR 已安装并加载。", PluginMessageKind.AutoNavigation);
            autoIslandLeaveRequestedUtc = now;
            return true;
        }

        autoIslandCycleActive = true;
        autoIslandReentryStarted = false;
        autoIslandLeaveRequestedUtc = now;
        autoIslandLeftUtc = null;
        LogHelper.Chat($"自动进出岛: {reason}，已执行 /pdr leaveduty，{Configuration.AutoIslandReenterDelaySeconds} 秒后重新进岛。", PluginMessageKind.AutoNavigation);
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
            postReturnScanDueUtc = null;
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
                LogHelper.Chat("开启时不在营地，直接扫描目标（不再强制先回营地）。", PluginMessageKind.AutoNavigation);
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
            if (IsAtCampOrStandby(currentMap.Value) && postReturnScanDueUtc.HasValue)
            {
                if (DateTime.UtcNow < postReturnScanDueUtc.Value)
                    return;

                postReturnScanDueUtc = null;
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
                LogHelper.Chat("等待回营地超时，重试一次。", PluginMessageKind.AutoNavigation);
                vnav.ReturnToBaseCamp();
                return true;
            }

            ClearAutoReturnGate();
            LogHelper.Chat("等待回营地超时，恢复扫描目标。", PluginMessageKind.AutoNavigation);
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
        var scanDelaySeconds = RandomizeAutoDelay(Configuration.AutoReturnScanDelaySeconds);
        postReturnScanDueUtc = DateTime.UtcNow + TimeSpan.FromSeconds(scanDelaySeconds);
        LogHelper.Chat(scanDelaySeconds > 0 ? $"已回到营地，{scanDelaySeconds} 秒后开始扫描目标。" : "已回到营地，开始扫描目标。", PluginMessageKind.AutoNavigation);
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
            && uint.TryParse(activeAutoNavigationKey[3..], out var dynamicEventId))
            return IsCeAvailable(dynamicEventId);

        return false;
    }

    private static unsafe bool IsCeAvailable(uint dynamicEventId)
    {
        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null)
            return false;

        foreach (var ev in content->DynamicEventContainer.Events)
        {
            if (ev.State == DynamicEventState.Inactive)
                continue;

            if (ev.DynamicEventId == dynamicEventId)
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
            LogHelper.Chat($"活跃FATE: {fateInfo}", PluginMessageKind.NavigationDebug);
        }

        foreach (var match in visibleFates)
        {
            var boss = BossCatalog.GetFates(map).FirstOrDefault(candidate =>
                candidate.FateId == match!.FateId
                || candidate.ObjectNameAliases.Any(alias => match.Name.TextValue.StartsWith(alias, StringComparison.Ordinal))
                || candidate.Name.Equals(match.Name.TextValue, StringComparison.Ordinal));
            if (boss?.FateId is not { } fateId || Configuration.DisabledAutoFateIds.Contains(fateId))
                continue;

            if (match.State is not FateState.Preparing and not FateState.Running
                && !(BossCatalog.IsMagicPotFateId(match.FateId) && match.State == FateState.Ending))
            {
                LogHelper.Chat($"{boss.Abbreviation}(FateId={fateId}) 状态={match.State} 跳过", PluginMessageKind.NavigationDebug);
                continue;
            }

            // Catalog IDs may be provisional; use the live row ID so the target remains active after navigation starts.
            var key = $"FATE:{match.FateId}";
            if (activeAutoNavigationKey != key && ShouldSkipAutoTarget(match.State == FateState.Running, match.Progress))
            {
                LogHelper.Chat($"{boss.Abbreviation} 进度 {match.Progress} ≥ {Configuration.AutoSkipProgressPercent} 跳过", PluginMessageKind.NavigationDebug);
                continue;
            }

            LogHelper.Chat($"匹配到 {boss.Abbreviation}(FateId={fateId}), 开始导航", PluginMessageKind.NavigationDebug);
            NavigateAutoTargetOnce(key, $"FATE {boss.Abbreviation}", match.Position, VnavService.GetPreferredShardIdForFate(match.FateId), Configuration.FateNavigationRandomOffset > 0 ? Configuration.FateNavigationRandomOffset : null, dismountOnArrival: true, boss);
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

            // DynamicEvent IDs may differ from catalog indexes; use the live ID for the target lifecycle.
            var key = $"CE:{ev.DynamicEventId}";
            if (activeAutoNavigationKey != key && ev.State == DynamicEventState.Battle)
                continue;

            if (activeAutoNavigationKey != key && ShouldSkipAutoTarget(ev.State == DynamicEventState.Battle, ev.Progress))
                continue;

            NavigateAutoTargetOnce(key, $"CE {boss.Abbreviation}", ev.MapMarker.Position, VnavService.GetPreferredShardIdForCriticalEncounter(map, boss.Index), Configuration.CeNavigationRandomOffset > 0 ? Configuration.CeNavigationRandomOffset : null, dismountOnArrival: VnavService.RollCriticalEncounterDismount(), boss: boss);
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

        if (pendingAutoNavigationKey != key)
        {
            var startDelaySeconds = RandomizeAutoDelay(Configuration.AutoNavigationStartDelaySeconds);
            if (startDelaySeconds > 0)
            {
                pendingAutoNavigationKey = key;
                pendingAutoNavigationDueUtc = DateTime.UtcNow + TimeSpan.FromSeconds(startDelaySeconds);
                LogHelper.Chat($"发现 {label}，{startDelaySeconds} 秒后导航。", PluginMessageKind.AutoNavigation);
                return;
            }
        }

        if (pendingAutoNavigationDueUtc.HasValue && DateTime.UtcNow < pendingAutoNavigationDueUtc.Value)
            return;

        if (!string.IsNullOrWhiteSpace(activeAutoNavigationKey))
        {
            LogHelper.Chat($"切换自动目标: {activeAutoNavigationKey} -> {key}，停止当前导航。", PluginMessageKind.AutoNavigation);
            vnav.Stop();
        }

        ClearPendingAutoNavigation();
        ClearPendingStandbyNavigation();
        activeAutoNavigationKey = key;
        autoNavigationReturned = false;
        autoReturnDueUtc = null;
        LogHelper.Chat($"导航到 {label}", PluginMessageKind.AutoNavigation);

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
        if (!autoReturnDueUtc.HasValue)
        {
            var delaySeconds = RandomizeAutoDelay(Configuration.AutoReturnDelaySeconds);
            autoReturnDueUtc = now + TimeSpan.FromSeconds(delaySeconds);
            vnav.Stop();
            LogHelper.Chat(delaySeconds > 0 ? $"目标已结束，延迟 {delaySeconds} 秒后回营地。" : "目标已结束，回营地等待下一次。", PluginMessageKind.AutoNavigation);
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

    private static int RandomizeAutoDelay(int configuredSeconds)
    {
        var delay = Math.Max(0, configuredSeconds);
        return delay == 0 ? 0 : Math.Max(0, delay + Random.Shared.Next(-1, 2));
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
            LogHelper.Chat("等待回营地超时，取消前往待命点。", PluginMessageKind.AutoNavigation);
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
        LogHelper.Chat("前往待命点。", PluginMessageKind.AutoNavigation);
        vnav.Stop();
        vnav.NavigateTo(pos);
    }

    private void ClearPendingStandbyNavigation()
    {
        pendingStandbyNavUtc = null;
        pendingStandbyNavStartedUtc = null;
        pendingStandbyBaseCampUtc = null;
    }

    private void ProcessAchievementRespawnNavigation(ExpeditionMap? currentMap)
    {
        if (!Configuration.AchievementRespawnNavigationEnabled)
        {
            ClearPendingAchievementRespawnNavigation();
            ClearActiveAchievementRespawnNavigation();
            ClearPendingAchievementTargetSelection();
            return;
        }

        var isDead = DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Unconscious];
        if (isDead)
        {
            achievementWasDead = true;
            return;
        }

        if (achievementWasDead)
        {
            achievementWasDead = false;

            if (Configuration.AutoNavigationEnabled)
                return;

            if (Configuration.HasAchievementDeathPoint
                && currentMap == Configuration.AchievementDeathMap)
            {
                QueueAchievementRespawnNavigation(
                    new Vector3(Configuration.AchievementDeathX, Configuration.AchievementDeathY, Configuration.AchievementDeathZ),
                    "送死坐标");
            }
        }

        if (!pendingAchievementRespawnTarget.HasValue)
            return;

        if (pendingAchievementRespawnStartedUtc.HasValue
            && DateTime.UtcNow - pendingAchievementRespawnStartedUtc.Value > TimeSpan.FromSeconds(30))
        {
            LogHelper.Chat($"复活后前往{pendingAchievementRespawnLabel}超时，请确认 vnavmesh 已加载。", PluginMessageKind.Navigation);
            ClearPendingAchievementRespawnNavigation();
            return;
        }

        if (pendingAchievementRespawnStartedUtc.HasValue
            && DateTime.UtcNow - pendingAchievementRespawnStartedUtc.Value < TimeSpan.FromSeconds(2))
            return;

        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]
            || DalamudApi.ObjectTable.LocalPlayer is not { IsDead: false }
            || !vnav.IsReady)
            return;

        if (DateTime.UtcNow - lastAchievementRespawnNavigationAttemptUtc < TimeSpan.FromSeconds(2))
            return;

        var target = pendingAchievementRespawnTarget.Value;
        var label = pendingAchievementRespawnLabel;
        lastAchievementRespawnNavigationAttemptUtc = DateTime.UtcNow;
        if (!vnav.NavigateRespawnDirect(target))
            return;

        ClearPendingAchievementRespawnNavigation();
        activeAchievementRespawnTarget = target;
        activeAchievementRespawnLastPosition = DalamudApi.ObjectTable.LocalPlayer?.Position ?? target;
        activeAchievementRespawnStartedUtc = DateTime.UtcNow;
        activeAchievementRespawnRetryCount = 0;
        LogHelper.Chat($"复活成功，前往{label}。", PluginMessageKind.Navigation);

        ProcessAchievementTargetSelection();
    }

    private void QueueAchievementRespawnNavigation(Vector3 target, string label)
    {
        pendingAchievementRespawnTarget = target;
        pendingAchievementRespawnLabel = label;
        pendingAchievementRespawnStartedUtc = DateTime.UtcNow;
        lastAchievementRespawnNavigationAttemptUtc = DateTime.MinValue;
    }

    private void ClearPendingAchievementRespawnNavigation()
    {
        pendingAchievementRespawnTarget = null;
        pendingAchievementRespawnLabel = string.Empty;
        pendingAchievementRespawnStartedUtc = null;
        lastAchievementRespawnNavigationAttemptUtc = DateTime.MinValue;
    }

    private void ProcessActiveAchievementRespawnNavigation()
    {
        if (!activeAchievementRespawnTarget.HasValue || !activeAchievementRespawnStartedUtc.HasValue)
            return;

        if (!Configuration.AchievementRespawnNavigationEnabled || Configuration.AutoNavigationEnabled)
        {
            ClearActiveAchievementRespawnNavigation();
            return;
        }

        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player is not { IsDead: false })
            return;

        var target = activeAchievementRespawnTarget.Value;
        if (HorizontalDistance(player.Position, target) <= 4f)
        {
            if (Configuration.AchievementTargetBaseId != 0)
            {
                pendingAchievementSelectionPosition = target;
                pendingAchievementSelectionStartedUtc = DateTime.UtcNow;
            }
            ClearActiveAchievementRespawnNavigation();
            return;
        }

        if (DateTime.UtcNow - activeAchievementRespawnStartedUtc.Value < TimeSpan.FromSeconds(7))
            return;

        if (HorizontalDistance(player.Position, activeAchievementRespawnLastPosition) >= 2.5f)
        {
            activeAchievementRespawnLastPosition = player.Position;
            activeAchievementRespawnStartedUtc = DateTime.UtcNow;
            return;
        }

        if (activeAchievementRespawnRetryCount >= 3)
        {
            LogHelper.Chat("复活后步行导航多次未移动，已停止。", PluginMessageKind.Navigation);
            ClearActiveAchievementRespawnNavigation();
            return;
        }

        activeAchievementRespawnRetryCount++;
        activeAchievementRespawnLastPosition = player.Position;
        activeAchievementRespawnStartedUtc = DateTime.UtcNow;
        LogHelper.Chat($"复活后步行导航未移动，重试 {activeAchievementRespawnRetryCount}/3。", PluginMessageKind.Navigation);
        vnav.NavigateRespawnDirect(target);
    }

    private void ClearActiveAchievementRespawnNavigation()
    {
        activeAchievementRespawnTarget = null;
        activeAchievementRespawnStartedUtc = null;
        activeAchievementRespawnRetryCount = 0;
    }

    private void ProcessAchievementTargetSelection()
    {
        if (!pendingAchievementSelectionPosition.HasValue)
            return;

        if (pendingAchievementSelectionStartedUtc.HasValue
            && DateTime.UtcNow - pendingAchievementSelectionStartedUtc.Value > TimeSpan.FromSeconds(45))
        {
            LogHelper.Chat($"到达坐标后未找到 BaseId={Configuration.AchievementTargetBaseId} 的可选中对象。", PluginMessageKind.Navigation);
            ClearPendingAchievementTargetSelection();
            return;
        }

        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null || HorizontalDistance(player.Position, pendingAchievementSelectionPosition.Value) > 8f)
            return;

        var target = DalamudApi.ObjectTable
            .Where(obj => obj != null
                && obj.IsValid()
                && obj.IsTargetable
                && obj.BaseId == Configuration.AchievementTargetBaseId)
            .OrderBy(obj => HorizontalDistance(player.Position, obj.Position))
            .FirstOrDefault();
        if (target == null)
            return;

        DalamudApi.TargetManager.Target = target;
        LogHelper.Chat($"已选中 BaseId={Configuration.AchievementTargetBaseId}：{target.Name.TextValue}。", PluginMessageKind.Navigation);
        TryUseAchievementRangedAction(player.ClassJob.RowId, target);
        ClearPendingAchievementTargetSelection();
    }

    private static unsafe void TryUseAchievementRangedAction(uint jobId, Dalamud.Game.ClientState.Objects.Types.IGameObject target)
    {
        var (actionId, actionName) = jobId switch
        {
            35 => (16526u, "冲击"),      // 赤魔法师
            25 => (141u, "火炎"),        // 黑魔法师
            27 => (3579u, "毁荡"),       // 召唤师
            42 => (34650u, "火炎之红"),  // 绘灵法师
            24 => (119u, "飞石"),        // 白魔法师
            40 => (24283u, "注药"),      // 贤者
            28 => (25865u, "极炎法"),    // 学者
            33 => (3596u, "凶星"),       // 占星术士
            31 => (7412u, "热分裂弹"),   // 机工士
            23 => (97u, "强力射击"),     // bard
            38 => (15989u, "瀑泻"),       // 舞者
            _ => (0u, string.Empty),
        };
        if (actionId == 0)
        {
            LogHelper.Chat("当前职业未配置成就远程技能，已仅选中目标。", PluginMessageKind.Navigation);
            return;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
        {
            LogHelper.Chat($"无法释放{actionName}：ActionManager 不可用。", PluginMessageKind.Navigation);
            return;
        }

        var used = actionManager->UseAction(ActionType.Action, actionId, target.GameObjectId);
        LogHelper.Chat(used
            ? $"已对 {target.Name.TextValue} 释放{actionName}。"
            : $"无法对 {target.Name.TextValue} 释放{actionName}，请确认目标在射程内且技能可用。", PluginMessageKind.Navigation);
    }

    private void ClearPendingAchievementTargetSelection()
    {
        pendingAchievementSelectionPosition = null;
        pendingAchievementSelectionStartedUtc = null;
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

        if (IsAtBaseCamp(map))
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

    private static bool IsAtBaseCamp(ExpeditionMap map)
    {
        var playerPos = DalamudApi.ObjectTable.LocalPlayer?.Position;
        return playerPos.HasValue
               && HorizontalDistance(playerPos.Value, GetBaseCampPosition(map)) <= 30f;
    }
}
