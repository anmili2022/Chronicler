using System.Text.RegularExpressions;

namespace Chronicler;

internal sealed class CurrencyGainTracker
{
    private const string Silver = "十二城邦白银币";
    private const string Gold = "十二城邦白金币";
    private static readonly Regex GainRegex = new(@"获得了([\d,]+)枚(十二城邦白银币|十二城邦白金币)[。.]", RegexOptions.Compiled);
    private readonly List<CurrencyGainRecord> records = new();
    private readonly DateTime startedUtc = DateTime.UtcNow;

    public void ObserveChat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        foreach (Match match in GainRegex.Matches(text))
        {
            if (!int.TryParse(match.Groups[1].Value.Replace(",", string.Empty), out var amount))
                continue;

            records.Add(new CurrencyGainRecord(DateTime.UtcNow, match.Groups[2].Value, amount));
        }
    }

    public void PrintEfficiency()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - startedUtc;
        LogHelper.Chat($"货币效率: 统计时长 {FormatDuration(elapsed)}");
        PrintCurrency(Silver, now, elapsed);
        PrintCurrency(Gold, now, elapsed);
    }

    private void PrintCurrency(string currency, DateTime now, TimeSpan elapsed)
    {
        var total = records.Where(record => record.Currency == currency).Sum(record => record.Amount);
        if (total <= 0)
        {
            LogHelper.Chat($"{currency}: 暂无获得记录。");
            return;
        }

        var fiveMinuteAmount = AmountInWindow(currency, now, TimeSpan.FromMinutes(5));
        var averagePerHour = elapsed.TotalMinutes > 0 ? total / elapsed.TotalMinutes * 60d : 0d;
        var fiveMinutePerHour = fiveMinuteAmount / 5d * 60d;
        LogHelper.Chat($"{currency}: 已获得 {total} 枚。");
        LogHelper.Chat($"{currency}: 最近5分钟 {fiveMinuteAmount} 枚，约 {fiveMinutePerHour:F0} 枚/小时。");
        LogHelper.Chat($"{currency}: 本轮平均约 {averagePerHour:F0} 枚/小时。");
    }

    private int AmountInWindow(string currency, DateTime now, TimeSpan window)
        => records
            .Where(record => record.Currency == currency && now - record.TimeUtc <= window)
            .Sum(record => record.Amount);

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
}

internal readonly record struct CurrencyGainRecord(DateTime TimeUtc, string Currency, int Amount);
