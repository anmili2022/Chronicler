namespace Chronicler;

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
