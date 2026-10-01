using System.Collections.Generic;
using OCT7.Sim.Data;

namespace OCT7.Sim.World
{
    /// <summary>Inclusive rectangle of obstacle cells (building, rock, wall or fence). Used for pathing, cover, LOS and rendering.</summary>
    public readonly struct MapObstacle
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        /// <summary>Height in meters; tall buildings and rocks block line of sight.</summary>
        public readonly float Height;

        public readonly ObstacleKind Kind;

        public MapObstacle(int minX, int minY, int maxX, int maxY, float height, ObstacleKind kind = ObstacleKind.Building)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Height = height;
            Kind = kind;
        }

        public bool BlocksMovement => Kind != ObstacleKind.Fence;
        public CoverType CoverSource => Kind == ObstacleKind.Fence ? CoverType.Light : CoverType.Heavy;
    }

    /// <summary>A playable map: grid, obstacles, start positions and sectors (capture points).</summary>
    public sealed class MapDefinition
    {
        public MapDefinition(
            string id,
            string name,
            MapGrid grid,
            IReadOnlyList<MapObstacle> obstacles,
            IReadOnlyList<Vec2> hqPositions,
            IReadOnlyList<SectorDef> sectors = null)
        {
            Id = id;
            Name = name;
            Grid = grid;
            Obstacles = obstacles;
            HqPositions = hqPositions;
            Sectors = sectors ?? new List<SectorDef>();
        }

        public string Id { get; }
        public string Name { get; }
        public MapGrid Grid { get; }
        public IReadOnlyList<MapObstacle> Obstacles { get; }
        public IReadOnlyList<Vec2> HqPositions { get; }
        public IReadOnlyList<SectorDef> Sectors { get; }
    }
}
