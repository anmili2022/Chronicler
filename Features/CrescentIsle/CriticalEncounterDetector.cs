using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace Chronicler;

internal sealed class CriticalEncounterDetector
{
    private readonly CrescentStateService state;
    private readonly HashSet<(ExpeditionMap Map, uint DynamicEventId)> recordedXydEvents = new();
    private DateTime lastScanUtc = DateTime.MinValue;

    public CriticalEncounterDetector(CrescentStateService state)
    {
        this.state = state;
    }

    public unsafe void Update(ExpeditionMap? currentMap, uint territoryType)
    {
        if (!currentMap.HasValue)
            return;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastScanUtc < TimeSpan.FromMilliseconds(500))
            return;

        lastScanUtc = nowUtc;

        var content = PublicContentOccultCrescent.GetInstance();
        if (content == null)
            return;

        foreach (var ev in content->DynamicEventContainer.Events)
        {
            if (ev.State == DynamicEventState.Inactive)
                continue;

            var appearedAt = ResolveAppearanceTime(ev);
            var name = ev.Name.ToString();
            state.RecordCriticalEncounterObservation(new CriticalEncounterObservationDto
            {
                Map = currentMap.Value,
                DynamicEventId = ev.DynamicEventId,
                Name = name,
                State = ev.State.ToString(),
                AppearedAtLocal = appearedAt,
                StartTimestamp = ev.StartTimestamp,
                SecondsLeft = ev.SecondsLeft,
                SecondsDuration = ev.SecondsDuration,
                Progress = ev.Progress,
                Participants = ev.Participants,
                MaxParticipants = ev.MaxParticipants,
                EventType = ev.EventType,
                DynamicEventType = ev.DynamicEventType,
                PositionX = ev.MapMarker.Position.X,
                PositionY = ev.MapMarker.Position.Y,
                MapIconId = ev.MapMarker.IconId,
                TerritoryType = territoryType,
            });

            if (ev.State != DynamicEventState.Register)
                continue;

            var boss = MatchBoss(currentMap.Value, name);
            if (boss == null)
                continue;

            if (!recordedXydEvents.Add((boss.Map, ev.DynamicEventId)))
                continue;

            state.RecordAppearance(boss, appearedAt);
            LogHelper.Info($"自动记录 CE {boss.Abbreviation} 出现时间，DynamicEventId={ev.DynamicEventId}，Name={name}。");
        }
    }

    private static DateTime ResolveAppearanceTime(DynamicEvent ev)
    {
        if (ev.StartTimestamp > 0)
            return DateTimeOffset.FromUnixTimeSeconds(ev.StartTimestamp).LocalDateTime;

        if (ev.SecondsDuration > 0 && ev.SecondsLeft > 0 && ev.SecondsDuration >= ev.SecondsLeft)
            return DateTime.Now - TimeSpan.FromSeconds(ev.SecondsDuration - ev.SecondsLeft);

        return DateTime.Now;
    }

    private static BossEntry? MatchBoss(ExpeditionMap map, string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return null;

        return BossCatalog.GetBosses(map).FirstOrDefault(boss =>
            boss.ObjectNameAliases.Any(alias => eventName.Contains(alias, StringComparison.Ordinal))
            || boss.Name.Contains(eventName, StringComparison.Ordinal)
            || eventName.Contains(boss.Abbreviation, StringComparison.Ordinal));
    }
}
