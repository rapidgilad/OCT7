using System.Collections.Generic;

namespace OCT7.Sim.World
{
    /// <summary>Inclusive rectangle of blocked cells (a building or wall). Used for both pathing and rendering.</summary>
    public readonly struct MapObstacle
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        /// <summary>Visual height in meters (presentation only).</summary>
        public readonly float Height;

        public MapObstacle(int minX, int minY, int maxX, int maxY, float height)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Height = height;
        }
    }

    /// <summary>A playable map: grid, obstacles and player start positions.</summary>
    public sealed class MapDefinition
    {
        public MapDefinition(string name, MapGrid grid, IReadOnlyList<MapObstacle> obstacles, IReadOnlyList<Vec2> hqPositions)
        {
            Name = name;
            Grid = grid;
            Obstacles = obstacles;
            HqPositions = hqPositions;
        }

        public string Name { get; }
        public MapGrid Grid { get; }
        public IReadOnlyList<MapObstacle> Obstacles { get; }
        public IReadOnlyList<Vec2> HqPositions { get; }
    }
}
