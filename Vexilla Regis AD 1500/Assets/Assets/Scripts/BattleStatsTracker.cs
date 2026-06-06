using System.Collections.Generic;
using UnityEngine;

public enum UnitRemovalReason
{
    None,
    Killed,
    Routed
}

public class BattleStatsTracker : MonoBehaviour
{
    public class UnitBattleRecord
    {
        public string unitName;
        public int teamId;
        public int initialSize;
        public int finalSize;
        public bool removed;
        public UnitRemovalReason removalReason;
        public GameUnit unitReference;

        public int CurrentSize => Mathf.Max(0, finalSize);
        public int MenLost => Mathf.Max(0, initialSize - finalSize);
        public int MenRemaining => Mathf.Max(0, finalSize);
        public bool UnitLost => removalReason == UnitRemovalReason.Killed;
        public bool UnitRouted => removalReason == UnitRemovalReason.Routed;
    }

    private readonly List<UnitBattleRecord> records = new List<UnitBattleRecord>();

    public IReadOnlyList<UnitBattleRecord> Records => records;

    public void Clear()
    {
        records.Clear();
    }

    public void RegisterScenarioUnit(GameUnit unit)
    {
        if (unit == null)
            return;

        UnitBattleRecord existing = FindRecord(unit);
        if (existing != null)
            return;

        string unitName = unit.Stats != null && !string.IsNullOrWhiteSpace(unit.Stats.unitName)
            ? unit.Stats.unitName
            : unit.name;

        int initialSize = unit.Stats != null
            ? unit.Stats.unitSize
            : unit.CurrentSize;

        records.Add(new UnitBattleRecord
        {
            unitName = unitName,
            teamId = unit.TeamId,
            initialSize = Mathf.Max(0, initialSize),
            finalSize = Mathf.Max(0, unit.CurrentSize),
            removed = false,
            removalReason = UnitRemovalReason.None,
            unitReference = unit
        });
    }

    public void ForceFinalSync()
    {
        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit unit = GameUnit.AllUnits[i];

            if (unit == null)
                continue;

            UpdateUnitCurrentSize(unit);
        }

        for (int i = 0; i < records.Count; i++)
        {
            UnitBattleRecord record = records[i];

            if (record == null)
                continue;

            if (record.unitReference == null)
                continue;

            record.teamId = record.unitReference.TeamId;

            if (!record.removed)
                record.finalSize = Mathf.Max(0, record.unitReference.CurrentSize);
        }
    }

    public void UpdateUnitCurrentSize(GameUnit unit)
    {
        if (unit == null)
            return;

        UnitBattleRecord record = FindRecord(unit);

        if (record == null)
        {
            RegisterScenarioUnit(unit);
            record = FindRecord(unit);
        }

        if (record == null)
            return;

        if (record.removed)
            return;

        record.teamId = unit.TeamId;
        record.finalSize = Mathf.Max(0, unit.CurrentSize);
    }

    public void MarkUnitKilled(GameUnit unit)
    {
        if (unit == null)
            return;

        UnitBattleRecord record = FindRecord(unit);

        if (record == null)
        {
            RegisterScenarioUnit(unit);
            record = FindRecord(unit);
        }

        if (record == null)
            return;

        record.teamId = unit.TeamId;
        record.finalSize = 0;
        record.removed = true;
        record.removalReason = UnitRemovalReason.Killed;
    }

    public void MarkUnitRouted(GameUnit unit)
    {
        if (unit == null)
            return;

        UnitBattleRecord record = FindRecord(unit);

        if (record == null)
        {
            RegisterScenarioUnit(unit);
            record = FindRecord(unit);
        }

        if (record == null)
            return;

        record.teamId = unit.TeamId;
        record.finalSize = Mathf.Max(0, unit.CurrentSize);
        record.removed = true;
        record.removalReason = UnitRemovalReason.Routed;
    }

    private UnitBattleRecord FindRecord(GameUnit unit)
    {
        if (unit == null)
            return null;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].unitReference == unit)
                return records[i];
        }

        return null;
    }

    public int GetTotalInitialMen(int teamId)
    {
        ForceFinalSync();

        int total = 0;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].teamId == teamId)
                total += records[i].initialSize;
        }

        return total;
    }

    public int GetTotalMenLost(int teamId)
    {
        ForceFinalSync();

        int total = 0;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].teamId == teamId)
                total += records[i].MenLost;
        }

        return total;
    }

    public int GetTotalUnits(int teamId)
    {
        ForceFinalSync();

        int total = 0;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].teamId == teamId)
                total++;
        }

        return total;
    }

    public int GetUnitsLost(int teamId)
    {
        ForceFinalSync();

        int total = 0;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].teamId == teamId && records[i].UnitLost)
                total++;
        }

        return total;
    }

    public int GetUnitsRouted(int teamId)
    {
        ForceFinalSync();

        int total = 0;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].teamId == teamId && records[i].UnitRouted)
                total++;
        }

        return total;
    }
}