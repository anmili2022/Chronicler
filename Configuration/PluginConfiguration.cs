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
    public bool ShowDebugSections = false;
    public bool ShowFloatingStatusWindow = true;
    public bool LockFloatingStatusWindow = false;
    public XivChatType MessageChatType = XivChatType.Echo;
    public ExpeditionMap LastSelectedMap = ExpeditionMap.South;
    public List<uint> SouthTerritoryIds = new();
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
