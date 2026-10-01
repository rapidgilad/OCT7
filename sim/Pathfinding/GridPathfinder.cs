using System;
using System.Collections.Generic;
using OCT7.Sim.World;

namespace OCT7.Sim.Pathfinding
{
    /// <summary>
    /// 8-directional A* on the map grid with an octile heuristic.
    /// Deterministic: fixed neighbour order and tie-breaking on (f, h, cell index).
    /// Diagonal moves may not cut corners. Results are smoothed with a walkability line-of-sight check.
    /// </summary>
    public sealed class GridPathfinder
    {
        private const int StraightCost = 10;
        private const int DiagonalCost = 14;
        private const int MaxNearestSearchRadius = 12;

        private static readonly int[] DirX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DirY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private readonly MapGrid _map;
        private readonly int[] _g;
        private readonly int[] _parent;
        private readonly int[] _visitStamp;
        private readonly int[] _closedStamp;
        private readonly MinHeap _open = new MinHeap();
        private readonly List<GridPos> _cellPath = new List<GridPos>();
        private int _stamp;

        public GridPathfinder(MapGrid map)
        {
            _map = map;
            int n = map.Width * map.Height;
            _g = new int[n];
            _parent = new int[n];
            _visitStamp = new int[n];
            _closedStamp = new int[n];
        }

        /// <summary>Finds a cell path from start to goal (both inclusive). Returns false if unreachable.</summary>
        public bool FindCellPath(GridPos start, GridPos goal, List<GridPos> result)
        {
            result.Clear();
            if (!_map.IsWalkable(start) || !_map.IsWalkable(goal))
            {
                return false;
            }

            if (start == goal)
            {
                result.Add(start);
                return true;
            }

            _stamp++;
            _open.Clear();

            int startIndex = _map.ToIndex(start);
            int goalIndex = _map.ToIndex(goal);
            _g[startIndex] = 0;
            _parent[startIndex] = -1;
            _visitStamp[startIndex] = _stamp;
            int h0 = Heuristic(start, goal);
            _open.Push(new HeapEntry(h0, h0, startIndex));

            while (_open.Count > 0)
            {
                var current = _open.Pop();
                int ci = current.Index;
                if (_closedStamp[ci] == _stamp)
                {
                    continue; // stale entry
                }

                _closedStamp[ci] = _stamp;
                if (ci == goalIndex)
                {
                    Reconstruct(goalIndex, result);
                    return true;
                }

                var cp = _map.FromIndex(ci);
                for (int d = 0; d < 8; d++)
                {
                    int nx = cp.X + DirX[d];
                    int ny = cp.Y + DirY[d];
                    if (!_map.IsWalkable(nx, ny))
                    {
                        continue;
                    }

                    bool diagonal = d >= 4;
                    if (diagonal && (!_map.IsWalkable(cp.X + DirX[d], cp.Y) || !_map.IsWalkable(cp.X, cp.Y + DirY[d])))
                    {
                        continue; // no corner cutting
                    }

                    var np = new GridPos(nx, ny);
                    int ni = _map.ToIndex(np);
                    if (_closedStamp[ni] == _stamp)
                    {
                        continue;
                    }

                    int tentative = _g[ci] + (diagonal ? DiagonalCost : StraightCost);
                    if (_visitStamp[ni] == _stamp && tentative >= _g[ni])
                    {
                        continue;
                    }

                    _visitStamp[ni] = _stamp;
                    _g[ni] = tentative;
                    _parent[ni] = ci;
                    int h = Heuristic(np, goal);
                    _open.Push(new HeapEntry(tentative + h, h, ni));
                }
            }

            return false;
        }

        /// <summary>
        /// World-space path from <paramref name="from"/> to <paramref name="to"/> as smoothed waypoints (excluding the start).
        /// If the target is blocked, the nearest walkable cell is used. Returns false if no path exists.
        /// </summary>
        public bool FindPath(Vec2 from, Vec2 to, List<Vec2> waypoints)
        {
            waypoints.Clear();
            to = _map.ClampToWorld(to);

            if (!_map.TryFindNearestWalkable(_map.WorldToCell(from), MaxNearestSearchRadius, out var startCell))
            {
                return false;
            }

            var requestedGoal = _map.WorldToCell(to);
            if (!_map.TryFindNearestWalkable(requestedGoal, MaxNearestSearchRadius, out var goalCell))
            {
                return false;
            }

            if (!FindCellPath(startCell, goalCell, _cellPath))
            {
                return false;
            }

            Vec2 finalPoint = goalCell == requestedGoal ? to : _map.CellCenter(goalCell);

            // Raw waypoints: cell centers after the start cell, then the exact final point.
            var raw = new List<Vec2>(_cellPath.Count + 1) { from };
            for (int i = 1; i < _cellPath.Count - 1; i++)
            {
                raw.Add(_map.CellCenter(_cellPath[i]));
            }

            raw.Add(finalPoint);
            Smooth(raw, waypoints);
            return true;
        }

