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
            autoNavigationReturned = false;
            autoReturnDueUtc = null;
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastAutoNavigationUpdateUtc < TimeSpan.FromSeconds(1))
            return;

        lastAutoNavigationUpdateUtc = now;

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

        ReturnAfterAutoTargetEnds();
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

            NavigateAutoTargetOnce(key, $"CE {boss.Abbreviation}", ev.MapMarker.Position, randomRadius: ev.MapMarker.Radius);
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

        activeAutoNavigationKey = key;
        autoNavigationReturned = false;
        autoReturnDueUtc = null;
        LogHelper.Chat($"全自动: 导航到 {label}");
        if (randomRadius.HasValue)
            vnav.NavigateToRandomInRadius(pos, randomRadius.Value, preferredShardId: preferredShardId);
        else
            vnav.NavigateTo(pos, preferredShardId: preferredShardId);
    }

    private void ReturnAfterAutoTargetEnds()
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
        autoReturnDueUtc = null;
        LogHelper.Chat(delaySeconds > 0 ? $"全自动: 目标已结束，延迟 {delaySeconds} 秒后回营地。" : "全自动: 目标已结束，回营地等待下一次。");
        vnav.ReturnToBaseCamp();
    }
}
