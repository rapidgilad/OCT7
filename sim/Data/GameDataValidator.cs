using System.Collections.Generic;

namespace OCT7.Sim.Data
{
    /// <summary>Sanity checks for game data. Runs in unit tests and at game start.</summary>
    public static class GameDataValidator
    {
        public static List<string> Validate(GameDataSet data)
        {
            var errors = new List<string>();
            var e = data.Economy;
            if (e.PopCap <= 0)
            {
                errors.Add("economy.popCap must be > 0.");
            }

            if (e.HqManpowerPerMinute < 0 || e.HqMunitionsPerMinute < 0 || e.HqFuelPerMinute < 0)
            {
                errors.Add("economy HQ income must be >= 0.");
            }

            var factionIds = new HashSet<string>();
            foreach (var f in data.Factions)
            {
                if (string.IsNullOrWhiteSpace(f.Id))
                {
                    errors.Add("A faction has an empty id.");
                    continue;
                }

                if (!factionIds.Add(f.Id))
                {
                    errors.Add($"Duplicate faction id '{f.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(f.DisplayNameKey))
                {
                    errors.Add($"Faction '{f.Id}' has no displayNameKey.");
                }
            }

            var unitIds = new HashSet<string>();
            foreach (var u in data.Units)
            {
                if (string.IsNullOrWhiteSpace(u.Id))
                {
                    errors.Add("A unit has an empty id.");
                    continue;
                }

                if (!unitIds.Add(u.Id))
                {
                    errors.Add($"Duplicate unit id '{u.Id}'.");
                }

                if (!factionIds.Contains(u.FactionId))
                {
                    errors.Add($"Unit '{u.Id}' references unknown faction '{u.FactionId}'.");
                }

                if (u.Manpower < 0 || u.Munitions < 0 || u.Fuel < 0)
                {
                    errors.Add($"Unit '{u.Id}' has a negative cost.");
                }

                if (u.Pop <= 0)
                {
                    errors.Add($"Unit '{u.Id}' must have pop > 0.");
                }

                if (u.SquadSize <= 0)
                {
                    errors.Add($"Unit '{u.Id}' must have squadSize > 0.");
                }

                if (u.MoveSpeed <= 0)
                {
                    errors.Add($"Unit '{u.Id}' must have moveSpeed > 0.");
                }

                if (u.Tier < 0 || u.Tier > 3)
                {
                    errors.Add($"Unit '{u.Id}' has tier {u.Tier}; expected 0 (HQ) to 3.");
                }
            }

            foreach (var f in data.Factions)
            {
                CheckUnitList(f.Id, "startingUnits", f.StartingUnits, unitIds, data, errors);
                CheckUnitList(f.Id, "sandboxUnits", f.SandboxUnits, unitIds, data, errors);
            }

            return errors;
        }

        private static void CheckUnitList(string factionId, string field, List<string> list, HashSet<string> unitIds, GameDataSet data, List<string> errors)
        {
            if (list == null)
            {
                return;
            }

            foreach (var id in list)
            {
                if (!unitIds.Contains(id))
                {
                    errors.Add($"Faction '{factionId}' {field} references unknown unit '{id}'.");
                }
                else if (data.GetUnit(id).FactionId != factionId)
                {
                    errors.Add($"Faction '{factionId}' {field} uses '{id}' from another faction.");
                }
            }
        }
    }
}
