using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;

namespace Chronicler;

[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public int Version { get; set; } = 1;
    public bool Enabled = true;
    public bool ListenChat = true;
    public bool AutoDetectAppearances = true;
    public bool AutoNavigationEnabled = false;
    public int AutoNavigationStartDelaySeconds = 5;
    public int AutoReturnDelaySeconds = 5;
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
    public bool ShowAutoNavigationStatusMessages = true;
    public bool ShowFloatingStatusWindow = true;
    public bool LockFloatingStatusWindow = false;
    public XivChatType MessageChatType = XivChatType.Echo;
    public ExpeditionMap LastSelectedMap = ExpeditionMap.South;
    public string LastIslandId = string.Empty;
    public List<uint> SouthTerritoryIds = new() { 1252 };
    public List<uint> NorthTerritoryIds = new() { 1346 };
    public List<BossRecordDto> Records = new();
    public List<FateObservationDto> FateObservations = new();
    public List<CeAnnouncementDto> CeAnnouncements = new();
    public List<CriticalEncounterObservationDto> CriticalEncounterObservations = new();

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }
}
