namespace Chronicler;

internal static class LogHelper
{
    private static PluginConfiguration? config;

    public static void Initialize(PluginConfiguration config)
    {
        LogHelper.config = config;
    }

    public static void Info(string message) => DalamudApi.Log.Information(message);

    public static void Warning(string message) => DalamudApi.Log.Warning(message);

    public static void Warning(Exception ex, string message) => DalamudApi.Log.Warning(ex, message);

    public static void Error(Exception ex, string message) => DalamudApi.Log.Error(ex, message);

    public static void Chat(string message)
    {
        PrintPluginMessage(message);
    }

    public static void PrintPluginMessage(string message)
    {
        try
        {
            if (config?.MessageChatType == Dalamud.Game.Text.XivChatType.None)
                return;

            DalamudApi.ChatGui.Print(new Dalamud.Game.Text.XivChatEntry
            {
                Type = config?.MessageChatType ?? Dalamud.Game.Text.XivChatType.Echo,
                Message = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder()
                    .AddUiForeground(Convert.ToChar(Dalamud.Game.Text.SeIconChar.BoxedLetterS).ToString(), 37)
                    .AddUiForeground(Convert.ToChar(Dalamud.Game.Text.SeIconChar.BoxedLetterH).ToString(), 37)
                    .AddUiForeground($" {message}", 24)
                    .Build(),
            });
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, "输出插件聊天消息失败。 ");
        }
    }
}
