using Dalamud.Game.Command;

namespace Chronicler;

public sealed partial class ChroniclerPlugin
{
    private void RegisterCommands()
    {
        DalamudApi.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "新月岛史官：/shiguan 打开界面；/shiguan shout 生成喊话；/shiguan code 生成分享码；/shiguan import <文本> 导入。",
        });
        DalamudApi.Commands.AddHandler("/史官", new CommandInfo(OnCommand)
        {
            HelpMessage = "新月岛史官：/史官 打开界面",
        });
    }

    private static void UnregisterCommands()
    {
        DalamudApi.Commands.RemoveHandler(CommandName);
        DalamudApi.Commands.RemoveHandler("/史官");
    }

    private void OnCommand(string command, string args)
    {
        _ = command;
        if (isDisposing)
            return;

        var trimmed = (args ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            ui.OpenMainWindow();
            return;
        }

        if (trimmed.Equals("shout", StringComparison.OrdinalIgnoreCase))
        {
            LogHelper.Chat(XydShoutGenerator.GenerateNormal(Configuration.LastSelectedMap, stateService));
            return;
        }

        if (trimmed.Equals("code", StringComparison.OrdinalIgnoreCase))
        {
            LogHelper.Chat(XydShareCodeCodec.Encode(Configuration.LastSelectedMap, stateService.Snapshot(Configuration.LastSelectedMap)));
            return;
        }

        if (trimmed.StartsWith("import ", StringComparison.OrdinalIgnoreCase))
        {
            var result = XydShoutParser.ApplyToState(trimmed["import ".Length..], Configuration.LastSelectedMap, stateService);
            if (result.AppliedCount > 0)
            {
                Configuration.LastSelectedMap = result.Map;
                Configuration.Save();
            }

            LogHelper.Chat($"已导入 {result.AppliedCount} 条记录。 ");
            return;
        }

        ui.OpenMainWindow();
    }
}
