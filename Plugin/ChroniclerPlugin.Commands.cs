using Dalamud.Game.Command;

namespace Chronicler;

public sealed partial class ChroniclerPlugin
{
    private void RegisterCommands()
    {
        DalamudApi.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "新月岛史官：/shiguan 打开界面；/shiguan enter 导航到新月岛；/shiguan record <简称> 记录当前时间；/shiguan set <简称> <HH:mm> 修改出现时间；/shiguan clear <简称> 清除；/shiguan shout 生成喊话；/shiguan code 生成分享码；/shiguan import <文本> 导入。",
        });
        DalamudApi.Commands.AddHandler("/史官", new CommandInfo(OnCommand)
        {
            HelpMessage = "新月岛史官：/史官 打开界面；/史官 记录 <简称>；/史官 设置 <简称> <HH:mm>；/史官 清除 <简称>",
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

        if (trimmed.StartsWith("record ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("记录 ", StringComparison.OrdinalIgnoreCase))
        {
            var abbreviation = trimmed[trimmed.IndexOf(' ')..].Trim();
            var boss = BossCatalog.FindByAbbreviation(Configuration.LastSelectedMap, abbreviation);
            if (boss == null)
            {
                LogHelper.Chat($"未找到简称「{abbreviation}」。");
                return;
            }

            stateService.RecordAppearance(boss, DateTime.Now);
            LogHelper.Chat($"已记录 {boss.Abbreviation} 出现时间 {DateTime.Now:HH:mm}。");
            return;
        }

        if (trimmed.StartsWith("set ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("设置 ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = trimmed[(trimmed.IndexOf(' ') + 1)..].Trim();
            var spaceIndex = rest.LastIndexOf(' ');
            if (spaceIndex <= 0 || spaceIndex == rest.Length - 1)
            {
                LogHelper.Chat("用法：/shiguan set <简称> <HH:mm>");
                return;
            }

            var abbreviation = rest[..spaceIndex].Trim();
            var time = rest[(spaceIndex + 1)..].Trim();
            var boss = BossCatalog.FindByAbbreviation(Configuration.LastSelectedMap, abbreviation);
            if (boss == null)
            {
                LogHelper.Chat($"未找到简称「{abbreviation}」。");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(time, @"^([01]\d|2[0-3]):[0-5]\d$"))
            {
                LogHelper.Chat($"时间格式不正确：{time}，请使用 HH:mm。");
                return;
            }

            stateService.SetTimeFromXyd(boss, time);
            LogHelper.Chat($"已设置 {boss.Abbreviation} 出现时间 {time}。");
            return;
        }

        if (trimmed.StartsWith("clear ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("清除 ", StringComparison.OrdinalIgnoreCase))
        {
            var abbreviation = trimmed[trimmed.IndexOf(' ')..].Trim();
            var boss = BossCatalog.FindByAbbreviation(Configuration.LastSelectedMap, abbreviation);
            if (boss == null)
            {
                LogHelper.Chat($"未找到简称「{abbreviation}」。");
                return;
            }

            stateService.Clear(boss);
            LogHelper.Chat($"已清除 {boss.Abbreviation}。");
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

        if (trimmed.Equals("enter", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("上岛", StringComparison.OrdinalIgnoreCase))
        {
            vnav.GoToCrescentIsle();
            return;
        }

        ui.OpenMainWindow();
    }
}
