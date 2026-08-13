using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Chronicler;

internal sealed record AchievementProgressInfo(uint AchievementId, string Name, uint Current, uint Max, bool Complete);

internal sealed class AchievementProgressService
{
    private const uint PasserbySaintAchievementId = 1357;
    private readonly PluginConfiguration config;
    private readonly Dictionary<uint, AchievementProgressInfo> cache = new();
    private DateTime lastRequestUtc = DateTime.MinValue;
    private string? characterKey;
    private bool achievementWindowRequested;

    public AchievementProgressService(PluginConfiguration config)
    {
        this.config = config;
    }

    public IReadOnlyList<AchievementProgressInfo> Snapshot()
    {
        EnsureTrackedIds();
        return cache.Values.ToArray();
    }

    public unsafe void Update(bool force = false)
    {
        if (!DalamudApi.ClientState.IsLoggedIn)
        {
            characterKey = null;
            achievementWindowRequested = false;
            return;
        }

        var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
        if (localPlayer == null)
            return;

        var currentCharacterKey = localPlayer.Name.TextValue;
        if (characterKey != currentCharacterKey)
        {
            characterKey = currentCharacterKey;
            achievementWindowRequested = false;
            lastRequestUtc = DateTime.MinValue;
            ResetProgress();
        }

        // The client does not populate achievement progress for a newly selected
        // character until the achievement window has been opened once.
        if (!achievementWindowRequested)
        {
            DalamudApi.Commands.ProcessCommand("/achievement");
            achievementWindowRequested = true;
        }

        var achievement = Achievement.Instance();
        if (achievement == null)
            return;

        EnsureTrackedIds();
        if (cache.Count == 0)
            return;

        foreach (var id in cache.Keys)
            UpdateSingle(achievement, id, force);
    }

    private unsafe void UpdateSingle(Achievement* achievement, uint id, bool force)
    {
        if (!force && achievement->IsLoaded() && achievement->IsComplete((int)id))
        {
            var max = ResolveMax(id);
            cache[id] = cache[id] with { Current = max, Max = max, Complete = true };
            return;
        }

        if (!force
            && achievement->ProgressAchievementId == id
            && achievement->ProgressRequestState == Achievement.AchievementState.Loaded)
        {
            var max = achievement->ProgressMax == 0 ? ResolveMax(id) : achievement->ProgressMax;
            cache[id] = cache[id] with { Current = achievement->ProgressCurrent, Max = max, Complete = achievement->ProgressCurrent >= max };
            return;
        }

        if (!force && DateTime.UtcNow - lastRequestUtc < TimeSpan.FromSeconds(30))
            return;

        achievement->RequestAchievementProgress(id);
        lastRequestUtc = DateTime.UtcNow;
    }

    private uint ResolveMax(uint id) => id switch
    {
        _ when id == config.AchievementSouthDoctorId => 500,
        _ when id == config.AchievementNorthDoctorId => 500,
        _ => 0,
    };

    private void EnsureTrackedIds()
    {
        var sheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Achievement>();
        var southId = config.AchievementSouthDoctorId;
        var northId = config.AchievementNorthDoctorId;
        var passerbySaintId = PasserbySaintAchievementId;
        var trackedIds = new HashSet<uint>();

        if (southId > 0)
        {
            var name = ResolveName(sheet, southId) ?? "南岛三角区·船医3";
            AddOrUpdateTracked(southId, name);
            trackedIds.Add(southId);
        }

        if (northId > 0)
        {
            var name = ResolveName(sheet, northId) ?? "北岛三角区·名医3";
            AddOrUpdateTracked(northId, name);
            trackedIds.Add(northId);
        }

        if (passerbySaintId > 0)
        {
            var name = ResolveName(sheet, passerbySaintId) ?? "过路圣人";
            AddOrUpdateTracked(passerbySaintId, name);
            trackedIds.Add(passerbySaintId);
        }

        foreach (var id in cache.Keys.Where(id => !trackedIds.Contains(id)).ToArray())
            cache.Remove(id);
    }

    private void AddOrUpdateTracked(uint id, string name)
    {
        if (!cache.ContainsKey(id))
            cache[id] = new AchievementProgressInfo(id, name, 0, 0, false);
    }

    private void ResetProgress()
    {
        foreach (var id in cache.Keys.ToArray())
        {
            var info = cache[id];
            cache[id] = info with { Current = 0, Max = 0, Complete = false };
        }
    }

    private static string? ResolveName(Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Achievement>? sheet, uint id)
        => sheet?.GetRow(id).Name.ToString();
}
