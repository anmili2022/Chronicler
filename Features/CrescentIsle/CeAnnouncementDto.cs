namespace Chronicler;

[Serializable]
public sealed class CeAnnouncementDto
{
    public ExpeditionMap Map { get; set; }
    public DateTime ObservedAtLocal { get; set; }
    public string Message { get; set; } = string.Empty;
    public uint TerritoryType { get; set; }
}
