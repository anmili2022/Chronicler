using System.Text.RegularExpressions;

namespace Chronicler;

internal static class XydShoutParser
{
    private static readonly Regex TimePattern = new(@"([^\s]+)\s*\[((?:[01]\d|2[0-3]):[0-5]\d|--:--)\]", RegexOptions.Compiled);

    public static ImportResult ApplyToState(string raw, ExpeditionMap activeMap, CrescentStateService state)
    {
        var decoded = XydShareCodeCodec.DecodeFromText(raw);
        if (decoded != null)
        {
            var appliedFromCode = ApplyDecoded(decoded, state);
            return new ImportResult(appliedFromCode, decoded.Map, true);
        }

        var stripped = Regex.Replace(raw.Trim(), @"^/\w+\s+", string.Empty);
        var applied = 0;
        foreach (Match match in TimePattern.Matches(stripped))
        {
            var abbreviation = match.Groups[1].Value;
            var time = match.Groups[2].Value;
            var boss = BossCatalog.FindByAbbreviation(activeMap, abbreviation);
            if (boss == null)
                continue;

            state.SetTimeFromXyd(boss, time);
            applied++;
        }

        return new ImportResult(applied, activeMap, false);
    }

    private static int ApplyDecoded(DecodedShareCode decoded, CrescentStateService state)
    {
        var applied = 0;
        foreach (var record in decoded.Records)
        {
            var boss = BossCatalog.FindByIndex(decoded.Map, record.Index);
            if (boss == null)
                continue;

            state.SetTimeFromXyd(boss, record.Time);
            applied++;
        }

        return applied;
    }
}

internal sealed record ImportResult(int AppliedCount, ExpeditionMap Map, bool FromShareCode);
