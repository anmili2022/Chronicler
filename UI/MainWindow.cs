using System.Diagnostics;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace Chronicler;

internal sealed class MainWindow : Window
{
    private readonly PluginConfiguration config;
    private readonly CrescentStateService state;
    private readonly VnavService vnav;
    private string sharedText = string.Empty;
    private string statusText = string.Empty;
    private string southTerritoriesText;
    private string northTerritoriesText;
    private readonly List<string> distanceDebugLines = new();
    private const int MaxDebugRows = 50;

    public MainWindow(PluginConfiguration config, CrescentStateService state, VnavService vnav)
        : base($"新月岛史官 v{GetVersionText()}")
    {
        this.config = config;
        this.state = state;
        this.vnav = vnav;
        NormalizeTerritoryIds();
        southTerritoriesText = FormatTerritoryIds(config.SouthTerritoryIds);
        northTerritoriesText = FormatTerritoryIds(config.NorthTerritoryIds);
        Size = new Vector2(760f, 640f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    private static string GetVersionText()
        => typeof(ChroniclerPlugin).Assembly.GetName().Version?.ToString() ?? "unknown";

    public override void Draw()
    {
        var territory = DalamudApi.ClientState.TerritoryType;
        var resolvedMap = TerritoryGate.ResolveMap(territory, config);

        DrawTopBar(territory, resolvedMap);
        ImGui.Separator();

        if (!ImGui.BeginTabBar("##chronicler_tabs"))
            return;

        if (ImGui.BeginTabItem("新月岛史官"))
        {
            DrawMapSelector();
            ImGui.Separator();
            DrawBossTable(config.LastSelectedMap);
            ImGui.Separator();
            DrawImportExport(config.LastSelectedMap);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("自动寻路"))
        {
            DrawAutoNavigation(resolvedMap);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("设置"))
        {
            DrawSettings();
            ImGui.Separator();
            DrawTerritorySettings();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("DEBUG"))
        {
            DrawDebugSettings();
            ImGui.Separator();
            DrawDebugSections(config.LastSelectedMap);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawTopBar(uint territory, ExpeditionMap? resolvedMap)
    {
        DrawDependencyStatus();
        ImGui.TextUnformatted($"当前 TerritoryType: {territory}");
        ImGui.SameLine();
        ImGui.TextUnformatted($"当前岛 ID: {GetCurrentIslandId()}");
        ImGui.SameLine();
        ImGui.TextUnformatted($"识别地图: {(resolvedMap.HasValue ? GetMapName(resolvedMap.Value) : "未识别")}");

        if (ImGui.Button("前往新月岛入口"))
            vnav.GoToCrescentIsle();
        ImGui.SameLine();
        if (ImGui.Button("新月岛：北征之章 信息整理"))
            OpenUrl("https://bbs.nga.cn/read.php?tid=47269383");

        if (!string.IsNullOrWhiteSpace(statusText))
            ImGui.TextDisabled(statusText);
    }

    private void DrawSettings()
    {
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
        var showAutoNavigationStatusMessages = config.ShowAutoNavigationStatusMessages;
        if (ImGui.Checkbox("全自动提示", ref showAutoNavigationStatusMessages))
        {
            config.ShowAutoNavigationStatusMessages = showAutoNavigationStatusMessages;
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
    }

    private void DrawDebugSettings()
    {
        var showDebugSections = config.ShowDebugSections;
        if (ImGui.Checkbox("显示调试区", ref showDebugSections))
        {
            config.ShowDebugSections = showDebugSections;
            config.Save();
        }

        ImGui.SameLine();
        var showNavigationDebug = config.ShowNavigationDebug;
        if (ImGui.Checkbox("导航调试", ref showNavigationDebug))
        {
            config.ShowNavigationDebug = showNavigationDebug;
            config.Save();
        }

        if (ImGui.Button("显示当前位置/CE/FATE距离"))
            UpdateDistanceDebugLines();

        ImGui.SameLine();
        if (ImGui.Button("复制全部调试信息"))
            CopyAllDebugInfo(config.LastSelectedMap);

        foreach (var line in distanceDebugLines)
            ImGui.TextDisabled(line);
    }

    private unsafe void UpdateDistanceDebugLines()
    {
        distanceDebugLines.Clear();

        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
        {
            distanceDebugLines.Add("未找到当前玩家对象。");
            return;
        }

        var playerPos = player.Position;
        distanceDebugLines.Add($"当前位置: {FormatPosition(playerPos)}");

        var fates = DalamudApi.FateTable
            .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
            .Where(fate => fate!.State is FateState.Preparing or FateState.Running or FateState.Ending)
            .Select(fate => fate!)
            .Select(fate => (Type: "FATE", Id: (uint)fate.FateId, Name: fate.Name.TextValue, Pos: fate.Position, Distance: Vector3.Distance(playerPos, fate.Position)))
            .ToList();

        var content = PublicContentOccultCrescent.GetInstance();
        var ces = content == null
            ? []
            : content->DynamicEventContainer.Events
                .ToArray()
                .Where(ev => ev.State != DynamicEventState.Inactive)
                .Select(ev => (Type: "CE", Id: (uint)ev.DynamicEventId, Name: ev.Name.ToString(), Pos: ev.MapMarker.Position, Distance: Vector3.Distance(playerPos, ev.MapMarker.Position)))
                .ToList();

        var targets = fates.Concat(ces).OrderBy(item => item.Distance).ToArray();
        if (targets.Length == 0)
        {
            distanceDebugLines.Add("当前没有活动 FATE/CE。");
            return;
        }

        foreach (var target in targets)
            distanceDebugLines.Add($"{target.Type} #{target.Id} {target.Name}: 距离 {target.Distance:F1}，坐标 {FormatPosition(target.Pos)}");
    }

    private static string FormatPosition(Vector3 pos)
        => $"({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})";

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogHelper.Warning(ex, "打开外部链接失败。");
            statusText = $"打开链接失败: {ex.Message}";
        }
    }

    private void DrawMapSelector()
    {
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
        if (ImGui.SmallButton("清空所有"))
        {
            state.ClearMap(config.LastSelectedMap);
            statusText = $"已清空 {GetMapName(config.LastSelectedMap)} 所有 Boss 时间记录。";
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"当前列表: {GetMapName(config.LastSelectedMap)}");
    }

    private void DrawDependencyStatus()
    {
        ImGui.TextUnformatted("依赖插件:");
        ImGui.SameLine();
        DrawDependencyLabel("vnavmesh", vnav.IsReady);
        ImGui.SameLine();
        DrawDependencyLabel("Lifestream >= 2.5.4.15", vnav.IsLifestreamAvailable, vnav.LifestreamStatus);
    }

    private static void DrawDependencyLabel(string name, bool installed, string? status = null)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, installed ? new Vector4(0.35f, 1f, 0.45f, 1f) : new Vector4(1f, 0.35f, 0.35f, 1f));
        ImGui.TextUnformatted($"{name}: {status ?? (installed ? "已安装" : "未安装")}");
        ImGui.PopStyleColor();
    }

    private void DrawBossTable(ExpeditionMap map)
    {
        if (!ImGui.BeginTable("##boss_table", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("简称", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("名称");
        ImGui.TableSetupColumn("掉落", ImGuiTableColumnFlags.WidthFixed, 50f);
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
            DrawDropMark(boss.Drop);
            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(FormatTime(state.GetAppearedAt(boss)));
            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(boss.Trigger);
            ImGui.TableSetColumnIndex(5);
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

    private unsafe void DrawAutoNavigation(ExpeditionMap? resolvedMap)
    {
        var autoNavigationEnabled = config.AutoNavigationEnabled;
        if (ImGui.Checkbox("全自动模式", ref autoNavigationEnabled))
        {
            config.AutoNavigationEnabled = autoNavigationEnabled;
            config.Save();
        }

        ImGui.SameLine();
        var autoPrioritizeCe = config.AutoPrioritizeCe;
        if (ImGui.Checkbox("优先 CE", ref autoPrioritizeCe))
        {
            config.AutoPrioritizeCe = autoPrioritizeCe;
            config.Save();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("自动参数");
        if (ImGui.BeginTable("##auto_nav_settings", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("出现后导航");
            ImGui.TableSetupColumn("结束后回营地");
            ImGui.TableSetupColumn("战斗进度跳过");
            ImGui.TableHeadersRow();

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted("导航延迟");
            ImGui.SetNextItemWidth(110f);
            var autoNavigationStartDelaySeconds = Math.Max(0, config.AutoNavigationStartDelaySeconds);
            if (ImGui.InputInt("秒##auto_nav_start_delay", ref autoNavigationStartDelaySeconds))
            {
                config.AutoNavigationStartDelaySeconds = Math.Clamp(autoNavigationStartDelaySeconds, 0, 600);
                config.Save();
            }
            ImGui.TextDisabled("目标出现后等待 X 秒再前往");

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted("回营地延迟");
            ImGui.SetNextItemWidth(110f);
            var autoReturnDelaySeconds = Math.Max(0, config.AutoReturnDelaySeconds);
            if (ImGui.InputInt("秒##auto_return_delay", ref autoReturnDelaySeconds))
            {
                config.AutoReturnDelaySeconds = Math.Clamp(autoReturnDelaySeconds, 0, 600);
                config.Save();
            }
            ImGui.TextDisabled("目标结束后等待 X 秒再回营地");

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted("跳过进度");
            ImGui.SetNextItemWidth(110f);
            var autoSkipProgressPercent = Math.Clamp(config.AutoSkipProgressPercent, 0, 100);
            if (ImGui.InputInt("%##auto_skip_progress", ref autoSkipProgressPercent))
            {
                config.AutoSkipProgressPercent = Math.Clamp(autoSkipProgressPercent, 0, 100);
                config.Save();
            }
            ImGui.TextDisabled("战斗进度 >= X% 时不再前往新目标");

            ImGui.EndTable();
        }

        if (!resolvedMap.HasValue)
        {
            ImGui.TextDisabled("当前未识别为新月岛地图。");
            return;
        }

        var content = PublicContentOccultCrescent.GetInstance();
        var fateBosses = BossCatalog.GetFates(resolvedMap.Value).ToArray();
        var ceBosses = BossCatalog.GetCriticalEncounters(resolvedMap.Value).ToArray();
        var enabledCeCount = ceBosses.Count(boss => !config.DisabledAutoCeIds.Contains((uint)boss.Index));
        var enabledFateCount = fateBosses.Count(boss => !config.DisabledAutoFateIds.Contains(boss.FateId!.Value));
        ImGui.TextUnformatted($"已勾选: CE {enabledCeCount}/{ceBosses.Length}  FATE {enabledFateCount}/{fateBosses.Length}");
        ImGui.SameLine();
        DrawAutoTargetBulkToggle("CE", ceBosses.Select(boss => (uint)boss.Index));
        ImGui.SameLine();
        DrawAutoTargetBulkToggle("FATE", fateBosses.Select(boss => (uint)boss.FateId!.Value));
        ImGui.SameLine();
        if (ImGui.SmallButton(config.HasAutoReturnStandbyPoint ? "更新待命点" : "记录待命点"))
        {
            var pos = DalamudApi.ObjectTable.LocalPlayer?.Position;
            var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
            if (pos.HasValue && currentMap.HasValue)
            {
                config.AutoReturnStandbyX = pos.Value.X;
                config.AutoReturnStandbyY = pos.Value.Y;
                config.AutoReturnStandbyZ = pos.Value.Z;
                config.AutoReturnStandbyMap = currentMap.Value;
                config.HasAutoReturnStandbyPoint = true;
                config.Save();
                LogHelper.Chat($"已记录待命点 {GetMapName(currentMap.Value)} ({pos.Value.X:F1}, {pos.Value.Y:F1}, {pos.Value.Z:F1})");
            }
        }
        if (config.HasAutoReturnStandbyPoint)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("清除待命点"))
            {
                config.HasAutoReturnStandbyPoint = false;
                config.Save();
                LogHelper.Chat("已清除待命点。");
            }
        }

        if (ImGui.CollapsingHeader("CE##auto_ce_targets", ImGuiTreeNodeFlags.DefaultOpen))
            DrawAutoCeTargetTable(ceBosses, content);

        if (ImGui.CollapsingHeader("FATE##auto_fate_targets", ImGuiTreeNodeFlags.DefaultOpen))
            DrawAutoFateTargetTable(fateBosses);
    }

    private void DrawAutoFateTargetTable(IReadOnlyList<BossEntry> bosses)
    {
        if (!ImGui.BeginTable("##auto_fate_table", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, 45f);
        ImGui.TableSetupColumn("FateId", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("简称", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("名称");
        ImGui.TableSetupColumn("状态", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("剩余", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableHeadersRow();

        foreach (var boss in bosses)
        {
            var fateId = boss.FateId!.Value;
            var fate = FindActiveFate(fateId);
            var enabled = !config.DisabledAutoFateIds.Contains(fateId);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox($"##auto_fate_{boss.Map}_{boss.Id}", ref enabled))
                SetAutoTargetEnabled("FATE", fateId, enabled);

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(fateId.ToString());
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(boss.Abbreviation);
            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(boss.Name);
            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(fate == null ? "未出现" : fate.State.ToString());
            ImGui.TableSetColumnIndex(5);
            ImGui.TextUnformatted(fate == null ? "--" : fate.TimeRemaining.ToString());
        }

        ImGui.EndTable();
    }

    private unsafe void DrawAutoCeTargetTable(IReadOnlyList<BossEntry> bosses, PublicContentOccultCrescent* content)
    {
        if (!ImGui.BeginTable("##auto_ce_table", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, 45f);
        ImGui.TableSetupColumn("EventId", ImGuiTableColumnFlags.WidthFixed, 65f);
        ImGui.TableSetupColumn("简称", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("名称");
        ImGui.TableSetupColumn("状态", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("进度", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("剩余", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableHeadersRow();

        foreach (var boss in bosses)
        {
            var eventId = (uint)boss.Index;
            var ev = FindActiveCriticalEncounter(content, boss);
            var enabled = !config.DisabledAutoCeIds.Contains(eventId);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox($"##auto_ce_{boss.Map}_{boss.Id}", ref enabled))
                SetAutoTargetEnabled("CE", eventId, enabled);

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(eventId.ToString());
            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(boss.Abbreviation);
            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(boss.Name);
            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(ev.HasValue ? ev.Value.State.ToString() : "未出现");
            ImGui.TableSetColumnIndex(5);
            ImGui.TextUnformatted(ev.HasValue ? $"{ev.Value.Progress}%" : "--");
            ImGui.TableSetColumnIndex(6);
            ImGui.TextUnformatted(ev.HasValue ? ev.Value.SecondsLeft.ToString() : "--");
        }

        ImGui.EndTable();
    }

    private static IFate? FindActiveFate(ushort fateId)
    {
        foreach (var fate in DalamudApi.FateTable)
        {
            if (fate == null || !DalamudApi.FateTable.IsValid(fate) || fate.FateId != fateId)
                continue;

            if (fate.State is FateState.Preparing or FateState.Running or FateState.Ending)
                return fate;
        }

        return null;
    }

    private static unsafe DynamicEvent? FindActiveCriticalEncounter(PublicContentOccultCrescent* content, BossEntry boss)
    {
        if (content == null)
            return null;

        foreach (var ev in content->DynamicEventContainer.Events)
        {
            if (ev.State != DynamicEventState.Inactive
                && BossCatalog.MatchesCriticalEncounter(boss, ev.DynamicEventId, ev.Name.ToString()))
                return ev;
        }

        return null;
    }

    private void DrawAutoTargetBulkToggle(string type, IEnumerable<uint> ids)
    {
        var list = type == "CE" ? config.DisabledAutoCeIds : config.DisabledAutoFateIds;
        var allIds = ids.Distinct().ToList();
        var allEnabled = allIds.All(id => !list.Contains(id));
        var label = allEnabled ? $"{type} 全不选" : $"{type} 全选";

        if (!ImGui.SmallButton($"{label}##auto_bulk_{type}"))
            return;

        if (allEnabled)
        {
            list.Clear();
            list.AddRange(allIds);
        }
        else
        {
            foreach (var id in allIds)
                list.Remove(id);
        }

        list.Sort();
        config.Save();
    }

    private void SetAutoTargetEnabled(string type, uint id, bool enabled)
    {
        var disabled = type == "CE" ? config.DisabledAutoCeIds : config.DisabledAutoFateIds;
        if (enabled)
            disabled.Remove(id);
        else if (!disabled.Contains(id))
            disabled.Add(id);

        disabled.Sort();
        config.Save();
    }

    private string ResolveFateState(BossEntry boss)
    {
        foreach (var fate in DalamudApi.FateTable)
        {
            if (fate == null || !DalamudApi.FateTable.IsValid(fate) || fate.FateId != boss.FateId)
                continue;

            if (fate.State is FateState.Preparing or FateState.Running or FateState.Ending)
                return fate.State.ToString();
        }

        return "未出现";
    }

    private unsafe string ResolveCeState(BossEntry boss, PublicContentOccultCrescent* content)
    {
        if (content != null)
        {
            foreach (var ev in content->DynamicEventContainer.Events)
            {
                if (ev.DynamicEventId == boss.Index && ev.State != DynamicEventState.Inactive)
                    return ev.State.ToString();
            }
        }

        return "未出现";
    }

    private void ClearAutoNavigationTarget()
    {
        config.AutoNavigationTargetType = string.Empty;
        config.AutoNavigationTargetId = 0;
        config.AutoNavigationTargetName = string.Empty;
        config.Save();
    }

    private void DrawImportExport(ExpeditionMap map)
    {
        ImGui.TextUnformatted("导入 / 导出（共享文本框）");
        ImGui.InputTextMultiline("##shared", ref sharedText, 4096, new Vector2(-1f, 80f));

        if (ImGui.Button("应用导入"))
        {
            var result = XydShoutParser.ApplyToState(sharedText, map, state);
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
        if (ImGui.Button("清空"))
            sharedText = string.Empty;

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
    }

    private void SetGeneratedOutput(string text, string label)
    {
        sharedText = text;
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

    private void DrawFateDebug(ExpeditionMap map)
    {
        if (!ImGui.CollapsingHeader("FATE/CE 调试区"))
            return;

        ImGui.TextUnformatted($"当前 FateTable.Length: {DalamudApi.FateTable.Length}");
        ImGui.TextDisabled("用于进图后确认 CE/FATE 的 FateId、名称和状态。把这里的数据反馈回来后，可写入 BossCatalog 做稳定匹配。");

        if (ImGui.Button("输出当前 FATE 到聊天"))
            PrintCurrentFatesToChat();

        ImGui.SameLine();
        if (ImGui.Button("扫描附近 EventObj"))
            ScanNearbyEventObjects();

        ImGui.SameLine();
        if (ImGui.Button("检测当前传送点"))
        {
            var id = vnav.GetCurrentAetheryteId();
            if (id.HasValue && id.Value != 0)
                LogHelper.Chat($"当前传送点 PlaceNameId={id.Value}");
            else
                LogHelper.Chat("未检测到传送点（不在传送点旁边或 Lifestream 未就绪）。");
        }

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

    private static void ScanNearbyEventObjects()
    {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        var playerPos = player.Position;
        var count = 0;
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null || obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)
                continue;

            var dist = Vector3.Distance(playerPos, obj.Position);
            if (dist > 50f) continue;

            LogHelper.Chat($"EventObj: BaseId={obj.BaseId} Name={obj.Name} Pos=({obj.Position.X:F2}, {obj.Position.Y:F2}, {obj.Position.Z:F2})");
            count++;
        }

        if (count == 0)
            LogHelper.Chat("附近 50 码内没有 EventObj。");
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
        DrawFateDebug(map);
        ImGui.Separator();
    }

    private void CopyAllDebugInfo(ExpeditionMap map)
    {
        var text = BuildDebugInfo(map);
        ImGui.SetClipboardText(text);
        statusText = "已复制全部调试信息到剪贴板。";
        LogHelper.Chat("已复制全部调试信息到剪贴板。");
    }

    private string BuildDebugInfo(ExpeditionMap map)
    {
        var sb = new StringBuilder();
        var territory = DalamudApi.ClientState.TerritoryType;
        var currentMap = TerritoryGate.ResolveMap(territory, config);
        var player = DalamudApi.ObjectTable.LocalPlayer;

        sb.AppendLine("[基础信息]");
        sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"当前 TerritoryType: {territory}");
        sb.AppendLine($"识别地图: {(currentMap.HasValue ? GetMapName(currentMap.Value) : "未识别")}");
        sb.AppendLine($"当前选中地图: {GetMapName(map)}");
        sb.AppendLine(player != null ? $"当前位置: {FormatPosition(player.Position)}" : "当前位置: 未找到玩家对象");
        sb.AppendLine($"岛 ID: {GetCurrentIslandId()}");
        sb.AppendLine();

        sb.AppendLine("[已观测 FATE]");
        var fateObservations = state.GetFateObservations(map);
        if (fateObservations.Count == 0)
        {
            sb.AppendLine("(无)");
        }
        else
        {
            foreach (var observation in fateObservations)
                sb.AppendLine($"#{observation.FateId} {observation.Name} state={observation.State} appeared={observation.AppearedAtLocal:HH:mm:ss} dur={observation.Duration} remain={observation.TimeRemaining} lv={observation.Level}/{observation.MaxLevel} pos=({observation.PositionX:F1},{observation.PositionY:F1},{observation.PositionZ:F1}) icon={observation.MapIconId} terr={observation.TerritoryType}");
        }
        sb.AppendLine();

        sb.AppendLine("[CE 公告记录]");
        var ceAnnouncements = state.GetCeAnnouncements(map);
        if (ceAnnouncements.Count == 0)
        {
            sb.AppendLine("(无)");
        }
        else
        {
            foreach (var announcement in ceAnnouncements)
                sb.AppendLine($"{announcement.ObservedAtLocal:HH:mm:ss} terr={announcement.TerritoryType} {announcement.Message}");
        }
        sb.AppendLine();

        sb.AppendLine("[CE 动态事件记录]");
        var ceObservations = state.GetCriticalEncounterObservations(map);
        if (ceObservations.Count == 0)
        {
            sb.AppendLine("(无)");
        }
        else
        {
            foreach (var observation in ceObservations)
                sb.AppendLine($"id={observation.DynamicEventId} name={observation.Name} state={observation.State} appeared={observation.AppearedAtLocal:HH:mm:ss} start={observation.StartTimestamp} dur={observation.SecondsDuration} left={observation.SecondsLeft} progress={observation.Progress}% players={observation.Participants}/{observation.MaxParticipants} pos=({observation.PositionX:F1},{observation.PositionY:F1}) type={observation.EventType}/{observation.DynamicEventType} icon={observation.MapIconId}");
        }
        sb.AppendLine();

        sb.AppendLine("[当前 FateTable]");
        var currentFates = DalamudApi.FateTable
            .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
            .ToArray();
        if (currentFates.Length == 0)
        {
            sb.AppendLine("(空)");
        }
        else
        {
            foreach (var fate in currentFates)
                sb.AppendLine($"#{fate!.FateId} {fate.Name.TextValue} state={fate.State} start={FormatEpoch(fate.StartTimeEpoch)} dur={fate.Duration} remain={fate.TimeRemaining} lv={fate.Level}/{fate.MaxLevel} progress={fate.Progress}% pos=({fate.Position.X:F1},{fate.Position.Y:F1},{fate.Position.Z:F1}) icon={fate.MapIconId} terr={fate.TerritoryType.RowId}");
        }
        sb.AppendLine();

        sb.AppendLine("[附近 EventObj 50y]");
        if (player == null)
        {
            sb.AppendLine("(未找到玩家对象)");
        }
        else
        {
            var eventObjects = DalamudApi.ObjectTable
                .Where(obj => obj != null && obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)
                .Select(obj => new { Obj = obj!, Dist = Vector3.Distance(player.Position, obj!.Position) })
                .Where(item => item.Dist <= 50f)
                .OrderBy(item => item.Dist)
                .ToArray();
            if (eventObjects.Length == 0)
            {
                sb.AppendLine("(无)");
            }
            else
            {
                foreach (var item in eventObjects)
                    sb.AppendLine($"dist={item.Dist:F1} baseId={item.Obj.BaseId} name={item.Obj.Name} pos=({item.Obj.Position.X:F2},{item.Obj.Position.Y:F2},{item.Obj.Position.Z:F2})");
            }
        }

        return sb.ToString();
    }

    private static string FormatTime(DateTime? time)
        => time.HasValue ? time.Value.ToString("HH:mm") : "--:--";

    private static void DrawDropMark(string drop)
    {
        if (string.IsNullOrEmpty(drop))
        {
            ImGui.TextUnformatted("");
            return;
        }

        var color = drop switch
        {
            "红" => new Vector4(1f, 0.3f, 0.3f, 1f),
            "黄" => new Vector4(1f, 0.85f, 0.2f, 1f),
            "紫" => new Vector4(0.75f, 0.35f, 1f, 1f),
            "绿" => new Vector4(0.35f, 0.9f, 0.35f, 1f),
            "蓝" => new Vector4(0.3f, 0.6f, 1f, 1f),
            "碧" => new Vector4(0.2f, 0.85f, 0.8f, 1f),
            "金" => new Vector4(0.95f, 0.8f, 0.3f, 1f),
            "α" => new Vector4(0.6f, 0.8f, 1f, 1f),
            "β" => new Vector4(1f, 0.75f, 0.35f, 1f),
            "γ" => new Vector4(0.75f, 1f, 0.5f, 1f),
            _ => new Vector4(1f, 1f, 1f, 1f),
        };

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(drop);
        ImGui.PopStyleColor();
    }

    private static string GetMapName(ExpeditionMap map)
        => map == ExpeditionMap.South ? "南征" : "北征";

    private string GetCurrentIslandId()
        => string.IsNullOrWhiteSpace(config.LastIslandId) ? "--" : config.LastIslandId;
}
