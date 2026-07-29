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
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastAutoNavigationUpdateUtc < TimeSpan.FromSeconds(1))
            return;

        lastAutoNavigationUpdateUtc = now;

        foreach (var boss in BossCatalog.GetFates(currentMap.Value))
        {
            if (!boss.FateId.HasValue || Configuration.DisabledAutoFateIds.Contains(boss.FateId.Value))
                continue;

            var fate = DalamudApi.FateTable
                .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
                .FirstOrDefault(fate => fate!.FateId == boss.FateId && fate.State is FateState.Preparing or FateState.Running);

            if (fate != null)
            {
                NavigateAutoTargetOnce($"FATE:{boss.FateId.Value}", $"FATE {boss.Abbreviation}", fate.Position);
                return;
            }
        }

        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null)
        {
            ReturnAfterAutoTargetEnds();
            return;
        }

        foreach (var ev in content->DynamicEventContainer.Events)
        {
            if (ev.State == DynamicEventState.Inactive)
                continue;

            var boss = BossCatalog.MatchCriticalEncounter(currentMap.Value, ev.DynamicEventId, ev.Name.ToString());
            if (boss == null)
                continue;

            var bossKey = (uint)boss.Index;
            if (Configuration.DisabledAutoCeIds.Contains(bossKey))
                continue;

            NavigateAutoTargetOnce($"CE:{bossKey}", $"CE {boss.Abbreviation}", new Vector3(ev.MapMarker.Position.X, 0, ev.MapMarker.Position.Y));
            return;
        }

        ReturnAfterAutoTargetEnds();
    }

    private void NavigateAutoTargetOnce(string key, string label, Vector3 pos)
    {
        if (activeAutoNavigationKey == key)
            return;

        activeAutoNavigationKey = key;
        autoNavigationReturned = false;
        LogHelper.Chat($"全自动: 导航到 {label}");
        vnav.NavigateTo(pos);
    }

    private void ReturnAfterAutoTargetEnds()
    {
        if (string.IsNullOrWhiteSpace(activeAutoNavigationKey) || autoNavigationReturned)
            return;

        autoNavigationReturned = true;
        activeAutoNavigationKey = string.Empty;
        LogHelper.Chat("全自动: 目标已结束，回营地等待下一次。");
        vnav.ReturnToBaseCamp();
    }
}
