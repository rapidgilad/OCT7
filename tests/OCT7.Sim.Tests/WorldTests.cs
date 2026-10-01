using System;
using System.Linq;
using OCT7.Sim.Data;
using OCT7.Sim.World;
using OCT7.Tools;
using Xunit;

namespace OCT7.Sim.Tests
{
    public class DataPhase1Tests
    {
        [Fact]
        public void AllDataFiles_LoadAndValidate()
        {
            var data = RepoData.Load();
            var errors = GameDataValidator.Validate(data);
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
            Assert.NotEmpty(data.Weapons);
            Assert.NotEmpty(data.Structures);
            Assert.NotEmpty(data.Maps);
            Assert.Equal(3, data.Ai.Difficulties.Count);
        }

        [Theory]
        [InlineData("idf")]
        [InlineData("hamas")]
        [InlineData("hezbollah")]
        public void EachFaction_HasHq_Engineer_AndThreeTiers(string factionId)
        {
            var data = TestMatch.Data;
            var faction = data.GetFaction(factionId);
            var hq = data.GetStructure(faction.HqStructure);
            Assert.Equal(StructureKind.Hq, hq.Kind);
            Assert.Contains(hq.Produces, id => data.GetUnit(id).Engineer);

            var tiers = data.BuildableStructures(factionId).Where(s => s.Kind == StructureKind.Production).Select(s => s.Tier).ToList();
            Assert.Equal(new[] { 1, 2, 3 }, tiers.OrderBy(t => t).ToArray());
            Assert.Contains(data.BuildableStructures(factionId), s => s.Kind == StructureKind.Sandbags);
        }

        [Fact]
        public void EnabledUnits_AllHaveWeaponsThatExist()
        {
            var data = TestMatch.Data;
            foreach (var u in data.Units.Where(u => u.Enabled))
            {
                Assert.NotEmpty(u.Weapons);
                Assert.All(u.Weapons, w => Assert.True(data.HasWeapon(w.Weapon), $"{u.Id}: {w.Weapon}"));
            }
        }
    }

    public class MapTests
    {
        private static MapDefinition Ridge() => MapFactory.FromDef(TestMatch.Data.GetMap("ridge_outpost"), TestMatch.Data.Rules);

        [Fact]
        public void RidgeOutpost_IsRotationallySymmetric()
        {
            var map = Ridge();
            var g = map.Grid;
            for (int y = 0; y < g.Height; y++)
            {
                for (int x = 0; x < g.Width; x++)
                {
                    int mx = g.Width - 1 - x, my = g.Height - 1 - y;
                    Assert.Equal(g.IsWalkable(x, y), g.IsWalkable(mx, my));
                    Assert.Equal(g.BlocksLos(x, y), g.BlocksLos(mx, my));
                    Assert.Equal(g.GetCoverSource(x, y), g.GetCoverSource(mx, my));
                }
            }
        }

        [Fact]
        public void RidgeOutpost_HasMirroredSectorsAndStarts()
        {
            var map = Ridge();
            Assert.Equal(2, map.HqPositions.Count);
            Assert.Equal(new Vec2(270f, 270f), map.HqPositions[1]);
            Assert.Equal(3, map.Sectors.Count(s => s.Type == SectorType.Victory));
            Assert.Equal(2, map.Sectors.Count(s => s.Type == SectorType.Fuel));
            Assert.Equal(2, map.Sectors.Count(s => s.Type == SectorType.Munitions));
            var hqs = map.Sectors.Where(s => s.Type == SectorType.Hq).OrderBy(s => s.Owner).ToList();
            Assert.Equal(new[] { 0, 1 }, hqs.Select(s => s.Owner).ToArray());
        }

        [Fact]
        public void Fences_AreWalkable_WallsAndBuildingsAreNot()
        {
            var g = Ridge().Grid;
            Assert.True(g.IsWalkable(72, 66));                                  // fence
            Assert.Equal(CoverType.Light, g.GetCoverSource(72, 66));
            Assert.False(g.IsWalkable(63, 75));                                 // wall
            Assert.False(g.BlocksLos(63, 75));                                  // low wall: see over it
            Assert.False(g.IsWalkable(65, 67));                                 // building
            Assert.True(g.BlocksLos(65, 67));
        }

        [Fact]
        public void AllStartsAndSectorPoints_AreReachableFromEachOther()
        {
            var map = Ridge();
            var pf = new Pathfinding.GridPathfinder(map.Grid);
            var path = new System.Collections.Generic.List<Vec2>();
            foreach (var s in map.Sectors)
            {
                Assert.True(pf.FindPath(map.HqPositions[0], new Vec2(s.X, s.Y), path), $"Sector {s.Id} unreachable");
            }
        }
    }

    public class CoverTests
    {
        private static (MapGrid grid, CoverGrid cover) WallMap()
        {
            var grid = new MapGrid(20, 20, 2f);
            MapFactory.ApplyObstacle(grid, new MapObstacle(10, 0, 10, 19, 6f, ObstacleKind.Building), 2.5f); // vertical wall of building cells at x = 10
            MapFactory.ApplyObstacle(grid, new MapObstacle(3, 15, 6, 15, 1f, ObstacleKind.Fence), 2.5f);
            return (grid, new CoverGrid(grid));
        }

