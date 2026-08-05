namespace Chronicler;

internal sealed record BossEntry(
    ExpeditionMap Map,
    int Id,
    int Index,
    BossEventKind Kind,
    string Abbreviation,
    string Name,
    string Trigger,
    ushort? FateId,
    uint? DynamicEventId,
    string Drop,
    string[] ObjectNameAliases);

internal enum BossEventKind
{
    CriticalEncounter,
    Fate,
}
