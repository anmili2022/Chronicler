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

    public FloatingStatusWindow(PluginConfiguration config, Action toggleSettings, VnavService vnav)
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

        ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
        ImGui.TextUnformatted("FATE / CE");
        ImGui.PopStyleColor();
        if (config.AutoNavigationEnabled)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 1f, 0.45f, 1f));
            ImGui.TextUnformatted("全自动中");
            ImGui.PopStyleColor();
        }
        ImGui.Separator();

        var drewAny = false;
        drewAny |= DrawCurrentFates();
        drewAny |= DrawCurrentCriticalEncounters();

        if (!drewAny)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
            ImGui.TextUnformatted("当前无 FATE/CE");
            ImGui.PopStyleColor();
        }

        if (vnav.IsReady)
        {
            ImGui.Separator();
            if (ImGui.SmallButton("清除导航"))
                vnav.Stop();
            ImGui.SameLine();
            if (ImGui.SmallButton("回营地"))
                vnav.ReturnToBaseCamp();
            ImGui.SameLine();
            if (ImGui.SmallButton(config.AutoNavigationEnabled ? "全自动: 开" : "全自动: 关"))
            {
                config.AutoNavigationEnabled = !config.AutoNavigationEnabled;
                config.Save();
                LogHelper.Chat(config.AutoNavigationEnabled ? "全自动模式已开启。" : "全自动模式已关闭。");
            }
        }
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

    private unsafe void DrawFlagNavButton(Vector3 pos, string id, uint? preferredShardId = null, float? randomRadius = null)
    {
        if (vnav.IsReady)
        {
            if (ImGui.SmallButton($"导航##{id}"))
            {
                if (config.ShowNavigationDebug)
                    LogHelper.Chat($"导航调试: 开始导航到 ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
                if (randomRadius.HasValue)
                    vnav.NavigateToRandomInRadius(pos, randomRadius.Value, preferredShardId: preferredShardId);
                else
                    vnav.NavigateTo(pos, preferredShardId: preferredShardId);
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
        foreach (var fate in fates)
        {
            var name = fate!.Name.TextValue;
            ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
            ImGui.TextUnformatted(name);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextUnformatted($"{FormatFateState(fate.State)} {fate.Progress}% {FormatSeconds(fate.TimeRemaining)}");
            ImGui.SameLine();
            DrawFlagNavButton(fate.Position, $"fate-{fate.FateId}", VnavService.GetPreferredShardIdForFate(fate.FateId));
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
        foreach (var ev in events)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
            ImGui.TextUnformatted(ev.Name.ToString());
            ImGui.PopStyleColor();
            ImGui.SameLine();
            var registerRemaining = ev.State == DynamicEventState.Register && ev.StartTimestamp > 0
                ? (int)Math.Max(0, ev.StartTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                : 0;
            ImGui.TextUnformatted(registerRemaining > 0
                ? $"{FormatCeState(ev.State)} {ev.Progress}% {FormatSeconds(ev.SecondsLeft)} (报名 {registerRemaining}秒)"
                : $"{FormatCeState(ev.State)} {ev.Progress}% {FormatSeconds(ev.SecondsLeft)}");
            ImGui.SameLine();
            DrawFlagNavButton(ev.MapMarker.Position, $"ce-{ev.DynamicEventId}", randomRadius: ev.MapMarker.Radius);
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
