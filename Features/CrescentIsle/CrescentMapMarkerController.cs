using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit.Classes;
using KamiToolKit.MapOverlay;
using XIVTreasure = Lumina.Excel.Sheets.Treasure;
using OverlayMarkerInfo = KamiToolKit.Classes.MapMarkerInfo;

namespace Chronicler;

internal sealed class CrescentMapMarkerController : IDisposable
{
    [Flags]
    internal enum MarkerSet : byte
    {
        None = 0,
        Bronze = 1,
        Silver = 2,
        MagicPot = 4,
        Reroll = 8,
        Carrot = 16,
        Survey = 32,
    }
    private const uint SouthMapId = 967;
    private const uint NorthMapId = 1135;
    private const uint NorthSubterraneMapId = 1244;
    private const uint BronzeIconId = 60356;
    private const uint SilverIconId = 60355;
    private const uint GoldIconId = 60354;
    private const uint CarrotIconId = 25207;

    private readonly PluginConfiguration config;
    private readonly MapOverlayController overlay = new();
    private uint lastTerritory;
    private bool needsRefresh = true;
    private string nearbyResourceKey = string.Empty;
    private bool markerErrorLogged;
    private bool overlayAvailable = true;

    public CrescentMapMarkerController(PluginConfiguration config)
    {
        this.config = config;
        DalamudApi.Framework.Update += OnFrameworkUpdate;
    }

    public void Refresh() => needsRefresh = true;

    public void RefreshProximity() => nearbyResourceKey = string.Empty;

    public MarkerSet GetMarkerSet()
        => (config.ShowMapBronzeChestMarkers ? MarkerSet.Bronze : MarkerSet.None)
           | (config.ShowMapSilverChestMarkers ? MarkerSet.Silver : MarkerSet.None)
           | ((config.ShowMapMagicPotMarkers || config.ShowMapNorthMagicPotMarkers || config.ShowMapSouthMagicPotMarkers) ? MarkerSet.MagicPot : MarkerSet.None)
           | (config.ShowMapRerollMarkers ? MarkerSet.Reroll : MarkerSet.None)
           | (config.ShowMapCarrotMarkers ? MarkerSet.Carrot : MarkerSet.None)
           | (config.ShowMapSurveyMarkers ? MarkerSet.Survey : MarkerSet.None);

    public void SetMarkerSet(MarkerSet set)
    {
        config.ShowMapBronzeChestMarkers = set.HasFlag(MarkerSet.Bronze);
        config.ShowMapSilverChestMarkers = set.HasFlag(MarkerSet.Silver);
        config.ShowMapChestMarkers = config.ShowMapBronzeChestMarkers || config.ShowMapSilverChestMarkers;
        config.ShowMapMagicPotMarkers = set.HasFlag(MarkerSet.MagicPot);
        config.ShowMapNorthMagicPotMarkers = config.ShowMapMagicPotMarkers;
        config.ShowMapSouthMagicPotMarkers = config.ShowMapMagicPotMarkers;
        config.ShowMapRerollMarkers = set.HasFlag(MarkerSet.Reroll);
        config.ShowMapCarrotMarkers = set.HasFlag(MarkerSet.Carrot);
        config.ShowMapSurveyMarkers = set.HasFlag(MarkerSet.Survey);
        config.Save();
        Refresh();
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        overlay.RemoveAllMarkers();
        markerErrorLogged = false;
        overlay.Dispose();
    }

