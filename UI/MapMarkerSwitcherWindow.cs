using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;

namespace Chronicler;

internal sealed class MapMarkerSwitcherWindow : Window
{
    private readonly PluginConfiguration config;
    private readonly CrescentMapMarkerController markers;

    private static readonly (CrescentMapMarkerController.MarkerSet Flag, uint IconId, string Tooltip)[] Buttons =
    [
        (CrescentMapMarkerController.MarkerSet.Bronze, 60356, "铜宝箱"),
        (CrescentMapMarkerController.MarkerSet.Silver, 60355, "银宝箱"),
        (CrescentMapMarkerController.MarkerSet.MagicPot, 60354, "魔法罐"),
        (CrescentMapMarkerController.MarkerSet.Reroll, 61473, "第二次机会宝箱"),
        (CrescentMapMarkerController.MarkerSet.Carrot, 25207, "胡萝卜"),
        (CrescentMapMarkerController.MarkerSet.Survey, 60468, "调查点"),
    ];

    public MapMarkerSwitcherWindow(PluginConfiguration config, CrescentMapMarkerController markers)
        : base("##ChroniclerMapMarkerSwitcher", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)
    {
        this.config = config;
        this.markers = markers;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override void PreDraw()
    {
        IsOpen = config.ShowMapMarkerSwitcher && TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config).HasValue;
        if (!IsOpen)
            return;

        var addon = DalamudApi.GameGui.GetAddonByName("AreaMap");
        if (addon == nint.Zero || !addon.IsVisible)
        {
            IsOpen = false;
            return;
        }

        Position = new Vector2(addon.X + 5f, addon.Y - 52f);
        PositionCondition = ImGuiCond.Always;
    }

    public void UpdateVisibility()
    {
        IsOpen = false;
        if (!config.ShowMapMarkerSwitcher || !TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config).HasValue)
            return;

        var addon = DalamudApi.GameGui.GetAddonByName("AreaMap");
        if (addon == nint.Zero || !addon.IsVisible)
            return;

        Size = new Vector2(32f * Buttons.Length + 8f * (Buttons.Length + 1), 48f);
        Position = new Vector2(addon.X + 5f, addon.Y - Size.Value.Y - 4f);
        PositionCondition = ImGuiCond.Always;
        IsOpen = true;
    }

    public override void Draw()
    {
        var current = markers.GetMarkerSet();
        foreach (var (flag, iconId, tooltip, index) in Buttons.Select((button, index) => (button.Flag, button.IconId, button.Tooltip, index)))
        {
            if (index > 0)
                ImGui.SameLine();

            var active = current.HasFlag(flag);
            ImGui.PushStyleColor(ImGuiCol.Button, active ? new Vector4(0.1f, 0.7f, 0.2f, 1f) : new Vector4(0.28f, 0.28f, 0.28f, 1f));
            var texture = DalamudApi.TextureProvider.GetFromGameIcon(iconId).GetWrapOrDefault();
            ImGui.PushID($"map-marker-{iconId}-{flag}");
            var clicked = texture != null && ImGui.ImageButton(texture.Handle, new Vector2(32f, 32f));
            ImGui.PopID();
            ImGui.PopStyleColor();
            if (clicked)
                markers.SetMarkerSet(active ? current & ~flag : current | flag);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }
    }
}
