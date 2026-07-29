namespace Chronicler;

[Serializable]
public sealed class FateObservationDto
{
    public ExpeditionMap Map { get; set; }
    public ushort FateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime AppearedAtLocal { get; set; }
    public string State { get; set; } = string.Empty;
    public short Duration { get; set; }
    public long TimeRemaining { get; set; }
    public byte Level { get; set; }
    public byte MaxLevel { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public uint MapIconId { get; set; }
    public uint TerritoryType { get; set; }
}
