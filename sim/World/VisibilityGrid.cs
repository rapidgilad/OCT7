using System;
using System.Collections.Generic;

namespace OCT7.Sim.World
{
    /// <summary>
    /// Per-player fog of war. Each update, every friendly unit reveals cells within its sight radius
    /// along precomputed rays that stop at line-of-sight blockers (tall buildings, rocks).
    /// <see cref="IsExplored"/> remembers cells seen at least once.
    /// </summary>
    public sealed class VisibilityGrid
    {
        private static readonly Dictionary<int, int[][]> RayCache = new Dictionary<int, int[][]>();
        private static readonly object CacheLock = new object();

        private readonly MapGrid _grid;
        private readonly int[][] _visibleStamp;
        private readonly bool[][] _explored;
        private readonly int[] _currentStamp;

        public VisibilityGrid(MapGrid grid, int playerCount)
        {
            _grid = grid;
            int n = grid.Width * grid.Height;
            _visibleStamp = new int[playerCount][];
            _explored = new bool[playerCount][];
            _currentStamp = new int[playerCount];
            for (int p = 0; p < playerCount; p++)
            {
                _visibleStamp[p] = new int[n];
                _explored[p] = new bool[n];
                _currentStamp[p] = 1; // cells start at stamp 0, so nothing is visible before the first Begin/Reveal
            }
        }

        public int PlayerCount => _currentStamp.Length;

        /// <summary>Starts a new visibility frame for a player (previously visible cells become hidden).</summary>
        public void Begin(int player) => _currentStamp[player]++;

        /// <summary>Reveals cells around <paramref name="pos"/> for <paramref name="player"/>.</summary>
        public void Reveal(int player, Vec2 pos, float radiusMeters)
        {
            var c = _grid.WorldToCell(pos);
            if (!_grid.InBounds(c))
            {
                return;
            }

            int stamp = _currentStamp[player];
            var visible = _visibleStamp[player];
            var explored = _explored[player];
            int w = _grid.Width;

            int origin = c.Y * w + c.X;
            visible[origin] = stamp;
            explored[origin] = true;

            float rCells = radiusMeters / _grid.CellSize;
            float r2 = rCells * rCells;
            foreach (var ray in GetRays((int)MathF.Ceiling(rCells)))
            {
                for (int i = 0; i < ray.Length; i += 2)
                {
                    int dx = ray[i];
                    int dy = ray[i + 1];
                    if (dx * dx + dy * dy > r2)
                    {
                        break;
                    }

                    int x = c.X + dx;
                    int y = c.Y + dy;
                    if (x < 0 || y < 0 || x >= w || y >= _grid.Height)
                    {
                        break;
                    }

                    int index = y * w + x;
                    visible[index] = stamp;
                    explored[index] = true;
                    if (_grid.BlocksLos(x, y))
                    {
                        break;
                    }
                }
            }
        }

        public bool IsVisible(int player, GridPos cell) =>
            _grid.InBounds(cell) && _visibleStamp[player][_grid.ToIndex(cell)] == _currentStamp[player];

        public bool IsVisible(int player, Vec2 pos) => IsVisible(player, _grid.WorldToCell(pos));

        public bool IsExplored(int player, GridPos cell) => _grid.InBounds(cell) && _explored[player][_grid.ToIndex(cell)];

        /// <summary>
        /// Direct line of sight between two points (Bresenham; blockers at either end don't count).
        /// Used per shot so a squad can't fire through a building even if a teammate sees the target.
        /// </summary>
        public static bool HasLineOfSight(MapGrid grid, Vec2 from, Vec2 to)
        {
            var a = grid.WorldToCell(from);
            var b = grid.WorldToCell(to);
            int x0 = a.X, y0 = a.Y, x1 = b.X, y1 = b.Y;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if (x0 == x1 && y0 == y1)
                {
                    return true;
                }

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }

                if ((x0 != x1 || y0 != y1) && grid.BlocksLos(x0, y0))
                {
                    return false;
                }
            }
        }

        /// <summary>Bresenham rays from the origin to every cell on the square perimeter of the given radius (offset pairs).</summary>
        private static int[][] GetRays(int radius)
        {
            lock (CacheLock)
            {
                if (RayCache.TryGetValue(radius, out var cached))
                {
                    return cached;
                }

                var rays = new List<int[]>();
                for (int i = -radius; i <= radius; i++)
                {
                    rays.Add(Line(i, -radius));
                    rays.Add(Line(i, radius));
                    if (i != -radius && i != radius)
                    {
                        rays.Add(Line(-radius, i));
                        rays.Add(Line(radius, i));
                    }
                }

                var result = rays.ToArray();
                RayCache[radius] = result;
                return result;
            }
        }

        private static int[] Line(int x1, int y1)
        {
            var points = new List<int>();
            int x0 = 0, y0 = 0;
            int dx = Math.Abs(x1), sx = x1 > 0 ? 1 : -1;
            int dy = -Math.Abs(y1), sy = y1 > 0 ? 1 : -1;
            int err = dx + dy;
            while (x0 != x1 || y0 != y1)
            {
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }

                points.Add(x0);
                points.Add(y0);
            }

            return points.ToArray();
        }
    }
}