        /// <summary>
        /// True if a point walking in a straight line from a to b only crosses walkable cells
        /// (both cells are checked when the line passes exactly through a corner).
        /// </summary>
        public bool HasWalkableLine(Vec2 a, Vec2 b)
        {
            float cs = _map.CellSize;
            float ax = a.X / cs, ay = a.Y / cs, bx = b.X / cs, by = b.Y / cs;
            int x = (int)MathF.Floor(ax), y = (int)MathF.Floor(ay);
            int endX = (int)MathF.Floor(bx), endY = (int)MathF.Floor(by);

            if (!_map.IsWalkable(x, y))
            {
                return false;
            }

            float dx = bx - ax, dy = by - ay;
            int stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);
            float tDeltaX = stepX != 0 ? MathF.Abs(1f / dx) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? MathF.Abs(1f / dy) : float.PositiveInfinity;
            float tMaxX = stepX > 0 ? (x + 1 - ax) * tDeltaX : (stepX < 0 ? (ax - x) * tDeltaX : float.PositiveInfinity);
            float tMaxY = stepY > 0 ? (y + 1 - ay) * tDeltaY : (stepY < 0 ? (ay - y) * tDeltaY : float.PositiveInfinity);

            int guard = _map.Width + _map.Height + 4;
            while ((x != endX || y != endY) && guard-- > 0)
            {
                if (MathF.Abs(tMaxX - tMaxY) < 1e-5f)
                {
                    // Passing through a corner: both side cells must be walkable.
                    if (!_map.IsWalkable(x + stepX, y) || !_map.IsWalkable(x, y + stepY))
                    {
                        return false;
                    }

                    x += stepX;
                    y += stepY;
                    tMaxX += tDeltaX;
                    tMaxY += tDeltaY;
                }
                else if (tMaxX < tMaxY)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }

                if (!_map.IsWalkable(x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private void Smooth(List<Vec2> raw, List<Vec2> output)
        {
            int anchor = 0;
            while (anchor < raw.Count - 1)
            {
                int next = anchor + 1;
                for (int candidate = raw.Count - 1; candidate > anchor + 1; candidate--)
                {
                    if (HasWalkableLine(raw[anchor], raw[candidate]))
                    {
                        next = candidate;
                        break;
                    }
                }

                output.Add(raw[next]);
                anchor = next;
            }
        }

        private void Reconstruct(int goalIndex, List<GridPos> result)
        {
            for (int i = goalIndex; i != -1; i = _parent[i])
            {
                result.Add(_map.FromIndex(i));
            }

            result.Reverse();
        }

        private static int Heuristic(GridPos a, GridPos b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return StraightCost * (dx + dy) + (DiagonalCost - 2 * StraightCost) * Math.Min(dx, dy);
        }

        private readonly struct HeapEntry
        {
            public readonly int F;
            public readonly int H;
            public readonly int Index;

            public HeapEntry(int f, int h, int index)
            {
                F = f;
                H = h;
                Index = index;
            }

            public bool LessThan(HeapEntry other)
            {
                if (F != other.F)
                {
                    return F < other.F;
                }

                if (H != other.H)
                {
                    return H < other.H;
                }

                return Index < other.Index;
            }
        }

        private sealed class MinHeap
        {
            private readonly List<HeapEntry> _items = new List<HeapEntry>();

            public int Count => _items.Count;

            public void Clear() => _items.Clear();

            public void Push(HeapEntry entry)
            {
                _items.Add(entry);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (!_items[i].LessThan(_items[parent]))
                    {
                        break;
                    }

                    (_items[i], _items[parent]) = (_items[parent], _items[i]);
                    i = parent;
                }
            }

            public HeapEntry Pop()
            {
                var top = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1;
                    int right = left + 1;
                    int smallest = i;
                    if (left < _items.Count && _items[left].LessThan(_items[smallest]))
                    {
                        smallest = left;
                    }

                    if (right < _items.Count && _items[right].LessThan(_items[smallest]))
                    {
                        smallest = right;
                    }

                    if (smallest == i)
                    {
                        break;
                    }

                    (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
                    i = smallest;
                }

                return top;
            }
        }
    }
}
