namespace Chronicler;

[Serializable]
public sealed class BossRecordDto
{
    public ExpeditionMap Map { get; set; }
    public int BossId { get; set; }
    public DateTime? AppearedAtLocal { get; set; }
}
