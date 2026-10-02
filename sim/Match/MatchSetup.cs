using System.Collections.Generic;
using OCT7.Sim.Data;
using OCT7.Sim.Production;
using OCT7.Sim.World;

namespace OCT7.Sim.Match
{
    /// <summary>Creates ready-to-run matches. Any faction pairing works, including mirror matches.</summary>
    public static class MatchSetup
    {
        /// <summary>
        /// A real skirmish: map from data, HQ structures at the start positions, starting units, territory.
        /// <paramref name="mapId"/> null = first map in maps.json.
        /// </summary>
        public static Simulation CreateSkirmish(GameDataSet data, string faction0, string faction1, ulong seed, string mapId = null)
        {
            var mapDef = mapId != null ? data.GetMap(mapId) : data.Maps[0];
            var map = MapFactory.FromDef(mapDef, data.Rules);
            var sim = new Simulation(data, map, seed);
            var factions = new[] { faction0, faction1 };
            for (int i = 0; i < factions.Length; i++)
            {
                sim.AddPlayer(factions[i], map.HqPositions[i]);
            }

            for (int i = 0; i < factions.Length; i++)
            {
                var player = sim.Players[i];
                var faction = data.GetFaction(factions[i]);
                var hqDef = data.GetStructure(faction.HqStructure);
                sim.Production.PlaceComplete(player.Id, hqDef, map.Grid.WorldToCell(player.HqPosition));
                player.HadHq = true;
                SpawnForce(sim, player.Id, faction.StartingUnits, player.HqPosition, map, 14f);
            }

            sim.Start();
            return sim;
        }

        /// <summary>Sandbox map with each faction's demo force (<see cref="FactionDef.SandboxUnits"/>); no HQs or sectors.</summary>
        public static Simulation CreateSandboxMatch(GameDataSet data, string faction0, string faction1, ulong seed)
        {
            var map = MapFactory.CreateSandbox();
            var sim = new Simulation(data, map, seed);
            var factions = new[] { faction0, faction1 };
            for (int i = 0; i < factions.Length; i++)
            {
                var hq = map.HqPositions[i];
                var player = sim.AddPlayer(factions[i], hq);
                var faction = data.GetFaction(factions[i]);
                var units = faction.SandboxUnits.Count > 0 ? faction.SandboxUnits : faction.StartingUnits;
                SpawnForce(sim, player.Id, units, hq, map, 12f);
            }

            sim.Start();
            return sim;
        }

        /// <summary>
        /// Visual gallery for screenshots and art reviews: an empty map with one band per faction (player i = faction i).
        /// Each band has every structure of the faction (HQ, tiers and shared ones), completed, in a row, and every enabled
        /// unit in a row in front of them (+Y), facing +Y. A building, wall, fence and rock sit behind the first band (low Y).
        /// No sectors and no AI; nothing happens unless commands are sent.
        /// </summary>
        public static Simulation CreateShowcase(GameDataSet data, IReadOnlyList<string> factions)
        {
            const float cellSize = 2f;
            const float bandDepth = 76f; // > max sight range between neighbouring bands, so nobody fights
            const int width = 90;
            const float firstBand = 40f;
            int height = (int)((firstBand + bandDepth * (factions.Count - 1) + 46f) / cellSize);
            var grid = new MapGrid(width, height, cellSize);
            var obstacles = new List<MapObstacle>
            {
                new MapObstacle(8, 2, 15, 9, 9f),
                new MapObstacle(22, 6, 33, 6, 1.2f, ObstacleKind.Wall),
                new MapObstacle(40, 6, 51, 6, 1f, ObstacleKind.Fence),
                new MapObstacle(58, 3, 63, 8, 3f, ObstacleKind.Rock),
            };
            foreach (var o in obstacles)
            {
                MapFactory.ApplyObstacle(grid, o, 2.5f);
            }

            var hqs = new List<Vec2>();
            for (int i = 0; i < factions.Count; i++)
            {
                hqs.Add(new Vec2(20f, firstBand + i * bandDepth));
            }

            var map = new MapDefinition("showcase", "Showcase", grid, obstacles, hqs);
            var sim = new Simulation(data, map, 1);
            for (int i = 0; i < factions.Count; i++)
            {
                sim.AddPlayer(factions[i], hqs[i]);
            }

            for (int i = 0; i < factions.Count; i++)
            {
                float z = firstBand + i * bandDepth;
                var structures = new List<StructureDef> { data.GetStructure(data.GetFaction(factions[i]).HqStructure) };
                structures.AddRange(data.BuildableStructures(factions[i]));
                float x = 8f;
                foreach (var def in structures)
                {
                    ProductionSystem.FootprintSize(def, 0, out int sizeX, out _);
                    float half = sizeX * cellSize * 0.5f;
                    x += half;
                    sim.Production.PlaceComplete(i, def, grid.WorldToCell(new Vec2(x, z)));
                    x += half + 12f;
                }

                x = 10f;
                foreach (var unit in data.UnitsOfFaction(factions[i]))
                {
                    if (!unit.Enabled)
                    {
                        continue;
                    }

                    var squad = sim.World.SpawnSquad(i, unit, new Vec2(x, z + 24f));
                    squad.Facing = new Vec2(0f, 1f);
                    x += unit.IsVehicle ? 15f : 12f;
                }
            }

            sim.Start();
            return sim;
        }

        /// <summary>Spawns units in a grid in front of the HQ, facing the map center, on walkable cells only.</summary>
        public static void SpawnForce(Simulation sim, int playerId, IReadOnlyList<string> unitIds, Vec2 hq, MapDefinition map, float firstRowDistance)
        {
            var center = new Vec2(map.Grid.WorldWidth * 0.5f, map.Grid.WorldHeight * 0.5f);
            var forward = (center - hq).Normalized();
            var right = new Vec2(forward.Y, -forward.X);
            const int columns = 4;
            const float spacing = 7f;
            for (int i = 0; i < unitIds.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                var pos = hq + forward * (firstRowDistance + row * spacing) + right * ((col - (columns - 1) * 0.5f) * spacing);
                pos = map.Grid.ClampToWorld(pos);
                if (map.Grid.TryFindNearestWalkable(map.Grid.WorldToCell(pos), 8, out var cell) && map.Grid.WorldToCell(pos) != cell)
                {
                    pos = map.Grid.CellCenter(cell);
                }

                var squad = sim.World.SpawnSquad(playerId, sim.Data.GetUnit(unitIds[i]), pos);
                squad.Facing = forward;
            }
        }
    }
}
