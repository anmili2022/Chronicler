using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;

namespace Chronicler;

[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public int Version { get; set; } = 5;
    public bool Enabled = true;
    public bool ListenChat = true;
    public bool AutoDetectAppearances = true;
    [NonSerialized]
    public bool AutoNavigationEnabled = false;
    [NonSerialized]
    public bool AutoIslandRotationEnabled = false;
    public bool AutoIslandLeaveByPlayerCount = true;
    public bool AutoIslandLeaveByTime = true;
    public int AutoIslandLeavePlayerThreshold = 30;
    public int AutoIslandLeaveTimeThresholdMinutes = 160;
    public int AutoIslandLeaveTimeThresholdSeconds = 160;
    public int AutoIslandReenterDelaySeconds = 20;
    public ExpeditionMap AutoIslandTargetMap = ExpeditionMap.North;
    public int AutoNavigationStartDelaySeconds = 5;
    public int AutoReturnDelaySeconds = 5;
    public int AutoNavigationTeleportThreshold = 100;
    public int AutoSkipProgressPercent = 80;
    public bool AutoPrioritizeCe = true;
    public float AutoReturnStandbyX = 0;
    public float AutoReturnStandbyY = 0;
    public float AutoReturnStandbyZ = 0;
    public ExpeditionMap AutoReturnStandbyMap = ExpeditionMap.South;
    public bool HasAutoReturnStandbyPoint = false;
    public string AutoNavigationTargetType = string.Empty;
    public uint AutoNavigationTargetId = 0;
    public string AutoNavigationTargetName = string.Empty;
    public List<uint> DisabledAutoFateIds = new();
    public List<uint> DisabledAutoCeIds = new();
    public bool ShowDebugSections = false;
    public bool ShowNavigationDebug = false;
    public bool ShowRouteNavigationDebug = false;
    public bool ShowAutoNavigationStatusMessages = true;
    public bool ShowAutoRecordMessages = true;
    public bool ShowNavigationMessages = true;
    public bool ShowFloatingStatusWindow = true;
    public bool LockFloatingStatusWindow = false;
    public bool ShowFloatingTreasureCounts = false;
    public bool ShowFloatingCarrotCount = false;
    public bool SortChestCatalogByDistance = false;
    public bool SortChestCatalogByBocchiRoute = false;
    public XivChatType MessageChatType = XivChatType.Echo;
    public ExpeditionMap LastSelectedMap = ExpeditionMap.South;
    public string LastIslandId = string.Empty;
    public uint TuliyollalTerritoryType = 1185;
    public uint SolutionNineTerritoryType = 1278;
    public List<uint> SouthTerritoryIds = new() { 1252 };
    public List<uint> NorthTerritoryIds = new() { 1346 };
    public float CrescentIsleEntranceX = -76.86f;
    public float CrescentIsleEntranceY = 5f;
    public float CrescentIsleEntranceZ = -14.54f;
    public uint TuliyollalAetheryteId = 216;
    public uint OccultVillageAethernetId = 239;
    public List<BossRecordDto> Records = new();
    public List<FateObservationDto> FateObservations = new();
    public List<CeAnnouncementDto> CeAnnouncements = new();
    public List<CriticalEncounterObservationDto> CriticalEncounterObservations = new();
    public List<BossRouteDto> BossRoutes = new();

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        AutoNavigationEnabled = false;
        AutoIslandRotationEnabled = false;

        if (Version < 2)
        {
            AutoIslandLeavePlayerThreshold = 30;
            AutoIslandLeaveTimeThresholdMinutes = 160;
            AutoIslandReenterDelaySeconds = 20;
            AutoIslandTargetMap = ExpeditionMap.North;
            AutoIslandLeaveByPlayerCount = true;
            AutoIslandLeaveByTime = true;
            Version = 2;
            Save();
        }

        if (Version < 3)
        {
            AutoIslandLeaveTimeThresholdMinutes = 160;
            AutoIslandLeaveByPlayerCount = true;
            AutoIslandLeaveByTime = true;
            Version = 3;
            Save();
        }

        if (Version < 4)
        {
            ShowAutoRecordMessages = true;
            ShowNavigationMessages = true;
            Version = 4;
            Save();
        }

        if (Version < 5)
        {
            ShowFloatingTreasureCounts = false;
            ShowFloatingCarrotCount = false;
            SortChestCatalogByDistance = false;
            SortChestCatalogByBocchiRoute = false;
            Version = 5;
            Save();
        }
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }
}
