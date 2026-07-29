using System.Text.RegularExpressions;

namespace Chronicler;

internal static class XydShareCodeCodec
{
    private const string Base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly Regex ShareCodeRegex = new(@"[NB]0[A-Za-z0-9]{5,}", RegexOptions.Compiled);

    public static string Encode(ExpeditionMap map, IReadOnlyDictionary<int, DateTime?> records)
    {
        var bosses = BossCatalog.GetBosses(map);
        var times = bosses.Select(boss => records.TryGetValue(boss.Id, out var value) ? value : null).ToArray();
        var mapChar = map == ExpeditionMap.South ? 'N' : 'B';

        if (!times.Any(time => time.HasValue))
            return mapChar + "0" + EncodeBase62(0, 2) + EncodeBase62(0, 3);

        var baseMin = times
            .Where(time => time.HasValue)
            .Select(time => time!.Value.Hour * 60 + time.Value.Minute)
            .Min();

        var bitmap = 0;
        var deltas = new List<int>();
        for (var i = 0; i < times.Length; i++)
        {
            if (!times[i].HasValue)
                continue;

            bitmap |= 1 << i;
            var mins = times[i]!.Value.Hour * 60 + times[i]!.Value.Minute;
            var delta = mins - baseMin;
            if (delta < 0)
                delta += 24 * 60;

            deltas.Add(delta);
        }

        return mapChar + "0" + EncodeBase62(baseMin, 2) + EncodeBase62(bitmap, 3) + string.Concat(deltas.Select(delta => EncodeBase62(delta, 2)));
    }

    public static DecodedShareCode? DecodeFromText(string text)
    {
        var match = ShareCodeRegex.Match(text);
        return match.Success ? Decode(match.Value) : null;
    }

    public static DecodedShareCode? Decode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 7)
            return null;

        var map = code[0] switch
        {
            'N' => ExpeditionMap.South,
            'B' => ExpeditionMap.North,
            _ => (ExpeditionMap?)null,
        };

        if (map == null || code[1] != '0')
            return null;

        try
        {
            var baseMin = DecodeBase62(code.Substring(2, 2));
            if (baseMin >= 24 * 60)
                return null;

            var bitmap = DecodeBase62(code.Substring(4, 3));
            if (bitmap >= 1 << 17)
                return null;

            var bitCount = 0;
            var bmp = bitmap;
            while (bmp != 0)
            {
                bitCount++;
                bmp &= bmp - 1;
            }

            var expectedLen = 7 + bitCount * 2;
            if (code.Length < expectedLen)
                return null;

            var records = new List<DecodedBossTime>();
            var pos = 7;
            for (var i = 0; i < 17; i++)
            {
                if ((bitmap & (1 << i)) == 0)
                    continue;

                var delta = DecodeBase62(code.Substring(pos, 2));
                pos += 2;
                var total = (baseMin + delta) % (24 * 60);
                records.Add(new DecodedBossTime(i, $"{total / 60:00}:{total % 60:00}"));
            }

            return new DecodedShareCode(map.Value, records);
        }
        catch
        {
            return null;
        }
    }

    private static string EncodeBase62(int num, int len)
    {
        var chars = new char[len];
        for (var i = len - 1; i >= 0; i--)
        {
            chars[i] = Base62[num % 62];
            num /= 62;
        }

        return new string(chars);
    }

    private static int DecodeBase62(string value)
    {
        var result = 0;
        foreach (var ch in value)
        {
            var digit = Base62.IndexOf(ch);
            if (digit < 0)
                throw new FormatException("Invalid Base62 character.");

            result = result * 62 + digit;
        }

        return result;
    }
}

internal sealed record DecodedShareCode(ExpeditionMap Map, IReadOnlyList<DecodedBossTime> Records);

internal sealed record DecodedBossTime(int Index, string Time);
