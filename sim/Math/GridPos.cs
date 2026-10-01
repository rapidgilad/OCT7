using System;

namespace OCT7.Sim
{
    /// <summary>Integer cell coordinate on the map grid.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static bool operator ==(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public bool Equals(GridPos other) => this == other;
        public override bool Equals(object obj) => obj is GridPos other && this == other;
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"[{X},{Y}]";
    }
}
