using System.Collections.Generic;
using OCT7.Sim.World;

namespace OCT7.Sim.Data
{
    /// <summary>Sanity checks for game data. Runs in unit tests and at game start.</summary>
    public static class GameDataValidator
    {
        public static List<string> Validate(GameDataSet data)
        {
            var errors = new List<string>();
            ValidateEconomy(data, errors);
            var factionIds = ValidateFactions(data, errors);
            var weaponIds = ValidateWeapons(data, errors);
            var unitIds = ValidateUnits(data, factionIds, weaponIds, errors);
            ValidateStructures(data, factionIds, unitIds, errors);
            ValidateFactionReferences(data, unitIds, errors);
            ValidateMaps(data, errors);
            ValidateAi(data, errors);
            return errors;
        }

        private static void ValidateEconomy(GameDataSet data, List<string> errors)
        {
            var e = data.Economy;
            if (e.PopCap <= 0)
            {
                errors.Add("economy.popCap must be > 0.");
            }

            if (e.HqManpowerPerMinute < 0 || e.HqMunitionsPerMinute < 0 || e.HqFuelPerMinute < 0)
            {
                errors.Add("economy HQ income must be >= 0.");
            }
        }

        private static HashSet<string> ValidateFactions(GameDataSet data, List<string> errors)
        {
            var ids = new HashSet<string>();
            foreach (var f in data.Factions)
            {
                if (string.IsNullOrWhiteSpace(f.Id))
                {
                    errors.Add("A faction has an empty id.");
                    continue;
                }

                if (!ids.Add(f.Id))
                {
                    errors.Add($"Duplicate faction id '{f.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(f.DisplayNameKey))
                {
                    errors.Add($"Faction '{f.Id}' has no displayNameKey.");
                }
            }

            return ids;
        }

        private static HashSet<string> ValidateWeapons(GameDataSet data, List<string> errors)
        {
            var ids = new HashSet<string>();
            foreach (var w in data.Weapons)
            {
                if (string.IsNullOrWhiteSpace(w.Id))
                {
                    errors.Add("A weapon has an empty id.");
                    continue;
                }

                if (!ids.Add(w.Id))
                {
                    errors.Add($"Duplicate weapon id '{w.Id}'.");
                }

                if (w.Range <= 0 || w.Cooldown <= 0 || w.Damage <= 0)
                {
                    errors.Add($"Weapon '{w.Id}' needs range, cooldown and damage > 0.");
                }

                if (!InUnit(w.AccuracyNear) || !InUnit(w.AccuracyMid) || !InUnit(w.AccuracyFar))
                {
                    errors.Add($"Weapon '{w.Id}' accuracy values must be within 0..1.");
                }

                if (w.ModelsHitVsInfantry < 1)
                {
                    errors.Add($"Weapon '{w.Id}' modelsHitVsInfantry must be >= 1.");
                }
            }

            return ids;
        }

        private static HashSet<string> ValidateUnits(GameDataSet data, HashSet<string> factionIds, HashSet<string> weaponIds, List<string> errors)
        {
            var ids = new HashSet<string>();
            foreach (var u in data.Units)
            {
                if (string.IsNullOrWhiteSpace(u.Id))
                {
                    errors.Add("A unit has an empty id.");
                    continue;
                }

                if (!ids.Add(u.Id))
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

                if (data.Weapons.Count > 0)
                {
                    if (u.Enabled && u.Weapons.Count == 0)
                    {
                        errors.Add($"Unit '{u.Id}' has no weapons.");
                    }

                    foreach (var w in u.Weapons)
                    {
                        if (!weaponIds.Contains(w.Weapon))
                        {
                            errors.Add($"Unit '{u.Id}' references unknown weapon '{w.Weapon}'.");
                        }

                        if (w.Count < 1)
                        {
                            errors.Add($"Unit '{u.Id}' weapon '{w.Weapon}' count must be >= 1.");
                        }
                    }
                }

                if (u.IsVehicle && u.ArmorFront <= 0)
                {
                    errors.Add($"Vehicle '{u.Id}' needs armorFront > 0.");
                }
            }

            return ids;
        }

        private static void ValidateStructures(GameDataSet data, HashSet<string> factionIds, HashSet<string> unitIds, List<string> errors)
        {
            var ids = new HashSet<string>();
            foreach (var s in data.Structures)
            {
                if (string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id))
                {
                    errors.Add($"Structure id '{s.Id}' is empty or duplicated.");
                    continue;
                }

                if (!s.IsShared && !factionIds.Contains(s.FactionId))
                {
                    errors.Add($"Structure '{s.Id}' references unknown faction '{s.FactionId}'.");
                }

                if (s.Health <= 0 || s.Footprint < 1)
                {
                    errors.Add($"Structure '{s.Id}' needs health > 0 and footprint >= 1.");
                }

                if (s.Kind == StructureKind.Sandbags && s.Length < 1)
                {
                    errors.Add($"Sandbags '{s.Id}' need length >= 1.");
                }

                if (s.Kind != StructureKind.Hq && s.BuildTime <= 0)
                {
                    errors.Add($"Structure '{s.Id}' needs buildTime > 0.");
                }

                foreach (var p in s.Produces)
                {
                    if (!unitIds.Contains(p))
                    {
                        errors.Add($"Structure '{s.Id}' produces unknown unit '{p}'.");
                    }
                    else if (data.GetUnit(p).FactionId != s.FactionId)
                    {
                        errors.Add($"Structure '{s.Id}' produces '{p}' from another faction.");
                    }
                }
            }

            foreach (var s in data.Structures)
            {
                foreach (var r in s.Requires)
                {
                    if (!ids.Contains(r))
                    {
                        errors.Add($"Structure '{s.Id}' requires unknown structure '{r}'.");
                    }
                }
            }
        }

        private static void ValidateFactionReferences(GameDataSet data, HashSet<string> unitIds, List<string> errors)
        {
            foreach (var f in data.Factions)
            {
                CheckUnitList(f.Id, "startingUnits", f.StartingUnits, unitIds, data, errors);
                CheckUnitList(f.Id, "sandboxUnits", f.SandboxUnits, unitIds, data, errors);

                if (data.Structures.Count == 0)
                {
                    continue;
                }

                if (!data.HasStructure(f.HqStructure))
                {
                    errors.Add($"Faction '{f.Id}' hqStructure '{f.HqStructure}' does not exist.");
                    continue;
                }

                var hq = data.GetStructure(f.HqStructure);
                if (hq.Kind != StructureKind.Hq || hq.FactionId != f.Id)
                {
                    errors.Add($"Faction '{f.Id}' hqStructure '{f.HqStructure}' must be an HQ of the same faction.");
                }

                bool hasEngineer = false;
                foreach (var id in hq.Produces)
                {
                    if (unitIds.Contains(id) && data.GetUnit(id).Engineer)
                    {
                        hasEngineer = true;
                    }
                }

                if (!hasEngineer)
                {
                    errors.Add($"Faction '{f.Id}' HQ must produce an engineer unit.");
                }

                // Every enabled unit must be producible by some structure of its faction.
                foreach (var u in data.UnitsOfFaction(f.Id))
                {
                    if (!u.Enabled)
                    {
                        continue;
                    }

                    bool producible = false;
                    foreach (var s in data.Structures)
                    {
                        if (s.FactionId == f.Id && s.Produces.Contains(u.Id))
                        {
                            producible = true;
                        }
                    }

                    if (!producible)
                    {
                        errors.Add($"Unit '{u.Id}' is enabled but no structure produces it.");
                    }
                }
            }
        }

        private static void ValidateMaps(GameDataSet data, List<string> errors)
        {
            foreach (var m in data.Maps)
            {
                if (m.Width <= 0 || m.Height <= 0 || m.CellSize <= 0)
                {
                    errors.Add($"Map '{m.Id}' has invalid dimensions.");
                    continue;
                }

                MapDefinition map;
                try
                {
                    map = MapFactory.FromDef(m, data.Rules);
                }
                catch (System.Exception ex)
                {
                    errors.Add($"Map '{m.Id}' failed to build: {ex.Message}");
                    continue;
                }

                if (map.HqPositions.Count < 2)
                {
                    errors.Add($"Map '{m.Id}' needs at least 2 start positions.");
                }

                foreach (var start in map.HqPositions)
                {
                    if (!map.Grid.IsWalkable(map.Grid.WorldToCell(start)))
                    {
                        errors.Add($"Map '{m.Id}' start {start} is blocked.");
                    }
                }

                int hqSectors = 0;
                int vps = 0;
                var sectorIds = new HashSet<string>();
                foreach (var s in map.Sectors)
                {
                    if (!sectorIds.Add(s.Id))
                    {
                        errors.Add($"Map '{m.Id}' has duplicate sector id '{s.Id}'.");
                    }

                    if (!map.Grid.IsWalkable(map.Grid.WorldToCell(new Vec2(s.X, s.Y))))
                    {
                        errors.Add($"Map '{m.Id}' sector '{s.Id}' point is blocked.");
                    }

                    if (s.Type == SectorType.Hq)
                    {
                        hqSectors++;
                    }

                    if (s.Type == SectorType.Victory)
                    {
                        vps++;
                    }
                }

                if (hqSectors < 2)
                {
                    errors.Add($"Map '{m.Id}' needs one HQ sector per player.");
                }

                if (vps == 0)
                {
                    errors.Add($"Map '{m.Id}' needs at least one victory point.");
                }
            }
        }

        private static void ValidateAi(GameDataSet data, List<string> errors)
        {
            if (data.Ai.Difficulties.Count == 0)
            {
                return;
            }

            foreach (var id in new[] { "easy", "normal", "hard" })
            {
                bool found = false;
                foreach (var d in data.Ai.Difficulties)
                {
                    found |= d.Id == id;
                }

                if (!found)
                {
                    errors.Add($"ai.json is missing difficulty '{id}'.");
                }
            }

            foreach (var plan in data.Ai.Factions)
            {
                if (!data.HasFaction(plan.FactionId))
                {
                    errors.Add($"AI plan references unknown faction '{plan.FactionId}'.");
                    continue;
                }

                foreach (var step in plan.Opening)
                {
                    bool isBuild = !string.IsNullOrEmpty(step.Build);
                    bool isProduce = !string.IsNullOrEmpty(step.Produce);
                    if (isBuild == isProduce)
                    {
                        errors.Add($"AI plan '{plan.FactionId}' has a step that must set exactly one of build/produce.");
                    }
                    else if (isBuild && !data.HasStructure(step.Build))
                    {
                        errors.Add($"AI plan '{plan.FactionId}' builds unknown structure '{step.Build}'.");
                    }
                    else if (isProduce && !data.HasUnit(step.Produce))
                    {
                        errors.Add($"AI plan '{plan.FactionId}' produces unknown unit '{step.Produce}'.");
                    }
                }

                foreach (var w in plan.Mix)
                {
                    if (!data.HasUnit(w.Unit) || data.GetUnit(w.Unit).FactionId != plan.FactionId)
                    {
                        errors.Add($"AI plan '{plan.FactionId}' mix uses invalid unit '{w.Unit}'.");
                    }
                }
            }

            foreach (var f in data.Factions)
            {
                if (data.GetAiPlan(f.Id) == null)
                {
                    errors.Add($"ai.json has no plan for faction '{f.Id}'.");
                }
            }
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

        private static bool InUnit(float v) => v >= 0f && v <= 1f;
    }
}
