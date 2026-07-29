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
    private readonly Action openSettings;

    public FloatingStatusWindow(PluginConfiguration config, Action openSettings)
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
        this.openSettings = openSettings;
        BgAlpha = 0.8f;
        SizeCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(420f, 220f);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public bool ShouldBeOpen => config.Enabled && config.ShowFloatingStatusWindow && IsInKnownMap();

    public override unsafe void Draw()
    {
        Flags = BuildFlags();
        DrawContextMenu();

        ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
        ImGui.TextUnformatted("FATE / CE");
        ImGui.PopStyleColor();
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
    }

    private ImGuiWindowFlags BuildFlags()
    {
        var flags = ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.AlwaysAutoResize
                    | ImGuiWindowFlags.NoFocusOnAppearing
                    | ImGuiWindowFlags.NoNav;

        if (config.LockFloatingStatusWindow)
            flags |= ImGuiWindowFlags.NoMove;

        return flags;
    }

    private void DrawContextMenu()
    {
        if (!ImGui.BeginPopupContextWindow("##chronicler_floating_context"))
            return;

        if (ImGui.MenuItem("打开设置"))
            openSettings();

        var locked = config.LockFloatingStatusWindow;
        if (ImGui.MenuItem("锁定悬浮窗", string.Empty, locked))
        {
            config.LockFloatingStatusWindow = !locked;
            config.Save();
        }

        if (ImGui.MenuItem("隐藏悬浮窗"))
        {
            config.ShowFloatingStatusWindow = false;
            config.Save();
        }

        ImGui.EndPopup();
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
            ImGui.TextUnformatted($"{FormatCeState(ev.State)} {ev.Progress}% {FormatSeconds(ev.SecondsLeft)}");
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