        [Fact]
        public void CellNextToBuilding_HasHeavyCover_FacingTheBuilding()
        {
            var (grid, cover) = WallMap();
            var cell = new GridPos(9, 5);
            Assert.Equal(CoverType.Heavy, cover.GetCover(cell));
            var n = cover.GetNormal(cell);
            Assert.True(n.X > 0.9f, $"normal should point +X toward the wall, got {n}");
            Assert.Equal(CoverType.None, cover.GetCover(new GridPos(5, 5)));
        }

        [Fact]
        public void Cover_OnlyProtects_AgainstAttackersBeyondIt()
        {
            var (grid, cover) = WallMap();
            var target = grid.CellCenter(new GridPos(9, 5));
            var attackerBeyondWall = new Vec2(30f, 11f);
            var attackerSameSide = new Vec2(2f, 11f);
            Assert.Equal(CoverType.Heavy, cover.CoverAgainst(target, attackerBeyondWall, 0.2f));
            Assert.Equal(CoverType.None, cover.CoverAgainst(target, attackerSameSide, 0.2f));
        }

        [Fact]
        public void Fence_GivesLightCover()
        {
            var (grid, cover) = WallMap();
            Assert.Equal(CoverType.Light, cover.GetCover(new GridPos(4, 14)));
            Assert.Equal(CoverType.Light, cover.GetCover(new GridPos(4, 15))); // standing on the fence line
        }

        [Fact]
        public void TryFindCoverNear_PicksHeavyCoverWithinRadius()
        {
            var (grid, cover) = WallMap();
            Assert.True(cover.TryFindCoverNear(new Vec2(16f, 11f), 4f, out var pos));
            Assert.Equal(CoverType.Heavy, cover.GetCover(grid.WorldToCell(pos)));
            Assert.False(cover.TryFindCoverNear(new Vec2(4f, 4f), 3f, out _));
        }

        [Fact]
        public void RebuildRegion_PicksUpNewCoverSources()
        {
            var grid = new MapGrid(10, 10, 2f);
            var cover = new CoverGrid(grid);
            Assert.Equal(CoverType.None, cover.GetCover(new GridPos(5, 4)));
            grid.SetCoverSource(new GridPos(5, 5), CoverType.Heavy); // e.g. sandbags placed
            cover.RebuildRegion(5, 5, 5, 5);
            Assert.Equal(CoverType.Heavy, cover.GetCover(new GridPos(5, 4)));
        }
    }

    public class VisibilityTests
    {
        [Fact]
        public void OpenGround_VisibleWithinRadiusOnly()
        {
            var grid = new MapGrid(40, 40, 2f);
            var vis = new VisibilityGrid(grid, 2);
            vis.Begin(0);
            vis.Reveal(0, new Vec2(40f, 40f), 20f);
            Assert.True(vis.IsVisible(0, new Vec2(55f, 40f)));
            Assert.False(vis.IsVisible(0, new Vec2(65f, 40f)));
            Assert.False(vis.IsVisible(1, new Vec2(40f, 40f)));
        }

        [Fact]
        public void TallBuilding_BlocksSightBehindIt_LowWallDoesNot()
        {
            var grid = new MapGrid(40, 40, 2f);
            MapFactory.ApplyObstacle(grid, new MapObstacle(22, 10, 22, 30, 8f, ObstacleKind.Building), 2.5f);
            MapFactory.ApplyObstacle(grid, new MapObstacle(10, 22, 18, 22, 1.2f, ObstacleKind.Wall), 2.5f);
            var vis = new VisibilityGrid(grid, 1);
            vis.Begin(0);
            vis.Reveal(0, new Vec2(30f, 40f), 30f);
            Assert.True(vis.IsVisible(0, grid.CellCenter(new GridPos(22, 20))));    // the building face itself
            Assert.False(vis.IsVisible(0, grid.CellCenter(new GridPos(26, 20))));   // directly behind it
            Assert.True(vis.IsVisible(0, grid.CellCenter(new GridPos(14, 24))));    // over the low wall
        }

        [Fact]
        public void Explored_PersistsAfterVisionMovesAway()
        {
            var grid = new MapGrid(40, 40, 2f);
            var vis = new VisibilityGrid(grid, 1);
            vis.Begin(0);
            vis.Reveal(0, new Vec2(10f, 10f), 10f);
            vis.Begin(0);
            vis.Reveal(0, new Vec2(70f, 70f), 10f);
            var cell = grid.WorldToCell(new Vec2(10f, 10f));
            Assert.False(vis.IsVisible(0, cell));
            Assert.True(vis.IsExplored(0, cell));
        }

        [Fact]
        public void LineOfSight_BlockedByTallBuildingBetween()
        {
            var grid = new MapGrid(40, 40, 2f);
            MapFactory.ApplyObstacle(grid, new MapObstacle(20, 0, 20, 39, 8f, ObstacleKind.Building), 2.5f);
            Assert.False(VisibilityGrid.HasLineOfSight(grid, new Vec2(20f, 20f), new Vec2(60f, 20f)));
            Assert.True(VisibilityGrid.HasLineOfSight(grid, new Vec2(20f, 20f), new Vec2(30f, 30f)));
        }
    }
}
