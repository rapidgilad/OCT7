using System.Collections.Generic;

namespace OCT7.Sim.World
{
    /// <summary>Builds maps. For now only the sandbox test map; real maps will load from data.</summary>
    public static class MapFactory
    {
        /// <summary>
        /// 256 x 256 m sandbox (128 x 128 cells of 2 m) with building blocks placed in 180-degree rotational
        /// symmetry, so both start positions are equally fair.
        /// </summary>
        public static MapDefinition CreateSandbox()
        {
            const int size = 128;
            var grid = new MapGrid(size, size, 2f);

            // One half of the layout; the other half is mirrored through the map center.
            var half = new[]
            {
                new MapObstacle(58, 58, 69, 69, 14f),  // central block (mirrors onto itself)
                new MapObstacle(20, 44, 29, 51, 9f),
                new MapObstacle(44, 18, 51, 28, 10f),
                new MapObstacle(36, 72, 43, 85, 8f),
                new MapObstacle(72, 34, 85, 41, 8f),
                new MapObstacle(14, 86, 33, 87, 3f),   // long low wall with open ends
                new MapObstacle(52, 96, 57, 103, 11f),
                new MapObstacle(90, 8, 99, 15, 7f),
            };

            var obstacles = new List<MapObstacle>();
            foreach (var o in half)
            {
                obstacles.Add(o);
                var mirrored = Mirror(o, size);
                if (!SameRect(o, mirrored))
                {
                    obstacles.Add(mirrored);
                }
            }

            foreach (var o in obstacles)
            {
                grid.BlockRect(o.MinX, o.MinY, o.MaxX, o.MaxY);
            }

            // Two mirrored rocky patches so ground types show up in the sandbox.
            PaintGround(grid, 70, 4, 95, 25, GroundType.Rock);
            PaintGround(grid, size - 1 - 95, size - 1 - 25, size - 1 - 70, size - 1 - 4, GroundType.Rock);
            PaintGround(grid, 2, 60, 18, 80, GroundType.MudFarmland);
            PaintGround(grid, size - 1 - 18, size - 1 - 80, size - 1 - 2, size - 1 - 60, GroundType.MudFarmland);

            var hqs = new[] { new Vec2(24f, 24f), new Vec2(232f, 232f) };
            return new MapDefinition("Sandbox", grid, obstacles, hqs);
        }

        private static MapObstacle Mirror(MapObstacle o, int size) =>
            new MapObstacle(size - 1 - o.MaxX, size - 1 - o.MaxY, size - 1 - o.MinX, size - 1 - o.MinY, o.Height);

        private static bool SameRect(MapObstacle a, MapObstacle b) =>
            a.MinX == b.MinX && a.MinY == b.MinY && a.MaxX == b.MaxX && a.MaxY == b.MaxY;

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