    private unsafe void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework _)
    {
        try
        {
            var territory = DalamudApi.ClientState.TerritoryType;
            if (territory != lastTerritory)
            {
                lastTerritory = territory;
                needsRefresh = true;
            }

            if (!needsRefresh)
            {
                NotifyNearbyResource();
                return;
            }

            if (overlayAvailable)
                overlay.RemoveAllMarkers();
            var agentMap = AgentMap.Instance();
            if (agentMap != null)
            {
                agentMap->ResetMapMarkers();
                agentMap->ResetMiniMapMarkers();
            }
            needsRefresh = false;
            var map = TerritoryGate.ResolveMap(territory, config);
            if (!map.HasValue)
                return;

            EnableOverlayIfAvailable();
            var treasureSheet = DalamudApi.DataManager.GetExcelSheet<XIVTreasure>();
            if (config.ShowMapChestMarkers)
                foreach (var chest in ChestCatalog.Get(map.Value))
                {
                    var iconId = treasureSheet.GetRow((uint)chest.TreasureRowId).SGB.RowId switch
                    {
                        1596 => BronzeIconId,
                        1597 => SilverIconId,
                        _ => 0u,
                    };
                    if (iconId != 0
                        && (iconId == BronzeIconId && config.ShowMapBronzeChestMarkers
                            || iconId == SilverIconId && config.ShowMapSilverChestMarkers))
                        Add(chest.Position, GetMapId(map.Value, chest.TreasureRowId), iconId);
                }

            if (config.ShowMapCarrotMarkers)
                foreach (var carrot in CrescentMapPointCatalog.GetCarrots(map.Value))
                    Add(carrot, GetMapId(map.Value, null), CarrotIconId);

        if (config.ShowMapMagicPotMarkers || config.ShowMapNorthMagicPotMarkers || config.ShowMapSouthMagicPotMarkers)
        {
            if (config.ShowMapNorthMagicPotMarkers || config.ShowMapMagicPotMarkers)
                foreach (var position in CrescentMapPointCatalog.GetPotNorth(map.Value))
                    Add(position, GetMapId(map.Value, null), GoldIconId);
            if (config.ShowMapSouthMagicPotMarkers || config.ShowMapMagicPotMarkers)
                foreach (var position in CrescentMapPointCatalog.GetPotSouth(map.Value))
                    Add(position, GetMapId(map.Value, null), GoldIconId);
            }

            if (config.ShowMapRerollMarkers)
                foreach (var position in CrescentMapPointCatalog.GetRerolls(map.Value))
                    Add(position, GetMapId(map.Value, null), 61473);

            if (config.ShowMapSurveyMarkers)
                foreach (var point in CrescentMapPointCatalog.GetSurveyPoints(map.Value))
                    Add(point.Position, point.MapId, 60468);

            NotifyNearbyResource();
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "更新新月岛地图标记失败。");
        }
    }

    private void EnableOverlayIfAvailable()
    {
        if (!overlayAvailable)
            return;

        try
        {
            overlay.Enable();
        }
        catch (Exception ex)
        {
            overlayAvailable = false;
            LogHelper.Error(ex, "KamiToolKit.MapOverlay 初始化失败；本次会话改用当前地图原生标记。");
        }
    }

    private unsafe void NotifyNearbyResource()
    {
        if (!config.NotifyNearbyMapResources && !config.FlagNearbyMapResources)
            return;

        var player = DalamudApi.ObjectTable.LocalPlayer;
        var map = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
        if (player == null || !map.HasValue)
            return;

        var treasures = DalamudApi.DataManager.GetExcelSheet<XIVTreasure>();
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null || !obj.IsValid() || Vector3.DistanceSquared(player.Position, obj.Position) > 80f * 80f)
                continue;

            var label = obj.ObjectKind == ObjectKind.EventObj && obj.BaseId == 2010139
                ? "胡萝卜"
                : obj.ObjectKind == ObjectKind.Treasure
                    ? ChestCatalog.GetLiveTreasureLabel(map.Value, obj.BaseId) switch
                    {
                        "铜宝箱" when treasures.GetRow(obj.BaseId).SGB.RowId == 1596 => "铜宝箱",
                        "银宝箱" when treasures.GetRow(obj.BaseId).SGB.RowId == 1597 => "银宝箱",
                        _ => string.Empty,
                    }
                    : string.Empty;
            if (string.IsNullOrEmpty(label))
                continue;

            var key = $"{label}:{obj.GameObjectId}";
            if (key == nearbyResourceKey)
                return;

            nearbyResourceKey = key;
            if (config.NotifyNearbyMapResources)
                LogHelper.Chat($"附近发现{label}。", PluginMessageKind.MapNotification);
            if (config.FlagNearbyMapResources)
            {
                var agentMap = AgentMap.Instance();
                if (agentMap != null)
                {
                    agentMap->FlagMarkerCount = 0;
                    agentMap->SetFlagMapMarker(DalamudApi.ClientState.TerritoryType, DalamudApi.ClientState.MapId, obj.Position);
                    LogHelper.Chat($"已标记附近{label}。", PluginMessageKind.MapNotification);
                }
            }
            return;
        }

        nearbyResourceKey = string.Empty;
    }

    private unsafe void Add(Vector3 position, uint mapId, uint iconId)
    {
        var agentMap = AgentMap.Instance();
        if (agentMap != null && agentMap->CurrentMapId == mapId)
        {
            try
            {
                agentMap->AddMapMarker(position, iconId);
                agentMap->AddMiniMapMarker(position, iconId);
            }
            catch (Exception ex)
            {
                if (!markerErrorLogged)
                {
                    markerErrorLogged = true;
                    LogHelper.Error(ex, "添加新月岛原生地图标记失败；已继续尝试覆盖层标记。");
                }
            }
        }

        if (!overlayAvailable)
            return;

        try
        {
            overlay.AddMarker(new OverlayMarkerInfo
            {
                AllowAnyMap = false,
                MapId = mapId,
                Position = new Vector2(position.X, position.Z),
                IconId = iconId,
            });
        }
        catch (Exception ex)
        {
            // One bad overlay node must not break the framework update loop.
            if (!markerErrorLogged)
            {
                markerErrorLogged = true;
                LogHelper.Error(ex, "添加新月岛地图标记失败；已跳过异常点位。");
            }
        }
    }

    private static uint GetMapId(ExpeditionMap map, int? chestId)
        => map == ExpeditionMap.South ? SouthMapId : chestId is 2013 or >= 2066 and <= 2069 or 2072 ? NorthSubterraneMapId : NorthMapId;
}
