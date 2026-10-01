using System;

namespace OCT7.Sim.World
{
    /// <summary>
    /// Per-cell cover derived from cover sources (buildings, rocks, walls, fences, sandbags).
    /// A walkable cell gets the best cover type among itself and its 8 neighbours, plus a normal pointing
    /// toward the cover. Cover only protects against attackers on the cover's side (docs/02 §3 Cover).
    /// </summary>
    public sealed class CoverGrid
    {
        private static readonly int[] Dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] Dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        private readonly MapGrid _grid;
        private readonly CoverType[] _cover;
        private readonly Vec2[] _normal;

        public CoverGrid(MapGrid grid)
        {
            _grid = grid;
            _cover = new CoverType[grid.Width * grid.Height];
            _normal = new Vec2[grid.Width * grid.Height];
            Rebuild();
        }

        public void Rebuild() => RebuildRegion(0, 0, _grid.Width - 1, _grid.Height - 1);

        /// <summary>Recomputes cover for the rectangle plus a one-cell border (call after obstacles/sandbags change).</summary>
        public void RebuildRegion(int minX, int minY, int maxX, int maxY)
        {
            minX = Math.Max(0, minX - 1);
            minY = Math.Max(0, minY - 1);
            maxX = Math.Min(_grid.Width - 1, maxX + 1);
            maxY = Math.Min(_grid.Height - 1, maxY + 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * _grid.Width + x;
                    if (!_grid.IsWalkable(x, y))
                    {
                        _cover[index] = CoverType.None;
                        _normal[index] = Vec2.Zero;
                        continue;
                    }

                    var best = _grid.GetCoverSource(x, y);
                    float nx = 0f, ny = 0f;
                    for (int d = 0; d < 8; d++)
                    {
                        var src = _grid.GetCoverSource(x + Dx[d], y + Dy[d]);
                        if (src == CoverType.None)
                        {
                            continue;
                        }

                        if (src > best)
                        {
                            best = src;
                        }

                        float w = src == CoverType.Heavy ? 2f : 1f;
                        nx += Dx[d] * w;
                        ny += Dy[d] * w;
                    }

                    _cover[index] = best;
                    var n = new Vec2(nx, ny);
                    _normal[index] = n.LengthSquared > 0.01f ? n.Normalized() : Vec2.Zero;
                }
            }
        }

        public CoverType GetCover(GridPos p) => _grid.InBounds(p) ? _cover[_grid.ToIndex(p)] : CoverType.None;

        public Vec2 GetNormal(GridPos p) => _grid.InBounds(p) ? _normal[_grid.ToIndex(p)] : Vec2.Zero;

        /// <summary>
        /// Cover the target at <paramref name="targetPos"/> has against fire from <paramref name="attackerPos"/>.
        /// Cover with no clear direction (surrounded, or standing on sandbags) protects from every side.
        /// </summary>
        public CoverType CoverAgainst(Vec2 targetPos, Vec2 attackerPos, float directionThreshold)
        {
            var cell = _grid.WorldToCell(targetPos);
            var cover = GetCover(cell);
            if (cover == CoverType.None)
            {
                return CoverType.None;
            }

            var normal = GetNormal(cell);
            if (normal == Vec2.Zero)
            {
                return cover;
            }

            var toAttacker = (attackerPos - targetPos).Normalized();
            float dot = normal.X * toAttacker.X + normal.Y * toAttacker.Y;
            return dot > directionThreshold ? cover : CoverType.None;
        }

        /// <summary>
        /// Best walkable cover cell within <paramref name="radius"/> meters of <paramref name="pos"/>
        /// (heavy beats light beats none; ties go to the closest, then lowest index). Returns false if none has cover.
        /// </summary>
        public bool TryFindCoverNear(Vec2 pos, float radius, out Vec2 coverPos)
        {
            var center = _grid.WorldToCell(pos);
            int r = (int)MathF.Ceiling(radius / _grid.CellSize);
            float r2 = radius * radius;
            var bestType = CoverType.None;
            float bestDist = float.MaxValue;
            coverPos = pos;

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var c = new GridPos(center.X + dx, center.Y + dy);
                    if (!_grid.IsWalkable(c))
                    {
                        continue;
                    }

                    var type = _cover[_grid.ToIndex(c)];
                    if (type == CoverType.None)
                    {
                        continue;
                    }

                    var p = _grid.CellCenter(c);
                    float d = Vec2.DistanceSquared(p, pos);
                    if (d > r2)
                    {
                        continue;
                    }

                    if (type > bestType || (type == bestType && d < bestDist))
                    {
                        bestType = type;
                        bestDist = d;
                        coverPos = p;
                    }
                }
            }

            return bestType != CoverType.None;
        }
    }
}
