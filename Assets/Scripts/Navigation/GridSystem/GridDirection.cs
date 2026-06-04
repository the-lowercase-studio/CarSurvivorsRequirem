using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Navigation.GridSystem
{
    public class GridDirection
    {
        public readonly Vector2Int Vector;

        public static readonly GridDirection None = new GridDirection(0, 0);
        public static readonly GridDirection North = new GridDirection(0, 1);
        public static readonly GridDirection South = new GridDirection(0, -1);
        public static readonly GridDirection East = new GridDirection(1, 0);
        public static readonly GridDirection West = new GridDirection(-1, 0);
        public static readonly GridDirection NorthEast = new GridDirection(1, 1);
        public static readonly GridDirection NorthWest = new GridDirection(-1, 1);
        public static readonly GridDirection SouthEast = new GridDirection(1, -1);
        public static readonly GridDirection SouthWest = new GridDirection(-1, -1);

        private GridDirection(int x, int y)
        {
            Vector = new Vector2Int(x, y);
        }

        public static implicit operator Vector2Int(GridDirection direction)
        {
            return direction.Vector;
        }

        public static GridDirection GetDirectionFromV2I(Vector2Int vector)
        {
            if (vector == North.Vector)
            {
                return North;
            }
            if (vector == NorthEast.Vector)
            {
                return NorthEast;
            }
            if (vector == East.Vector)
            {
                return East;
            }
            if (vector == SouthEast.Vector)
            {
                return SouthEast;
            }
            if (vector == South.Vector)
            {
                return South;
            }
            if (vector == SouthWest.Vector)
            {
                return SouthWest;
            }
            if (vector == West.Vector)
            {
                return West;
            }
            if (vector == NorthWest.Vector)
            {
                return NorthWest;
            }

            return None;
        }

        public static readonly List<GridDirection> CardinalDirections = new List<GridDirection>
        {
            North,
            East,
            South,
            West
        };

        public static readonly List<GridDirection> CardinalAndIntercardinalDirections = new List<GridDirection>
        {
            North,
            NorthEast,
            East,
            SouthEast,
            South,
            SouthWest,
            West,
            NorthWest
        };

        public static readonly List<GridDirection> AllDirections = new List<GridDirection>
        {
            None,
            North,
            NorthEast,
            East,
            SouthEast,
            South,
            SouthWest,
            West,
            NorthWest
        };
    }
}
