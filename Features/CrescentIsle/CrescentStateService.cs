namespace Chronicler;

internal sealed class CrescentStateService
{
    private readonly PluginConfiguration config;
    private readonly Dictionary<(ExpeditionMap Map, int BossId), DateTime?> records = new();

    public CrescentStateService(PluginConfiguration config)
    {
        this.config = config;

        foreach (var map in new[] { ExpeditionMap.South, ExpeditionMap.North })
        {
            foreach (var boss in BossCatalog.GetBosses(map))
            {
                var saved = config.Records.FirstOrDefault(record => record.Map == map && record.BossId == boss.Id);
                records[(map, boss.Id)] = saved?.AppearedAtLocal;
            }
        }
    }

    public DateTime? GetAppearedAt(BossEntry boss)
        => records.TryGetValue((boss.Map, boss.Id), out var value) ? value : null;

    public IReadOnlyDictionary<int, DateTime?> Snapshot(ExpeditionMap map)
        => BossCatalog.GetBosses(map).ToDictionary(boss => boss.Id, GetAppearedAt);

    public void RecordAppearance(BossEntry boss, DateTime appearedAtLocal)
    {
        records[(boss.Map, boss.Id)] = appearedAtLocal;
        Save();
    }

    public void Clear(BossEntry boss)
    {
        records[(boss.Map, boss.Id)] = null;
        Save();
    }

    public void ClearMap(ExpeditionMap map)
    {
        foreach (var boss in BossCatalog.GetBosses(map))
            records[(map, boss.Id)] = null;

        Save();
    }

    public void SetTimeFromXyd(BossEntry boss, string hhmm)
    {
        if (hhmm == "--:--")
        {
            Clear(boss);
            return;
        }

        var parts = hhmm.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute))
            return;

        var now = DateTime.Now;
        records[(boss.Map, boss.Id)] = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        Save();
    }

    public IReadOnlyList<FateObservationDto> GetFateObservations(ExpeditionMap map)
        => config.FateObservations
            .Where(observation => observation.Map == map)
            .OrderByDescending(observation => observation.AppearedAtLocal)
            .ThenBy(observation => observation.FateId)
            .ToArray();

    public bool RecordFateObservation(FateObservationDto observation)
    {
        var existing = config.FateObservations.FirstOrDefault(item => item.Map == observation.Map && item.FateId == observation.FateId);
        if (existing != null)
            return false;

        config.FateObservations.Add(observation);
        config.Save();
        return true;
    }

    public void ClearFateObservations(ExpeditionMap map)
    {
        config.FateObservations = config.FateObservations.Where(observation => observation.Map != map).ToList();
        config.Save();
    }

    public IReadOnlyList<CeAnnouncementDto> GetCeAnnouncements(ExpeditionMap map)
        => config.CeAnnouncements
            .Where(announcement => announcement.Map == map)
            .OrderByDescending(announcement => announcement.ObservedAtLocal)
            .ToArray();

    public bool RecordCeAnnouncement(ExpeditionMap map, uint territoryType, string message, DateTime observedAtLocal)
    {
        if (config.CeAnnouncements.Any(existing => existing.Map == map && existing.Message == message && Math.Abs((existing.ObservedAtLocal - observedAtLocal).TotalSeconds) < 30))
            return false;

        config.CeAnnouncements.Add(new CeAnnouncementDto
        {
            Map = map,
            TerritoryType = territoryType,
            Message = message,
            ObservedAtLocal = observedAtLocal,
        });

        config.Save();
        return true;
    }

    public void ClearCeAnnouncements(ExpeditionMap map)
    {
        config.CeAnnouncements = config.CeAnnouncements.Where(announcement => announcement.Map != map).ToList();
        config.Save();
    }

    public IReadOnlyList<CriticalEncounterObservationDto> GetCriticalEncounterObservations(ExpeditionMap map)
        => config.CriticalEncounterObservations
            .Where(observation => observation.Map == map)
            .OrderByDescending(observation => observation.AppearedAtLocal)
            .ThenBy(observation => observation.DynamicEventId)
            .ToArray();

    public bool RecordCriticalEncounterObservation(CriticalEncounterObservationDto observation)
    {
        var existing = config.CriticalEncounterObservations.FirstOrDefault(item => item.Map == observation.Map && item.DynamicEventId == observation.DynamicEventId);
        if (existing != null)
        {
            existing.Name = observation.Name;
            existing.State = observation.State;
            existing.StartTimestamp = observation.StartTimestamp;
            existing.SecondsLeft = observation.SecondsLeft;
            existing.SecondsDuration = observation.SecondsDuration;
            existing.Progress = observation.Progress;
            existing.Participants = observation.Participants;
            existing.MaxParticipants = observation.MaxParticipants;
            existing.EventType = observation.EventType;
            existing.DynamicEventType = observation.DynamicEventType;
            existing.PositionX = observation.PositionX;
            existing.PositionY = observation.PositionY;
            existing.MapIconId = observation.MapIconId;
            existing.TerritoryType = observation.TerritoryType;

            return false;
        }

        config.CriticalEncounterObservations.Add(observation);
        config.Save();
        return true;
    }

    public void ClearCriticalEncounterObservations(ExpeditionMap map)
    {
        config.CriticalEncounterObservations = config.CriticalEncounterObservations.Where(observation => observation.Map != map).ToList();
        config.Save();
    }

    private void Save()
    {
        config.Records = records
            .Select(pair => new BossRecordDto
            {
                Map = pair.Key.Map,
                BossId = pair.Key.BossId,
                AppearedAtLocal = pair.Value,
            })
            .ToList();

        config.Save();
    }
}
