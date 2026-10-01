using System;

namespace DungeonGuardians.Core
{
    [Serializable]
    public struct GridPoint
    {
        public int x;
        public int y;

        public GridPoint(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static GridPoint Left => new GridPoint(-1, 0);
        public static GridPoint Right => new GridPoint(1, 0);
        public static GridPoint Up => new GridPoint(0, 1);
        public static GridPoint Down => new GridPoint(0, -1);

        public static GridPoint operator +(GridPoint a, GridPoint b)
        {
            return new GridPoint(a.x + b.x, a.y + b.y);
        }

        public override bool Equals(object obj)
        {
            return obj is GridPoint point && x == point.x && y == point.y;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }

        public override string ToString()
        {
            return $"{x},{y}";
        }
    }
}
