using System.Text.RegularExpressions;

namespace Chronicler;

public sealed partial class ChroniclerPlugin
{
    private static readonly Regex ShareCodeLikeRegex = new(@"[NB]0[A-Za-z0-9]{5,}", RegexOptions.Compiled);
    private static readonly Regex IslandIdRegex = new(@"岛\s*ID[:：]\s*(\d+)", RegexOptions.Compiled);

    private void RegisterChatHandlers()
    {
        DalamudApi.ChatGui.ChatMessage += OnHandleableChatMessage;
    }

    private void UnregisterChatHandlers()
    {
        DalamudApi.ChatGui.ChatMessage -= OnHandleableChatMessage;
    }

    private void OnHandleableChatMessage(object message)
    {
        if (isDisposing || !Configuration.ListenChat)
            return;

        var text = ExtractChatMessageText(message);
        ObserveIslandId(text);
        ObserveCeAnnouncement(text);

        if (!LooksLikeXydText(text))
            return;

        var result = XydShoutParser.ApplyToState(text, Configuration.LastSelectedMap, stateService);
        if (result.AppliedCount <= 0)
            return;

        Configuration.LastSelectedMap = result.Map;
        Configuration.Save();
        LogHelper.Chat($"已从聊天同步 {GetMapName(result.Map)} {result.AppliedCount} 条记录。 ");
    }

    private static bool LooksLikeXydText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (ShareCodeLikeRegex.IsMatch(text))
            return true;

        return BossCatalog.South.Concat(BossCatalog.North).Any(boss => text.Contains($"{boss.Abbreviation} [", StringComparison.Ordinal));
    }

    private static string GetMapName(ExpeditionMap map)
        => map == ExpeditionMap.South ? "南征" : "北征";

    private void ObserveCeAnnouncement(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains("紧急遭遇战", StringComparison.Ordinal))
            return;

        var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, Configuration) ?? Configuration.LastSelectedMap;
        stateService.RecordCeAnnouncement(currentMap, DalamudApi.ClientState.TerritoryType, text.Trim(), DateTime.Now);
    }

    private void ObserveIslandId(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var match = IslandIdRegex.Match(text);
        if (!match.Success)
            return;

        var islandId = match.Groups[1].Value;
        if (Configuration.LastIslandId == islandId)
            return;

        Configuration.LastIslandId = islandId;
        Configuration.Save();
    }

    private static string ExtractChatMessageText(object message)
    {
        try
        {
            var messageProperty = message.GetType().GetProperty("Message");
            var value = messageProperty?.GetValue(message);
            var textValueProperty = value?.GetType().GetProperty("TextValue");
            return textValueProperty?.GetValue(value) as string
                   ?? value?.ToString()
                   ?? message.ToString()
                   ?? string.Empty;
        }
        catch
        {
            return message.ToString() ?? string.Empty;
        }
    }
}
