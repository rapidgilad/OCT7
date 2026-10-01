using System;
using System.Collections.Generic;
using OCT7.Sim.Data;

namespace OCT7.Sim.Production
{
    /// <summary>A queued unit in a production structure.</summary>
    public sealed class ProductionItem
    {
        internal ProductionItem(UnitDef unit)
        {
            Unit = unit;
        }

        public UnitDef Unit { get; }
        public float Elapsed { get; internal set; }
        public float Progress => Unit.BuildTime > 0f ? Math.Min(1f, Elapsed / Unit.BuildTime) : 1f;
    }

    /// <summary>A placed structure: HQ, production building or sandbags. Occupies a rectangle of cells.</summary>
    public sealed class Structure
    {
        internal Structure(int id, int ownerId, StructureDef def, GridPos minCell, int sizeX, int sizeY, float cellSize)
        {
            Id = id;
            OwnerId = ownerId;
            Def = def;
            MinCell = minCell;
            SizeX = sizeX;
            SizeY = sizeY;
            CellSize = cellSize;
            Center = new Vec2((minCell.X + sizeX * 0.5f) * cellSize, (minCell.Y + sizeY * 0.5f) * cellSize);
            Health = def.Health;
        }

        public int Id { get; }
        public int OwnerId { get; }
        public StructureDef Def { get; }
        public GridPos MinCell { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public float CellSize { get; }
        public Vec2 Center { get; }

        public float Health { get; internal set; }
        public float MaxHealth => Def.Health;
        public float HealthFraction => MaxHealth > 0f ? Health / MaxHealth : 0f;

        /// <summary>0..1 construction progress. HQs start complete.</summary>
        public float BuildProgress { get; internal set; } = 1f;

        public bool IsComplete => BuildProgress >= 1f;
        public bool IsAlive => Health > 0f;
        public int LastDamagedTick { get; internal set; } = -100000;

        internal List<ProductionItem> QueueList { get; } = new List<ProductionItem>();
        public IReadOnlyList<ProductionItem> Queue => QueueList;

        public Vec2 RallyPoint { get; internal set; }
        public bool HasRallyPoint { get; internal set; }

        public bool ContainsCell(GridPos c) =>
            c.X >= MinCell.X && c.Y >= MinCell.Y && c.X < MinCell.X + SizeX && c.Y < MinCell.Y + SizeY;

        /// <summary>Distance from a point to the footprint rectangle (0 if inside).</summary>
        public float DistanceTo(Vec2 p)
        {
            float minX = MinCell.X * CellSize, minY = MinCell.Y * CellSize;
            float maxX = (MinCell.X + SizeX) * CellSize, maxY = (MinCell.Y + SizeY) * CellSize;
            float dx = Math.Max(Math.Max(minX - p.X, 0f), p.X - maxX);
            float dy = Math.Max(Math.Max(minY - p.Y, 0f), p.Y - maxY);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
