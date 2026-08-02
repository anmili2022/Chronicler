using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace Chronicler;

internal sealed class FloatingStatusWindow : Window
{
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1f);

    private readonly PluginConfiguration config;
    private readonly Action toggleSettings;
    private readonly VnavService vnav;
    private readonly CurrencyGainTracker currencyGainTracker;
    private bool collapsed;

    public FloatingStatusWindow(PluginConfiguration config, Action toggleSettings, VnavService vnav, CurrencyGainTracker currencyGainTracker)
        : base(
            "##ChroniclerFloatingStatus",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav)
    {
        this.config = config;
        this.toggleSettings = toggleSettings;
        this.vnav = vnav;
        this.currencyGainTracker = currencyGainTracker;
        BgAlpha = 0.8f;
        SizeCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(420f, 220f);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public bool ShouldBeOpen => config.Enabled && config.ShowFloatingStatusWindow && IsInKnownMap();

    public override unsafe void Draw()
    {
        Flags = BuildFlags();

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            toggleSettings();

        if (DrawHeader())
            collapsed = !collapsed;

        if (config.AutoNavigationEnabled)
        {
            ImGui.SameLine();
            DrawStatusBadge("自动", new Vector4(0.22f, 0.45f, 0.28f, 1f), new Vector4(0.45f, 1f, 0.58f, 1f));

            if (config.AutoIslandRotationEnabled)
            {
                ImGui.SameLine();
                DrawStatusBadge("自动进出", new Vector4(0.16f, 0.32f, 0.5f, 1f), new Vector4(0.6f, 0.82f, 1f, 1f));
            }
        }

        if (collapsed)
            return;

        ImGui.Separator();

        var drewAny = false;
        drewAny |= DrawCurrentFates();
        drewAny |= DrawCurrentCriticalEncounters();

        if (!drewAny)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
            ImGui.TextUnformatted("当前无新月岛史官目标");
            ImGui.PopStyleColor();
        }

        if (vnav.IsReady)
        {
            ImGui.Separator();
            if (ImGui.SmallButton("清除导航"))
                vnav.Stop();
            ImGui.SameLine();
            if (ImGui.SmallButton("回营地"))
            {
                if (config.HasAutoReturnStandbyPoint)
                {
                    var target = new Vector3(config.AutoReturnStandbyX, config.AutoReturnStandbyY, config.AutoReturnStandbyZ);
                    vnav.ReturnToBaseCampThenNavigateTo(target, config.AutoReturnStandbyMap);
                }
                else
                {
                    vnav.ReturnToBaseCamp();
                }
            }
            ImGui.SameLine();
            if (ImGui.SmallButton(config.AutoNavigationEnabled ? "全自动: 开" : "全自动: 关"))
            {
                config.AutoNavigationEnabled = !config.AutoNavigationEnabled;
                LogHelper.Chat(config.AutoNavigationEnabled ? "全自动模式已开启。" : "全自动模式已关闭。");
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("CE 提升知见等级快\nFATE 提升辅助职业等级快");
            ImGui.SameLine();
            if (ImGui.SmallButton("待命点"))
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
                    LogHelper.Chat($"已记录待命点 {FormatMapName(currentMap.Value)} ({pos.Value.X:F1}, {pos.Value.Y:F1}, {pos.Value.Z:F1})");
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("记录、更新待命点");
            ImGui.SameLine();
            if (ImGui.SmallButton("Flag"))
                vnav.NavigateToFlag();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("导航到当前地图 Flag");
            ImGui.SameLine();
            if (ImGui.SmallButton("效率"))
                currencyGainTracker.PrintEfficiency();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("输出当前货币获取效率");
        }
    }

    private bool DrawHeader()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("新月岛史官");
        ImGui.PopStyleColor();

        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(collapsed ? "左键展开悬浮窗" : "左键折叠悬浮窗");
        return clicked;
    }

    private static string FormatMapName(ExpeditionMap map)
        => map == ExpeditionMap.South ? "南征" : "北征";

    private static void DrawStatusBadge(string label, Vector4 background, Vector4 textColor)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 3f));
        ImGui.PushStyleColor(ImGuiCol.Button, background);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, background);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, background);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        ImGui.Button(label);
        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(2);
    }

    private static void DrawDropMark(string drop)
    {
        if (string.IsNullOrEmpty(drop))
            return;

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
        ImGui.TextUnformatted($"[{drop}]");
        ImGui.PopStyleColor();
    }

    private ImGuiWindowFlags BuildFlags()
    {
        var flags = ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.AlwaysAutoResize
                    | ImGuiWindowFlags.NoFocusOnAppearing;

        if (config.LockFloatingStatusWindow)
            flags |= ImGuiWindowFlags.NoMove;

        return flags;
    }

    private unsafe void DrawFlagNavButton(Vector3 pos, string id, uint? preferredShardId = null, float? randomRadius = null, bool dismountOnArrival = false)
    {
        if (vnav.IsReady)
        {
            if (ImGui.SmallButton($"导航##{id}"))
            {
                if (config.ShowNavigationDebug)
                    LogHelper.Chat($"导航调试: 开始导航到 ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
                if (randomRadius.HasValue)
                    vnav.NavigateToRandomInRadius(pos, randomRadius.Value, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
                else
                    vnav.NavigateTo(pos, preferredShardId: preferredShardId, dismountOnArrival: dismountOnArrival);
            }
        }
    }

    private bool DrawCurrentFates()
    {
        var fates = DalamudApi.FateTable
            .Where(fate => fate != null && DalamudApi.FateTable.IsValid(fate))
            .Where(fate => fate!.State is FateState.Preparing or FateState.Running or FateState.Ending)
            .OrderBy(fate => fate!.TimeRemaining)
            .Take(8)
            .ToArray();

        if (fates.Length == 0)
            return false;

        ImGui.TextUnformatted("FATE");
        var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
        foreach (var fate in fates)
        {
            var boss = currentMap.HasValue
                ? BossCatalog.GetFates(currentMap.Value).FirstOrDefault(boss => boss.FateId == fate!.FateId
                    || boss.ObjectNameAliases.Any(alias => fate!.Name.TextValue.StartsWith(alias, StringComparison.Ordinal))
                    || boss.Name.Equals(fate!.Name.TextValue, StringComparison.Ordinal))
                : null;
            var name = fate!.Name.TextValue;
            ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
            ImGui.TextUnformatted(name);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextUnformatted($"{FormatFateState(fate.State)} {fate.Progress}% {FormatSeconds(fate.TimeRemaining)}");
            ImGui.SameLine();
            DrawFlagNavButton(fate.Position, $"fate-{fate.FateId}", VnavService.GetPreferredShardIdForFate(fate.FateId), dismountOnArrival: true);
        }

        return true;
    }

    private unsafe bool DrawCurrentCriticalEncounters()
    {
        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null)
            return false;

        var events = content->DynamicEventContainer.Events
            .ToArray()
            .Where(ev => ev.State != DynamicEventState.Inactive)
            .OrderBy(ev => ev.SecondsLeft)
            .Take(8)
            .ToArray();

        if (events.Length == 0)
            return false;

        ImGui.TextUnformatted("CE");
        var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
        foreach (var ev in events)
        {
            var boss = currentMap.HasValue
                ? BossCatalog.MatchCriticalEncounter(currentMap.Value, ev.DynamicEventId, ev.Name.ToString())
                : null;
            ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
            ImGui.TextUnformatted(ev.Name.ToString());
            ImGui.PopStyleColor();
            if (boss != null && !string.IsNullOrWhiteSpace(boss.Drop))
            {
                ImGui.SameLine();
                DrawDropMark(boss.Drop);
            }
            ImGui.SameLine();
            var registerRemaining = ev.State == DynamicEventState.Register && ev.StartTimestamp > 0
                ? (int)Math.Max(0, ev.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                : 0;
            ImGui.TextUnformatted(registerRemaining > 0
                ? $"{FormatCeState(ev.State)} {ev.Progress}% {FormatSeconds(ev.SecondsLeft)} (报名 {registerRemaining}秒)"
                : $"{FormatCeState(ev.State)} {ev.Progress}% {FormatSeconds(ev.SecondsLeft)}");
            ImGui.SameLine();
            DrawFlagNavButton(ev.MapMarker.Position, $"ce-{ev.DynamicEventId}", boss == null ? null : VnavService.GetPreferredShardIdForCriticalEncounter(currentMap!.Value, boss.Index), ev.MapMarker.Radius, dismountOnArrival: true);
        }

        return true;
    }

    private bool IsInKnownMap()
        => TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config).HasValue;

    private static string FormatFateState(FateState state)
        => state switch
        {
            FateState.Preparing => "准备",
            FateState.Running => "战斗",
            FateState.Ending => "结束中",
            FateState.Ended => "已结束",
            FateState.Failed => "失败",
            _ => state.ToString(),
        };

    private static string FormatCeState(DynamicEventState state)
        => state switch
        {
            DynamicEventState.Register => "报名",
            DynamicEventState.Warmup => "准备",
            DynamicEventState.Battle => "战斗",
            DynamicEventState.Inactive => "未激活",
            _ => state.ToString(),
        };

    private static string FormatSeconds(long seconds)
    {
        if (seconds <= 0)
            return "--:--";

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}" : $"{span.Minutes:D2}:{span.Seconds:D2}";
    }
}
