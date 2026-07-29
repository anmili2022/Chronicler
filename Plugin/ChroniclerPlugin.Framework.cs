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
            if (currentMap.HasValue && Configuration.LastSelectedMap != currentMap.Value)
            {
                Configuration.LastSelectedMap = currentMap.Value;
                Configuration.Save();
            }

            ProcessStandbyNavigation();

            if (Configuration.AutoDetectAppearances)
            {
                appearanceDetector.Update(currentMap);
                criticalEncounterDetector.Update(currentMap, DalamudApi.ClientState.TerritoryType);
            }

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

    private unsafe void UpdateAutoNavigation(ExpeditionMap? currentMap)
    {
        if (!Configuration.AutoNavigationEnabled || !currentMap.HasValue)
        {
            activeAutoNavigationKey = string.Empty;
            ClearPendingAutoNavigation();
            autoNavigationReturned = false;
            autoReturnDueUtc = null;
            ClearAutoReturnGate();
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastAutoNavigationUpdateUtc < TimeSpan.FromSeconds(1))
            return;

        lastAutoNavigationUpdateUtc = now;

        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
            return;

        if (ProcessAutoReturnGate(currentMap.Value))
            return;

        if (!string.IsNullOrWhiteSpace(activeAutoNavigationKey) && !IsActiveAutoTargetAvailable(currentMap.Value))
        {
            ReturnAfterAutoTargetEnds(currentMap.Value);
            return;
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

        ClearPendingAutoNavigation();
        ReturnAfterAutoTargetEnds(currentMap.Value);
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
            ClearAutoReturnGate();
            LogHelper.Chat("全自动: 等待回营地超时，恢复扫描目标。");
            return false;
        }

        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
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
        LogHelper.Chat("全自动: 已回到营地，恢复扫描目标。");
        return false;
    }

    private void ClearAutoReturnGate()
    {
        pendingAutoReturnMap = null;
        pendingAutoReturnStartedUtc = null;
        pendingAutoReturnBaseCampUtc = null;
    }

    private bool IsActiveAutoTargetAvailable(ExpeditionMap map)
    {
        if (activeAutoNavigationKey.StartsWith("FATE:", StringComparison.Ordinal)
            && ushort.TryParse(activeAutoNavigationKey[5..], out var fateId))
        {
            return DalamudApi.FateTable
                .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
                .Any(fate => fate!.FateId == fateId && fate.State is FateState.Preparing or FateState.Running);
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
        foreach (var boss in BossCatalog.GetFates(map))
        {
            if (!boss.FateId.HasValue || Configuration.DisabledAutoFateIds.Contains(boss.FateId.Value))
                continue;

            var fate = DalamudApi.FateTable
                .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
                .FirstOrDefault(fate => fate!.FateId == boss.FateId && fate.State is FateState.Preparing or FateState.Running);

            if (fate != null)
            {
                var key = $"FATE:{boss.FateId.Value}";
                if (activeAutoNavigationKey != key && ShouldSkipAutoTarget(fate.State == FateState.Running, fate.Progress))
                    continue;

                NavigateAutoTargetOnce(key, $"FATE {boss.Abbreviation}", fate.Position, VnavService.GetPreferredShardIdForFate(fate.FateId));
                return true;
            }
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
            if (activeAutoNavigationKey != key && ShouldSkipAutoTarget(ev.State == DynamicEventState.Battle, ev.Progress))
                continue;

            NavigateAutoTargetOnce(key, $"CE {boss.Abbreviation}", ev.MapMarker.Position, VnavService.GetPreferredShardIdForCriticalEncounter(map, boss.Index), ev.MapMarker.Radius);
            return true;
        }

        return false;
    }

    private bool ShouldSkipAutoTarget(bool isBattle, int progress)
        => isBattle && progress >= Math.Clamp(Configuration.AutoSkipProgressPercent, 0, 100);

    private void NavigateAutoTargetOnce(string key, string label, Vector3 pos, uint? preferredShardId = null, float? randomRadius = null)
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
        activeAutoNavigationKey = key;
        autoNavigationReturned = false;
        autoReturnDueUtc = null;
        LogHelper.Chat($"全自动: 导航到 {label}");
        if (randomRadius.HasValue)
            vnav.NavigateToRandomInRadius(pos, randomRadius.Value, preferredShardId: preferredShardId);
        else
            vnav.NavigateTo(pos, preferredShardId: preferredShardId);
    }

    private void ReturnAfterAutoTargetEnds(ExpeditionMap map)
    {
        if (string.IsNullOrWhiteSpace(activeAutoNavigationKey) || autoNavigationReturned)
            return;

        var now = DateTime.UtcNow;
        var delaySeconds = Math.Max(0, Configuration.AutoReturnDelaySeconds);
        autoReturnDueUtc ??= now + TimeSpan.FromSeconds(delaySeconds);
        if (now < autoReturnDueUtc.Value)
            return;

        autoNavigationReturned = true;
        activeAutoNavigationKey = string.Empty;
        ClearPendingAutoNavigation();
        autoReturnDueUtc = null;
        LogHelper.Chat(delaySeconds > 0 ? $"全自动: 目标已结束，延迟 {delaySeconds} 秒后回营地。" : "全自动: 目标已结束，回营地等待下一次。");
        vnav.ReturnToBaseCamp();
        pendingAutoReturnMap = map;
        pendingAutoReturnStartedUtc = DateTime.UtcNow;
        pendingAutoReturnBaseCampUtc = null;

        if (Configuration.HasAutoReturnStandbyPoint)
        {
            pendingStandbyNavStartedUtc = DateTime.UtcNow;
            pendingStandbyNavUtc = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        }
    }

    private void ProcessStandbyNavigation()
    {
        if (!pendingStandbyNavUtc.HasValue)
            return;

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
}
