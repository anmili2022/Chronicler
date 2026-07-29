using Dalamud.Game.ClientState.Fates;

namespace Chronicler;

internal sealed class FateAppearanceDetector
{
    private readonly CrescentStateService state;
    private readonly HashSet<(ExpeditionMap Map, int BossId, ushort FateId)> seenFates = new();
    private DateTime lastScanUtc = DateTime.MinValue;

    public FateAppearanceDetector(CrescentStateService state)
    {
        this.state = state;
    }

    public void Update(ExpeditionMap? currentMap)
    {
        if (!currentMap.HasValue)
        {
            seenFates.Clear();
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastScanUtc < TimeSpan.FromMilliseconds(500))
            return;

        lastScanUtc = nowUtc;

        foreach (var fate in DalamudApi.FateTable)
        {
            if (fate == null || !DalamudApi.FateTable.IsValid(fate))
                continue;

            if (fate.State is not (FateState.Preparing or FateState.Running))
                continue;

            var appearedAt = ResolveAppearanceTime(fate);
            var isNewObservation = state.RecordFateObservation(new FateObservationDto
            {
                Map = currentMap.Value,
                FateId = fate.FateId,
                Name = fate.Name.TextValue,
                AppearedAtLocal = appearedAt,
                State = fate.State.ToString(),
                Duration = fate.Duration,
                TimeRemaining = fate.TimeRemaining,
                Level = fate.Level,
                MaxLevel = fate.MaxLevel,
                PositionX = fate.Position.X,
                PositionY = fate.Position.Y,
                PositionZ = fate.Position.Z,
                MapIconId = fate.MapIconId,
                TerritoryType = fate.TerritoryType.RowId,
            });

            if (isNewObservation)
                LogHelper.Info($"观测到 FATE #{fate.FateId} {fate.Name.TextValue}，出现时间 {appearedAt:HH:mm}。 ");

            var boss = MatchBoss(currentMap.Value, fate);
            if (boss == null)
                continue;

            var key = (boss.Map, boss.Id, fate.FateId);
            if (!seenFates.Add(key))
                continue;

            state.RecordAppearance(boss, appearedAt);
            LogHelper.Chat($"自动记录 {boss.Abbreviation} 出现时间。 ");
        }
    }

    private static BossEntry? MatchBoss(ExpeditionMap map, IFate fate)
    {
        var fateName = fate.Name.TextValue;
        return BossCatalog.GetBosses(map).FirstOrDefault(boss =>
            boss.FateId == fate.FateId
            || boss.ObjectNameAliases.Any(alias => fateName.Contains(alias, StringComparison.Ordinal))
            || boss.Name.Contains(fateName, StringComparison.Ordinal));
    }

    private static DateTime ResolveAppearanceTime(IFate fate)
    {
        if (fate.StartTimeEpoch > 0)
            return DateTimeOffset.FromUnixTimeSeconds(fate.StartTimeEpoch).LocalDateTime;

        if (fate.Duration > 0 && fate.TimeRemaining > 0)
        {
            var elapsed = Math.Max(0, fate.Duration - fate.TimeRemaining);
            return DateTime.Now - TimeSpan.FromSeconds(elapsed);
        }

        return DateTime.Now;
    }
}
