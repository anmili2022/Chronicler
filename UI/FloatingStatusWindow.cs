using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using XIVTreasure = Lumina.Excel.Sheets.Treasure;

namespace Chronicler;

internal sealed class FloatingStatusWindow : Window
{
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.3f, 1f);

    private readonly PluginConfiguration config;
    private readonly CrescentStateService state;
    private readonly Action toggleSettings;
    private readonly VnavService vnav;
    private readonly CurrencyGainTracker currencyGainTracker;
    private bool collapsed;
    private int? wideTextSilverChests;
    private int? wideTextCopperChests;
    private int? previousWideTextSilverChests;
    private int? previousWideTextCopperChests;
    private DateTime lastWideTextParseUtc = DateTime.MinValue;
    private readonly List<DetectedResource> detectedResources = new();
    private readonly List<DetectedResource> removedResources = new();
    private uint detectedResourceTerritory;

    private sealed record DetectedResource(string Type, Vector3 Position);

    public FloatingStatusWindow(PluginConfiguration config, CrescentStateService state, Action toggleSettings, VnavService vnav, CurrencyGainTracker currencyGainTracker)
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
        this.state = state;
        this.toggleSettings = toggleSettings;
        this.vnav = vnav;
        this.currencyGainTracker = currencyGainTracker;
        BgAlpha = 0.8f;
        SizeCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(420f, 220f);
        PositionCondition = ImGuiCond.FirstUseEver;
        DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
    }

    public override void OnClose()
    {
        DalamudApi.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_WideText", OnWideTextPostDraw);
        base.OnClose();
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

        DrawResourceCounts();
        var drewMagicPotWarning = DrawMagicPotRefreshWarnings();

        var drewAny = false;
        drewAny |= DrawCurrentFates();
        drewAny |= DrawCurrentCriticalEncounters();
        drewAny |= drewMagicPotWarning;

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
                LogHelper.Chat(config.AutoNavigationEnabled ? "模式已开启。" : "模式已关闭。", PluginMessageKind.AutoNavigation);
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

    private void DrawResourceCounts()
    {
        if (!config.ShowFloatingTreasureCounts && !config.ShowFloatingCarrotCount)
            return;

        var territory = DalamudApi.ClientState.TerritoryType;
        if (detectedResourceTerritory != territory)
        {
            detectedResources.Clear();
            removedResources.Clear();
            detectedResourceTerritory = territory;
            wideTextSilverChests = null;
            wideTextCopperChests = null;
            previousWideTextSilverChests = null;
            previousWideTextCopperChests = null;
        }

        var copperChests = 0;
        var silverChests = 0;
        var carrots = 0;
        var visibleResources = new List<DetectedResource>();
        var openedResources = new List<DetectedResource>();

        var treasureSheet = DalamudApi.DataManager.GetExcelSheet<XIVTreasure>();
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null)
                continue;

            if (config.ShowFloatingTreasureCounts)
            {
                if (obj.ObjectKind == ObjectKind.Treasure
                    && treasureSheet.GetRow(obj.BaseId).SGB.RowId == 1596)
                {
                    var resource = new DetectedResource("铜宝箱", obj.Position);
                    if (IsTreasureOpened(obj))
                        openedResources.Add(resource);
                    if (!obj.IsValid() || obj.IsDead)
                        continue;

                    copperChests++;
                    visibleResources.Add(resource);
                    AddDetectedResource(resource);
                }
                else if (obj.ObjectKind == ObjectKind.Treasure
                    && treasureSheet.GetRow(obj.BaseId).SGB.RowId == 1597)
                {
                    var resource = new DetectedResource("银宝箱", obj.Position);
                    if (IsTreasureOpened(obj))
                        openedResources.Add(resource);
                    if (!obj.IsValid() || obj.IsDead)
                        continue;

                    silverChests++;
                    visibleResources.Add(resource);
                    AddDetectedResource(resource);
                }
            }

            if (!obj.IsValid() || obj.IsDead)
                continue;

            if (config.ShowFloatingCarrotCount
                && obj.ObjectKind == ObjectKind.EventObj
                && obj.BaseId == 2010139)
            {
                carrots++;
                var resource = new DetectedResource("胡萝卜", obj.Position);
                visibleResources.Add(resource);
                AddDetectedResource(resource);
            }
        }

        var playerPosition = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (playerPosition.HasValue)
        {
            var resourcesToRemove = detectedResources.Where(resource =>
                Vector3.Distance(playerPosition.Value, resource.Position) <= 10f
                && (openedResources.Any(opened => opened.Type == resource.Type
                    && Vector3.Distance(opened.Position, resource.Position) <= 10f)
                    || !visibleResources.Any(visible => visible.Type == resource.Type
                        && Vector3.Distance(visible.Position, resource.Position) <= 10f)))
                .ToArray();
            foreach (var resource in resourcesToRemove)
            {
                detectedResources.Remove(resource);
                AddRemovedResource(resource);
            }
        }

        var carrotObjects = detectedResources.Where(resource => resource.Type == "胡萝卜").ToArray();
        var chestObjects = detectedResources.Where(resource => resource.Type is "铜宝箱" or "银宝箱").ToArray();

        if (config.ShowFloatingTreasureCounts)
        {
            var removedCopper = removedResources.Count(resource => resource.Type == "铜宝箱");
            var removedSilver = removedResources.Count(resource => resource.Type == "银宝箱");
            var displayCopper = Math.Max(0, (wideTextCopperChests ?? copperChests) - removedCopper);
            var displaySilver = Math.Max(0, (wideTextSilverChests ?? silverChests) - removedSilver);
            ImGui.TextUnformatted($"铜宝箱 {displayCopper} 个  银宝箱 {displaySilver} 个");
            if (config.ShowFloatingCarrotCount)
                ImGui.SameLine();
        }

        if (config.ShowFloatingCarrotCount)
        {
            ImGui.TextUnformatted($"胡萝卜 {carrots} 个");

            foreach (var carrot in carrotObjects.OrderBy(carrot => Vector3.DistanceSquared(
                         playerPosition ?? carrot.Position,
                         carrot.Position)).Select((carrot, index) => (carrot, index)))
            {
                ImGui.Indent();
                ImGui.TextUnformatted($"胡萝卜 ({carrot.carrot.Position.X:F1}, {carrot.carrot.Position.Y:F1}, {carrot.carrot.Position.Z:F1})");
                if (vnav.IsReady)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"导航##carrot-{carrot.index}"))
                        vnav.NavigateTo(carrot.carrot.Position, fly: false);
                }
                ImGui.Unindent();
            }
        }

        if (config.ShowFloatingTreasureCounts)
        {
            foreach (var chest in chestObjects.OrderBy(chest => Vector3.DistanceSquared(
                         playerPosition ?? chest.Position,
                         chest.Position)).Select((chest, index) => (chest, index)))
            {
                ImGui.Indent();
                ImGui.TextUnformatted($"{chest.chest.Type} ({chest.chest.Position.X:F1}, {chest.chest.Position.Y:F1}, {chest.chest.Position.Z:F1})");
                if (vnav.IsReady)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"导航##chest-{chest.index}"))
                        vnav.NavigateTo(chest.chest.Position, fly: false);
                }
                ImGui.Unindent();
            }
        }

        ImGui.Separator();
    }

    private bool DrawMagicPotRefreshWarnings()
    {
        var currentMap = TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config);
        if (!currentMap.HasValue)
            return false;

        var now = DateTime.Now;
        var warnings = new List<(BossEntry Boss, DateTime NextRefresh, TimeSpan Remaining)>();
        foreach (var boss in BossCatalog.GetFates(currentMap.Value).Where(BossCatalog.IsMagicPot))
        {
            var lastAppeared = state.GetAppearedAt(boss);
            if (!lastAppeared.HasValue)
                continue;

            var nextRefresh = lastAppeared.Value.AddHours(1);
            while (nextRefresh <= now)
                nextRefresh = nextRefresh.AddHours(1);

            var remaining = nextRefresh - now;
            if (remaining <= TimeSpan.FromMinutes(5))
            {
                warnings.Add((boss, nextRefresh, remaining));
            }
        }

        warnings = warnings.OrderBy(warning => warning.Remaining).ToList();

        if (warnings.Count == 0)
            return false;

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.2f, 0.2f, 1f));
        ImGui.TextUnformatted("魔法罐即将刷新");
        ImGui.PopStyleColor();
        foreach (var warning in warnings)
        {
            var remaining = warning.Remaining.TotalSeconds <= 0
                ? "即将刷新"
                : $"{(int)warning.Remaining.TotalMinutes:D2}:{warning.Remaining.Seconds:D2} 后刷新";
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.2f, 0.2f, 1f));
            ImGui.TextUnformatted($"{warning.Boss.Abbreviation} {remaining} ({warning.NextRefresh:HH:mm})");
            ImGui.PopStyleColor();

            var position = BossPositionCatalog.Find(warning.Boss);
            if (position != null && vnav.IsReady)
            {
                var routes = RouteCatalog.GetRoutes(warning.Boss.Map, warning.Boss.Id, config);
                ImGui.SameLine();
                if (ImGui.SmallButton($"导航##magic-pot-{warning.Boss.Map}-{warning.Boss.Id}"))
                    vnav.NavigateToTarget(position.Position, routes, dismountOnArrival: false);
            }
        }

        return true;
    }

    private void AddDetectedResource(DetectedResource resource)
    {
        if (removedResources.Any(removed => removed.Type == resource.Type
            && Vector3.Distance(removed.Position, resource.Position) <= 10f))
            return;

        if (detectedResources.Any(existing => existing.Type == resource.Type
            && Vector3.Distance(existing.Position, resource.Position) <= 1f))
            return;

        detectedResources.Add(resource);
    }

    private void AddRemovedResource(DetectedResource resource)
    {
        if (!removedResources.Any(removed => removed.Type == resource.Type
            && Vector3.Distance(removed.Position, resource.Position) <= 10f))
            removedResources.Add(resource);
    }

    private static unsafe bool IsTreasureOpened(Dalamud.Game.ClientState.Objects.Types.IGameObject obj)
    {
        var gameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(void*)obj.Address;
        if (gameObject == null)
            return false;

        var treasure = (FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure*)gameObject;
        return treasure->Flags.HasFlag(
            FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure.TreasureFlags.Opened);
    }

    private unsafe void OnWideTextPostDraw(AddonEvent type, AddonArgs args)
    {
        if (TerritoryGate.ResolveMap(DalamudApi.ClientState.TerritoryType, config) == null)
            return;

        if (DateTime.UtcNow - lastWideTextParseUtc < TimeSpan.FromSeconds(5))
            return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible)
            return;

        var node = addon->GetNodeById(3);
        if (node == null)
            return;

        lastWideTextParseUtc = DateTime.UtcNow;
        var text = node->GetAsAtkTextNode()->NodeText.ToString();

        var match = Regex.Match(
            text,
            @"(?<silver>\d+)\s*个银宝箱.*?(?<copper>\d+)\s*个铜宝箱",
            RegexOptions.Singleline);
        if (!match.Success || match.Groups.Count < 3)
            return;

        wideTextSilverChests = int.Parse(match.Groups[1].Value);
        wideTextCopperChests = int.Parse(match.Groups[2].Value);
        if (previousWideTextSilverChests.HasValue
            && wideTextSilverChests > previousWideTextSilverChests)
            removedResources.RemoveAll(resource => resource.Type == "银宝箱");

        if (previousWideTextCopperChests.HasValue
            && wideTextCopperChests > previousWideTextCopperChests)
            removedResources.RemoveAll(resource => resource.Type == "铜宝箱");

        previousWideTextSilverChests = wideTextSilverChests;
        previousWideTextCopperChests = wideTextCopperChests;
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

    private unsafe void DrawFlagNavButton(Vector3 pos, string id, uint? preferredShardId = null, float? randomRadius = null, bool dismountOnArrival = false, IReadOnlyList<BossRouteDto>? routes = null)
    {
        if (vnav.IsReady)
        {
            if (ImGui.SmallButton($"导航##{id}"))
            {
                if (config.ShowNavigationDebug)
                    LogHelper.Chat($"开始导航到 ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})", PluginMessageKind.NavigationDebug);
                vnav.NavigateToTarget(pos, routes, preferredShardId, randomRadius, dismountOnArrival);
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
            var dropMark = (boss?.Drop ?? string.Empty) switch
            {
                var reward when reward.StartsWith("消幻晶α", StringComparison.Ordinal) => "α",
                var reward when reward.StartsWith("消幻晶β", StringComparison.Ordinal) => "β",
                var reward when reward.StartsWith("消幻晶γ", StringComparison.Ordinal) => "γ",
                _ => string.Empty,
            };
            if (!string.IsNullOrEmpty(dropMark))
            {
                ImGui.SameLine();
                DrawDropMark(dropMark);
            }
            ImGui.SameLine();
            ImGui.TextUnformatted($"{FormatFateState(fate.State)} {fate.Progress}% {FormatSeconds(fate.TimeRemaining)}");
            ImGui.SameLine();
            var bossRoutes = boss != null && currentMap.HasValue
                ? RouteCatalog.GetRoutes(currentMap.Value, boss.Id, config)
                : null;
            DrawFlagNavButton(fate.Position, $"fate-{fate.FateId}", VnavService.GetPreferredShardIdForFate(fate.FateId), dismountOnArrival: true, routes: bossRoutes);
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
            var ceRoutes = boss != null && currentMap.HasValue
                ? RouteCatalog.GetRoutes(currentMap.Value, boss.Id, config)
                : null;
            DrawFlagNavButton(ev.MapMarker.Position, $"ce-{ev.DynamicEventId}", boss == null ? null : VnavService.GetPreferredShardIdForCriticalEncounter(currentMap!.Value, boss.Index), ev.MapMarker.Radius, dismountOnArrival: boss != null && VnavService.RollCriticalEncounterDismount(), routes: ceRoutes);
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
