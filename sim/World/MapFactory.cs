using System;
using System.Collections.Generic;
using OCT7.Sim.Data;

namespace OCT7.Sim.World
{
    /// <summary>Builds runtime maps from data (game/data/maps/*.json), plus the sandbox test map.</summary>
    public static class MapFactory
    {
        /// <summary>Builds a map from its definition. Mirrored maps duplicate every non-center element through the map center.</summary>
        public static MapDefinition FromDef(MapDef def, RulesDef rules)
        {
            var grid = new MapGrid(def.Width, def.Height, def.CellSize);
            float losHeight = rules?.LosBlockHeight ?? 2.5f;

            var obstacles = new List<MapObstacle>();
            foreach (var o in def.Obstacles)
            {
                var obstacle = new MapObstacle(o.MinX, o.MinY, o.MaxX, o.MaxY, o.Height, o.Kind);
                obstacles.Add(obstacle);
                if (def.Mirror && !o.Center)
                {
                    obstacles.Add(Mirror(obstacle, def.Width, def.Height));
                }
            }

            foreach (var o in obstacles)
            {
                ApplyObstacle(grid, o, losHeight);
            }

            foreach (var z in def.GroundZones)
            {
                var type = ParseGround(z.Type);
                PaintGround(grid, z.MinX, z.MinY, z.MaxX, z.MaxY, type);
                if (def.Mirror && !z.Center)
                {
                    PaintGround(grid, def.Width - 1 - z.MaxX, def.Height - 1 - z.MaxY, def.Width - 1 - z.MinX, def.Height - 1 - z.MinY, type);
                }
            }

            var sectors = new List<SectorDef>();
            foreach (var s in def.Sectors)
            {
                sectors.Add(s);
                if (def.Mirror && !s.Center)
                {
                    sectors.Add(new SectorDef
                    {
                        Id = s.Id + "_m",
                        Type = s.Type,
                        X = grid.WorldWidth - s.X,
                        Y = grid.WorldHeight - s.Y,
                        Owner = s.Owner >= 0 ? 1 - s.Owner : -1,
                    });
                }
            }

            var starts = new List<Vec2>();
            foreach (var p in def.Starts)
            {
                starts.Add(new Vec2(p.X, p.Y));
            }

            if (def.Mirror && starts.Count == 1)
            {
                starts.Add(new Vec2(grid.WorldWidth - starts[0].X, grid.WorldHeight - starts[0].Y));
            }

            return new MapDefinition(def.Id, def.Name, grid, obstacles, starts, sectors);
        }

        /// <summary>
        /// 256 x 256 m sandbox (128 x 128 cells of 2 m) with building blocks placed in 180-degree rotational
        /// symmetry. No sectors; used by movement/pathing tests and the sandbox demo.
        /// </summary>
        public static MapDefinition CreateSandbox()
        {
            const int size = 128;
            var grid = new MapGrid(size, size, 2f);

            var half = new[]
            {
                new MapObstacle(58, 58, 69, 69, 14f),  // central block (mirrors onto itself)
                new MapObstacle(20, 44, 29, 51, 9f),
                new MapObstacle(44, 18, 51, 28, 10f),
                new MapObstacle(36, 72, 43, 85, 8f),
                new MapObstacle(72, 34, 85, 41, 8f),
                new MapObstacle(14, 86, 33, 87, 1.2f, ObstacleKind.Wall),
                new MapObstacle(52, 96, 57, 103, 11f),
                new MapObstacle(90, 8, 99, 15, 7f),
            };

            var obstacles = new List<MapObstacle>();
            foreach (var o in half)
            {
                obstacles.Add(o);
                var mirrored = Mirror(o, size, size);
                if (!SameRect(o, mirrored))
                {
                    obstacles.Add(mirrored);
                }
            }

            foreach (var o in obstacles)
            {
                ApplyObstacle(grid, o, 2.5f);
            }

            PaintGround(grid, 70, 4, 95, 25, GroundType.Rock);
            PaintGround(grid, size - 1 - 95, size - 1 - 25, size - 1 - 70, size - 1 - 4, GroundType.Rock);
            PaintGround(grid, 2, 60, 18, 80, GroundType.MudFarmland);
            PaintGround(grid, size - 1 - 18, size - 1 - 80, size - 1 - 2, size - 1 - 60, GroundType.MudFarmland);

            var hqs = new[] { new Vec2(24f, 24f), new Vec2(232f, 232f) };
            return new MapDefinition("sandbox", "Sandbox", grid, obstacles, hqs);
        }

        public static void ApplyObstacle(MapGrid grid, MapObstacle o, float losBlockHeight)
        {
            bool blocksLos = o.Height >= losBlockHeight && (o.Kind == ObstacleKind.Building || o.Kind == ObstacleKind.Rock);
            for (int y = o.MinY; y <= o.MaxY; y++)
            {
                for (int x = o.MinX; x <= o.MaxX; x++)
                {
                    var p = new GridPos(x, y);
                    if (o.BlocksMovement)
                    {
                        grid.SetBlocked(p, true);
                    }

                    if (blocksLos)
                    {
                        grid.SetLosBlocked(p, true);
                    }

                    grid.SetCoverSource(p, o.CoverSource);
                }
            }
        }

        private static MapObstacle Mirror(MapObstacle o, int width, int height) =>
            new MapObstacle(width - 1 - o.MaxX, height - 1 - o.MaxY, width - 1 - o.MinX, height - 1 - o.MinY, o.Height, o.Kind);

        private static bool SameRect(MapObstacle a, MapObstacle b) =>
            a.MinX == b.MinX && a.MinY == b.MinY && a.MaxX == b.MaxX && a.MaxY == b.MaxY;

        private static GroundType ParseGround(string type)
        {
            switch (type)
            {
                case "rock": return GroundType.Rock;
                case "mud":
                case "farmland":
                case "mudFarmland": return GroundType.MudFarmland;
                case "sand":
                case "urban":
                case "sandUrban": return GroundType.SandUrban;
                default: throw new ArgumentException($"Unknown ground type '{type}'.");
            }
        }

        private static void PaintGround(MapGrid grid, int minX, int minY, int maxX, int maxY, GroundType type)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    grid.SetGround(new GridPos(x, y), type);
                }
            }
        }
    }
}
