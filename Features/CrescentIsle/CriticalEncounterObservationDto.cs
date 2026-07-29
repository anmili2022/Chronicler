namespace Chronicler;

[Serializable]
public sealed class CriticalEncounterObservationDto
{
    public ExpeditionMap Map { get; set; }
    public uint DynamicEventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime AppearedAtLocal { get; set; }
    public int StartTimestamp { get; set; }
    public uint SecondsLeft { get; set; }
    public uint SecondsDuration { get; set; }
    public byte Progress { get; set; }
    public byte Participants { get; set; }
    public ushort MaxParticipants { get; set; }
    public uint EventType { get; set; }
    public uint DynamicEventType { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public uint MapIconId { get; set; }
    public uint TerritoryType { get; set; }
}
