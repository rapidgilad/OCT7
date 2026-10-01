using System;
using System.Collections.Generic;
using OCT7.Sim.Pathfinding;
using OCT7.Sim.World;
using Xunit;

namespace OCT7.Sim.Tests
{
    public class PathfindingTests
    {
        private static MapGrid Grid(int w = 20, int h = 20) => new MapGrid(w, h, 2f);

        [Fact]
        public void StraightLine_OnOpenGrid()
        {
            var grid = Grid();
            var pf = new GridPathfinder(grid);
            var cells = new List<GridPos>();
            Assert.True(pf.FindCellPath(new GridPos(2, 5), new GridPos(12, 5), cells));
            Assert.Equal(11, cells.Count);
            Assert.All(cells, c => Assert.Equal(5, c.Y));
        }

        [Fact]
        public void RoutesAroundWall()
        {
            var grid = Grid();
            grid.BlockRect(10, 0, 10, 15); // wall with a gap at the top (y 16..19)
            var pf = new GridPathfinder(grid);
            var cells = new List<GridPos>();
            Assert.True(pf.FindCellPath(new GridPos(5, 5), new GridPos(15, 5), cells));
            Assert.All(cells, c => Assert.True(grid.IsWalkable(c)));
            Assert.Contains(cells, c => c.X == 10 && c.Y >= 16);
        }

        [Fact]
        public void UnreachableGoal_ReturnsFalse()
        {
            var grid = Grid();
            grid.BlockRect(10, 0, 10, 19); // full wall
            var pf = new GridPathfinder(grid);
            var cells = new List<GridPos>();
            Assert.False(pf.FindCellPath(new GridPos(5, 5), new GridPos(15, 5), cells));
            Assert.Empty(cells);

            var waypoints = new List<Vec2>();
            Assert.False(pf.FindPath(new Vec2(11f, 11f), new Vec2(31f, 11f), waypoints));
        }

        [Fact]
        public void NoDiagonalCornerCutting()
        {
            var grid = Grid(3, 3);
            // . X .
            // . . .   path from (0,0) to (1,1) may not squeeze diagonally past the X at (1,0)... check (0,1)->(1,0) style
            grid.SetBlocked(new GridPos(1, 0), true);
            grid.SetBlocked(new GridPos(0, 1), true);
            var pf = new GridPathfinder(grid);
            var cells = new List<GridPos>();
            // (0,0) is boxed in: its only exits are diagonal through two blocked corners.
            Assert.False(pf.FindCellPath(new GridPos(0, 0), new GridPos(2, 2), cells));
        }

        [Fact]
        public void PathIsDeterministic()
        {
            var map = MapFactory.CreateSandbox();
            var pf1 = new GridPathfinder(map.Grid);
            var pf2 = new GridPathfinder(map.Grid);
            var a = new List<Vec2>();
            var b = new List<Vec2>();
            Assert.True(pf1.FindPath(new Vec2(24f, 24f), new Vec2(232f, 232f), a));
            Assert.True(pf2.FindPath(new Vec2(24f, 24f), new Vec2(232f, 232f), b));
            Assert.Equal(a, b);
        }

        [Fact]
        public void SmoothedPath_StaysWalkable_AndEndsAtTarget()
        {
            var map = MapFactory.CreateSandbox();
            var pf = new GridPathfinder(map.Grid);
            var waypoints = new List<Vec2>();
            var start = new Vec2(24f, 24f);
            var target = new Vec2(200f, 140f);
            Assert.True(pf.FindPath(start, target, waypoints));
            Assert.Equal(target, waypoints[waypoints.Count - 1]);

            // Walk every segment in small steps and make sure no point is inside an obstacle.
            var prev = start;
            foreach (var wp in waypoints)
            {
                float len = Vec2.Distance(prev, wp);
                int steps = Math.Max(1, (int)(len / 0.25f));
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vec2.Lerp(prev, wp, i / (float)steps);
                    Assert.True(map.Grid.IsWalkable(map.Grid.WorldToCell(p)), $"Point {p} on segment {prev}->{wp} is blocked");
                }

                prev = wp;
            }

            // Smoothing should remove most of the raw cell-by-cell waypoints.
            Assert.True(waypoints.Count < 20, $"Expected a smoothed path, got {waypoints.Count} waypoints");
        }

        [Fact]
        public void BlockedTarget_UsesNearestWalkableCell()
        {
            var map = MapFactory.CreateSandbox();
            var pf = new GridPathfinder(map.Grid);
            var waypoints = new List<Vec2>();
            var insideCentralBlock = new Vec2(128f, 128f);
            Assert.True(pf.FindPath(new Vec2(24f, 24f), insideCentralBlock, waypoints));
            var end = waypoints[waypoints.Count - 1];
            Assert.True(map.Grid.IsWalkable(map.Grid.WorldToCell(end)));
            Assert.True(Vec2.Distance(end, insideCentralBlock) < 20f);
        }
    }
}
