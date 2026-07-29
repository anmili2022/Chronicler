using Dalamud.Plugin.Services;

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
}
