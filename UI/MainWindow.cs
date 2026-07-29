using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Chronicler;

internal sealed class MainWindow : Window
{
    private readonly PluginConfiguration config;
    private readonly CrescentStateService state;
    private string importText = string.Empty;
    private string outputText = string.Empty;
    private string statusText = string.Empty;
    private string southTerritoriesText;
    private string northTerritoriesText;
    private const int MaxDebugRows = 50;

    public MainWindow(PluginConfiguration config, CrescentStateService state)
        : base("新月岛史官")
    {
        this.config = config;
        this.state = state;
        NormalizeTerritoryIds();
        southTerritoriesText = FormatTerritoryIds(config.SouthTerritoryIds);
        northTerritoriesText = FormatTerritoryIds(config.NorthTerritoryIds);
        Size = new Vector2(760f, 640f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var territory = DalamudApi.ClientState.TerritoryType;
        var resolvedMap = TerritoryGate.ResolveMap(territory, config);
        if (resolvedMap.HasValue && config.LastSelectedMap != resolvedMap.Value)
        {
            config.LastSelectedMap = resolvedMap.Value;
            config.Save();
        }

        DrawStatus(territory, resolvedMap);
        ImGui.Separator();
        DrawBossTable(config.LastSelectedMap);
        ImGui.Separator();
        DrawImportExport(config.LastSelectedMap);
        ImGui.Separator();
        DrawDebugSections(config.LastSelectedMap);
        DrawTerritorySettings();
    }

    private void DrawStatus(uint territory, ExpeditionMap? resolvedMap)
    {
        ImGui.TextUnformatted($"当前 TerritoryType: {territory}");
        ImGui.TextUnformatted($"识别地图: {(resolvedMap.HasValue ? GetMapName(resolvedMap.Value) : "未识别")}");

        var enabled = config.Enabled;
        if (ImGui.Checkbox("启用插件", ref enabled))
        {
            config.Enabled = enabled;
            config.Save();
        }

        ImGui.SameLine();
        var listenChat = config.ListenChat;
        if (ImGui.Checkbox("监听聊天同步", ref listenChat))
        {
            config.ListenChat = listenChat;
            config.Save();
        }

        ImGui.SameLine();
        var autoDetect = config.AutoDetectAppearances;
        if (ImGui.Checkbox("自动记录出现", ref autoDetect))
        {
            config.AutoDetectAppearances = autoDetect;
            config.Save();
        }

        ImGui.SameLine();
        var showDebugSections = config.ShowDebugSections;
        if (ImGui.Checkbox("显示调试区", ref showDebugSections))
        {
            config.ShowDebugSections = showDebugSections;
            config.Save();
        }

        var showFloating = config.ShowFloatingStatusWindow;
        if (ImGui.Checkbox("显示 FATE/CE 悬浮窗", ref showFloating))
        {
            config.ShowFloatingStatusWindow = showFloating;
            config.Save();
        }

        ImGui.SameLine();
        var lockFloating = config.LockFloatingStatusWindow;
        if (ImGui.Checkbox("锁定悬浮窗", ref lockFloating))
        {
            config.LockFloatingStatusWindow = lockFloating;
            config.Save();
        }

        if (ImGui.Button("南征"))
        {
            config.LastSelectedMap = ExpeditionMap.South;
            config.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("北征"))
        {
            config.LastSelectedMap = ExpeditionMap.North;
            config.Save();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"当前列表: {GetMapName(config.LastSelectedMap)}");

        if (!string.IsNullOrWhiteSpace(statusText))
            ImGui.TextDisabled(statusText);
    }

    private void DrawBossTable(ExpeditionMap map)
    {
        if (!ImGui.BeginTable("##boss_table", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("简称", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("名称");
        ImGui.TableSetupColumn("出现时间", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn("触发/位置");
        ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableHeadersRow();

        foreach (var boss in BossCatalog.GetBosses(map))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(boss.Abbreviation);
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(boss.Name);
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(FormatTime(state.GetAppearedAt(boss)));
            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(boss.Trigger);
            ImGui.TableSetColumnIndex(4);
            if (ImGui.SmallButton($"记录##{boss.Map}_{boss.Id}"))
            {
                state.RecordAppearance(boss, DateTime.Now);
                statusText = $"已记录 {boss.Abbreviation} 出现时间。";
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"清除##{boss.Map}_{boss.Id}"))
            {
                state.Clear(boss);
                statusText = $"已清除 {boss.Abbreviation}。";
            }
        }

        ImGui.EndTable();
    }

    private void DrawImportExport(ExpeditionMap map)
    {
        ImGui.TextUnformatted("导入分享码或喊话");
        ImGui.InputTextMultiline("##import", ref importText, 4096, new Vector2(-1f, 80f));

        if (ImGui.Button("应用导入"))
        {
            var result = XydShoutParser.ApplyToState(importText, map, state);
            if (result.AppliedCount > 0)
            {
                config.LastSelectedMap = result.Map;
                config.Save();
                statusText = $"已导入 {GetMapName(result.Map)} {result.AppliedCount} 条记录。";
            }
            else
            {
                statusText = "未识别到有效分享码或喊话时间。";
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("清空导入框"))
            importText = string.Empty;

        if (ImGui.Button("生成喊话"))
            SetGeneratedOutput(XydShoutGenerator.GenerateNormal(map, state), "喊话");

        ImGui.SameLine();
        if (ImGui.Button("生成出岛喊话"))
            SetGeneratedOutput(XydShoutGenerator.GenerateOutIsland(map, state), "出岛喊话");

        ImGui.SameLine();
        if (ImGui.Button("生成分享码"))
            SetGeneratedOutput(XydShareCodeCodec.Encode(map, state.Snapshot(map)), "分享码");

        ImGui.SameLine();
        if (ImGui.Button("分享码出岛喊话"))
            SetGeneratedOutput(XydShoutGenerator.GenerateShareCodeOutIsland(map, state), "分享码出岛喊话");

        ImGui.InputTextMultiline("##output", ref outputText, 4096, new Vector2(-1f, 80f), ImGuiInputTextFlags.ReadOnly);
    }

    private void SetGeneratedOutput(string text, string label)
    {
        outputText = text;
        ImGui.SetClipboardText(text);
        statusText = $"已生成并复制{label}到剪贴板。";
    }

    private void DrawObservedFates(ExpeditionMap map)
    {
        if (!ImGui.CollapsingHeader("已观测 FATE", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var observations = state.GetFateObservations(map);
        ImGui.TextUnformatted($"{GetMapName(map)} 已观测: {observations.Count} 条");
        ImGui.SameLine();
        if (ImGui.Button("清空当前地图观测"))
        {
            state.ClearFateObservations(map);
            statusText = $"已清空 {GetMapName(map)} FATE 观测记录。";
        }

        if (!ImGui.BeginTable("##observed_fates_table", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX))
            return;

        ImGui.TableSetupColumn("FateId", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 180f);
        ImGui.TableSetupColumn("出现", ImGuiTableColumnFlags.WidthFixed, 65f);
        ImGui.TableSetupColumn("状态", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("时长/剩余", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("等级", ImGuiTableColumnFlags.WidthFixed, 50f);
        ImGui.TableSetupColumn("位置", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("图标/地图", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableHeadersRow();

        foreach (var observation in observations.Take(MaxDebugRows))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(observation.FateId.ToString());
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(observation.Name);
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(observation.AppearedAtLocal.ToString("HH:mm"));
            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(observation.State);
            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted($"{observation.Duration}/{observation.TimeRemaining}");
            ImGui.TableSetColumnIndex(5);
            ImGui.TextUnformatted($"{observation.Level}/{observation.MaxLevel}");
            ImGui.TableSetColumnIndex(6);
            ImGui.TextUnformatted($"{observation.PositionX:F1}, {observation.PositionY:F1}, {observation.PositionZ:F1}");
            ImGui.TableSetColumnIndex(7);
            ImGui.TextUnformatted($"{observation.MapIconId}/{observation.TerritoryType}");
        }

        ImGui.EndTable();
    }

    private void DrawCeAnnouncements(ExpeditionMap map)
    {
        if (!ImGui.CollapsingHeader("CE 公告记录"))
            return;

        var announcements = state.GetCeAnnouncements(map);
        ImGui.TextUnformatted($"{GetMapName(map)} 已记录 CE 公告: {announcements.Count} 条");
        ImGui.SameLine();
        if (ImGui.Button("清空当前地图 CE 公告"))
        {
            state.ClearCeAnnouncements(map);
            statusText = $"已清空 {GetMapName(map)} CE 公告记录。";
        }

        if (!ImGui.BeginTable("##ce_announcements_table", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn("地图", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn("消息");
        ImGui.TableHeadersRow();

        foreach (var announcement in announcements.Take(MaxDebugRows))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(announcement.ObservedAtLocal.ToString("HH:mm:ss"));
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(announcement.TerritoryType.ToString());
            ImGui.TableSetColumnIndex(2);
            ImGui.TextWrapped(announcement.Message);
        }

        ImGui.EndTable();
    }

    private void DrawCriticalEncounters(ExpeditionMap map)
    {
        if (!ImGui.CollapsingHeader("CE 动态事件记录"))
            return;

        var observations = state.GetCriticalEncounterObservations(map);
        ImGui.TextUnformatted($"{GetMapName(map)} 已记录 CE 动态事件: {observations.Count} 条");
        ImGui.SameLine();
        if (ImGui.Button("清空当前地图 CE 动态事件"))
        {
            state.ClearCriticalEncounterObservations(map);
            statusText = $"已清空 {GetMapName(map)} CE 动态事件记录。";
        }

        if (!ImGui.BeginTable("##critical_encounter_table", 10, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX))
            return;

        ImGui.TableSetupColumn("EventId", ImGuiTableColumnFlags.WidthFixed, 65f);
        ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 180f);
        ImGui.TableSetupColumn("出现", ImGuiTableColumnFlags.WidthFixed, 65f);
        ImGui.TableSetupColumn("状态", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("开始戳", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("时长/剩余", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("进度", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("人数", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("位置", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("类型/图标", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableHeadersRow();

        foreach (var observation in observations.Take(MaxDebugRows))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(observation.DynamicEventId.ToString());
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(observation.Name);
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(observation.AppearedAtLocal.ToString("HH:mm"));
            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(observation.State);
            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(observation.StartTimestamp.ToString());
            ImGui.TableSetColumnIndex(5);
            ImGui.TextUnformatted($"{observation.SecondsDuration}/{observation.SecondsLeft}");
            ImGui.TableSetColumnIndex(6);
            ImGui.TextUnformatted($"{observation.Progress}%");
            ImGui.TableSetColumnIndex(7);
            ImGui.TextUnformatted($"{observation.Participants}/{observation.MaxParticipants}");
            ImGui.TableSetColumnIndex(8);
            ImGui.TextUnformatted($"{observation.PositionX:F1}, {observation.PositionY:F1}");
            ImGui.TableSetColumnIndex(9);
            ImGui.TextUnformatted($"{observation.EventType}/{observation.DynamicEventType}/{observation.MapIconId}");
        }

        ImGui.EndTable();
    }

    private void DrawTerritorySettings()
    {
        ImGui.TextUnformatted("地图 ID 设置（逗号分隔，进图后可从顶部读取当前 TerritoryType）");
        if (ImGui.InputText("南征 Territory IDs", ref southTerritoriesText, 256))
            SaveTerritoryIds(ExpeditionMap.South, southTerritoriesText);

        if (ImGui.InputText("北征 Territory IDs", ref northTerritoriesText, 256))
            SaveTerritoryIds(ExpeditionMap.North, northTerritoriesText);
    }

    private void SaveTerritoryIds(ExpeditionMap map, string text)
    {
        var ids = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => uint.TryParse(part, out var id) ? id : 0u)
            .Where(id => id != 0)
            .Distinct()
            .ToList();

        if (map == ExpeditionMap.South)
        {
            config.SouthTerritoryIds = ids;
            southTerritoriesText = FormatTerritoryIds(ids);
        }
        else
        {
            config.NorthTerritoryIds = ids;
            northTerritoriesText = FormatTerritoryIds(ids);
        }

        config.Save();
    }

    private void NormalizeTerritoryIds()
    {
        var normalizedSouth = NormalizeIds(config.SouthTerritoryIds);
        var normalizedNorth = NormalizeIds(config.NorthTerritoryIds);
        if (normalizedSouth.SequenceEqual(config.SouthTerritoryIds) && normalizedNorth.SequenceEqual(config.NorthTerritoryIds))
            return;

        config.SouthTerritoryIds = normalizedSouth;
        config.NorthTerritoryIds = normalizedNorth;
        config.Save();
    }

    private static List<uint> NormalizeIds(IEnumerable<uint> ids)
        => ids.Where(id => id != 0).Distinct().OrderBy(id => id).ToList();

    private static string FormatTerritoryIds(IEnumerable<uint> ids)
        => string.Join(",", NormalizeIds(ids));

    private void DrawFateDebug()
    {
        if (!ImGui.CollapsingHeader("FATE/CE 调试区"))
            return;

        ImGui.TextUnformatted($"当前 FateTable.Length: {DalamudApi.FateTable.Length}");
        ImGui.TextDisabled("用于进图后确认 CE/FATE 的 FateId、名称和状态。把这里的数据反馈回来后，可写入 BossCatalog 做稳定匹配。");

        if (ImGui.Button("输出当前 FATE 到聊天"))
            PrintCurrentFatesToChat();

        if (!ImGui.BeginTable("##fate_debug_table", 10, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollX))
            return;

        ImGui.TableSetupColumn("FateId", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 180f);
        ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("StartEpoch", ImGuiTableColumnFlags.WidthFixed, 95f);
        ImGui.TableSetupColumn("StartLocal", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn("Remain", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthFixed, 50f);
        ImGui.TableSetupColumn("Pos", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("MapIcon/Territory", ImGuiTableColumnFlags.WidthFixed, 130f);
        ImGui.TableHeadersRow();

        foreach (var fate in DalamudApi.FateTable)
        {
            if (fate == null || !DalamudApi.FateTable.IsValid(fate))
                continue;

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(fate.FateId.ToString());
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(fate.Name.TextValue);
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(fate.State.ToString());
            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(fate.StartTimeEpoch.ToString());
            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(FormatEpoch(fate.StartTimeEpoch));
            ImGui.TableSetColumnIndex(5);
            ImGui.TextUnformatted(fate.Duration.ToString());
            ImGui.TableSetColumnIndex(6);
            ImGui.TextUnformatted(fate.TimeRemaining.ToString());
            ImGui.TableSetColumnIndex(7);
            ImGui.TextUnformatted($"{fate.Level}/{fate.MaxLevel}");
            ImGui.TableSetColumnIndex(8);
            ImGui.TextUnformatted($"{fate.Position.X:F1}, {fate.Position.Y:F1}, {fate.Position.Z:F1}");
            ImGui.TableSetColumnIndex(9);
            ImGui.TextUnformatted($"{fate.MapIconId}/{fate.TerritoryType.RowId}");
        }

        ImGui.EndTable();
    }

    private static void PrintCurrentFatesToChat()
    {
        var lines = DalamudApi.FateTable
            .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
            .Select(fate => $"#{fate!.FateId} {fate.Name.TextValue} {fate.State} start={FormatEpoch(fate.StartTimeEpoch)} dur={fate.Duration} remain={fate.TimeRemaining} lv={fate.Level}/{fate.MaxLevel} pos={fate.Position.X:F1},{fate.Position.Y:F1},{fate.Position.Z:F1} icon={fate.MapIconId} terr={fate.TerritoryType.RowId}")
            .ToArray();

        if (lines.Length == 0)
        {
            LogHelper.Chat("当前 FateTable 为空。 ");
            return;
        }

        foreach (var line in lines.Take(12))
            LogHelper.Chat(line);

        if (lines.Length > 12)
            LogHelper.Chat($"还有 {lines.Length - 12} 条 FATE 未输出。 ");
    }

    private static string FormatEpoch(int epoch)
        => epoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime.ToString("HH:mm:ss") : "--";

    private void DrawDebugSections(ExpeditionMap map)
    {
        if (!config.ShowDebugSections)
        {
            ImGui.TextDisabled("调试区已隐藏。需要采集 FateId/CE 动态事件时，勾选顶部“显示调试区”。");
            return;
        }

        DrawObservedFates(map);
        DrawCeAnnouncements(map);
        DrawCriticalEncounters(map);
        DrawFateDebug();
        ImGui.Separator();
    }

    private static string FormatTime(DateTime? time)
        => time.HasValue ? time.Value.ToString("HH:mm") : "--:--";

    private static string GetMapName(ExpeditionMap map)
        => map == ExpeditionMap.South ? "南征" : "北征";
}
