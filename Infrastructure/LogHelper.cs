namespace Chronicler;

internal enum PluginMessageKind
{
    General,
    AutoRecord,
    Navigation,
    AutoNavigation,
    NavigationDebug,
    RouteDebug,
}

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

    public static void Chat(string message, PluginMessageKind kind = PluginMessageKind.General)
    {
        if (!ShouldShow(kind))
            return;

        PrintPluginMessage(FormatMessage(message, kind));
    }

    private static bool ShouldShow(PluginMessageKind kind) => kind switch
    {
        PluginMessageKind.AutoRecord => config?.ShowAutoRecordMessages != false,
        PluginMessageKind.Navigation => config?.ShowNavigationMessages != false,
        PluginMessageKind.AutoNavigation => config?.ShowAutoNavigationStatusMessages != false,
        PluginMessageKind.NavigationDebug => config?.ShowNavigationDebug == true,
        PluginMessageKind.RouteDebug => config?.ShowRouteNavigationDebug == true,
        _ => true,
    };

    private static string FormatMessage(string message, PluginMessageKind kind) => kind switch
    {
        PluginMessageKind.AutoRecord => $"[自动记录] {message}",
        PluginMessageKind.Navigation => $"[导航通知] {message}",
        PluginMessageKind.AutoNavigation => $"[全自动] {message}",
        PluginMessageKind.NavigationDebug => $"[导航调试] {message}",
        PluginMessageKind.RouteDebug => $"[路线调试] {message}",
        _ => message,
    };

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
