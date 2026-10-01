using System;

namespace OCT7.Sim.World
{
    /// <summary>Ground type per cell. Drives tunnel/bunker costs and vehicle speed (docs/02-core-gameplay.md).</summary>
    public enum GroundType : byte
    {
        SandUrban = 0,
        Rock = 1,
        MudFarmland = 2,
    }

    /// <summary>Cover quality, both as a property of obstacle cells (source) and of the cells next to them.</summary>
    public enum CoverType : byte
    {
        None = 0,
        Light = 1,
        Heavy = 2,
    }

    /// <summary>
    /// Walkability, line-of-sight, cover-source and ground-type grid. Cell (0,0) covers world [0, CellSize) on both axes.
    /// </summary>
    public sealed class MapGrid
    {
        private readonly bool[] _blocked;
        private readonly bool[] _losBlocked;
        private readonly CoverType[] _coverSource;
        private readonly GroundType[] _ground;

        public MapGrid(int width, int height, float cellSize)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be positive.");
            }

            Width = width;
            Height = height;
            CellSize = cellSize;
            _blocked = new bool[width * height];
            _losBlocked = new bool[width * height];
            _coverSource = new CoverType[width * height];
            _ground = new GroundType[width * height];
        }

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public float WorldWidth => Width * CellSize;
        public float WorldHeight => Height * CellSize;

        public int ToIndex(GridPos p) => p.Y * Width + p.X;
        public GridPos FromIndex(int index) => new GridPos(index % Width, index / Width);

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public bool IsWalkable(GridPos p) => InBounds(p) && !_blocked[ToIndex(p)];

        public bool IsWalkable(int x, int y) => IsWalkable(new GridPos(x, y));

        public void SetBlocked(GridPos p, bool blocked)
        {
            if (InBounds(p))
            {
                _blocked[ToIndex(p)] = blocked;
            }
        }

        public bool BlocksLos(int x, int y) => InBounds(new GridPos(x, y)) && _losBlocked[y * Width + x];

        public void SetLosBlocked(GridPos p, bool blocked)
        {
            if (InBounds(p))
            {
                _losBlocked[ToIndex(p)] = blocked;
            }
        }

        public CoverType GetCoverSource(int x, int y) => InBounds(new GridPos(x, y)) ? _coverSource[y * Width + x] : CoverType.None;

        public void SetCoverSource(GridPos p, CoverType type)
        {
            if (InBounds(p))
            {
                _coverSource[ToIndex(p)] = type;
            }
        }

        /// <summary>Blocks every cell in the inclusive rectangle.</summary>
        public void BlockRect(int minX, int minY, int maxX, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    SetBlocked(new GridPos(x, y), true);
                }
            }
        }

        public GroundType GetGround(GridPos p) => InBounds(p) ? _ground[ToIndex(p)] : GroundType.SandUrban;

        public void SetGround(GridPos p, GroundType type)
        {
            if (InBounds(p))
            {
                _ground[ToIndex(p)] = type;
            }
        }

        public GridPos WorldToCell(Vec2 world)
        {
            int x = (int)MathF.Floor(world.X / CellSize);
            int y = (int)MathF.Floor(world.Y / CellSize);
            return new GridPos(x, y);
        }

        public Vec2 CellCenter(GridPos p) => new Vec2((p.X + 0.5f) * CellSize, (p.Y + 0.5f) * CellSize);

        public GridPos ClampToBounds(GridPos p) =>
            new GridPos(Math.Clamp(p.X, 0, Width - 1), Math.Clamp(p.Y, 0, Height - 1));

        public Vec2 ClampToWorld(Vec2 world)
        {
            const float margin = 0.01f;
            return new Vec2(
                Math.Clamp(world.X, 0f, WorldWidth - margin),
                Math.Clamp(world.Y, 0f, WorldHeight - margin));
        }

        /// <summary>
        /// Finds the nearest walkable cell by searching square rings of growing radius.
        /// Scan order is fixed, so the result is deterministic.
        /// </summary>
        public bool TryFindNearestWalkable(GridPos from, int maxRadius, out GridPos result)
        {
            from = ClampToBounds(from);
            if (IsWalkable(from))
            {
                result = from;
                return true;
            }

            for (int r = 1; r <= maxRadius; r++)
            {
                bool found = false;
                int bestDist = int.MaxValue;
                GridPos best = default;
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                        {
                            continue;
                        }

                        var p = new GridPos(from.X + dx, from.Y + dy);
                        if (!IsWalkable(p))
                        {
                            continue;
                        }

                        int d = dx * dx + dy * dy;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = p;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    result = best;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
