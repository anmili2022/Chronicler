namespace Chronicler;

internal sealed record BossEntry(
    ExpeditionMap Map,
    int Id,
    int Index,
    string Abbreviation,
    string Name,
    string Trigger,
    ushort? FateId,
    string[] ObjectNameAliases);
