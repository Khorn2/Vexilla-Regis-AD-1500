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

        public int CurrentSize
        {
            get
            {
                if (removed)
                    return finalSize;

                if (unitReference == null)
                    return finalSize;

                return Mathf.Max(0, unitReference.CurrentSize);
            }
        }

        public int MenLost
        {
            get
            {
                if (removalReason == UnitRemovalReason.Routed)
                    return 0;

                return Mathf.Max(0, initialSize - CurrentSize);
            }
        }

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

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].unitReference == unit)
                return;
        }

        string unitName = unit.Stats != null ? unit.Stats.name : unit.name;
        int initialSize = unit.Stats != null ? unit.Stats.unitSize : unit.CurrentSize;

        records.Add(new UnitBattleRecord
        {
            unitName = unitName,
            teamId = unit.TeamId,
            initialSize = initialSize,
            finalSize = initialSize,
            removed = false,
            removalReason = UnitRemovalReason.None,
            unitReference = unit
        });
    }

    public void MarkUnitKilled(GameUnit unit)
    {
        UnitBattleRecord record = FindRecord(unit);
        if (record == null)
            return;

        if (record.removalReason == UnitRemovalReason.Routed)
            return;

        record.finalSize = 0;
        record.removed = true;
        record.removalReason = UnitRemovalReason.Killed;
    }

    public void MarkUnitRouted(GameUnit unit)
    {
        UnitBattleRecord record = FindRecord(unit);
        if (record == null)
            return;

        record.finalSize = unit != null ? Mathf.Max(0, unit.CurrentSize) : record.finalSize;
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
        int total = 0;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].teamId == teamId && records[i].UnitLost)
                total++;
        }

        return total;
    }
}