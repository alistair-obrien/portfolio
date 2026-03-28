using System;
using System.Collections;
using System.Collections.Generic;

internal static class GridRaycaster
{
    internal static GridRay GetRayInDirection(
        Vec2 origin,
        Vec2 direction,
        int maxDistance)
    {
        if (maxDistance <= 0 || direction.sqrMagnitude < 0.0001f)
        {
            CellPosition tile = new(
                Mathf.FloorToInt(origin.x),
                Mathf.FloorToInt(origin.y)
            );

            return new GridRay(
                tile,
                tile,
                origin,
                origin,
                maxDistance,
                Array.Empty<GridRayStep>());
        }

        direction.Normalize();

        // Current tile
        int x = Mathf.FloorToInt(origin.x);
        int y = Mathf.FloorToInt(origin.y);

        int stepX = direction.x > 0 ? 1 : -1;
        int stepY = direction.y > 0 ? 1 : -1;

        float tDeltaX = direction.x != 0 ? Mathf.Abs(1f / direction.x) : float.PositiveInfinity;
        float tDeltaY = direction.y != 0 ? Mathf.Abs(1f / direction.y) : float.PositiveInfinity;

        float nextBoundaryX = stepX > 0 ? x + 1f : x;
        float nextBoundaryY = stepY > 0 ? y + 1f : y;

        float tMaxX = direction.x != 0
            ? (nextBoundaryX - origin.x) / direction.x
            : float.PositiveInfinity;

        float tMaxY = direction.y != 0
            ? (nextBoundaryY - origin.y) / direction.y
            : float.PositiveInfinity;

        var steps = new List<GridRayStep>();
        float travelled = 0f;
        int distanceTiles = 0;

        while (travelled < maxDistance)
        {
            if (tMaxX < tMaxY)
            {
                travelled = tMaxX;
                tMaxX += tDeltaX;
                x += stepX;
            }
            else
            {
                travelled = tMaxY;
                tMaxY += tDeltaY;
                y += stepY;
            }

            if (travelled > maxDistance)
                break;

            distanceTiles++;
            steps.Add(new GridRayStep(new CellPosition(x, y), distanceTiles));
        }

        Vec2 endPos = origin + direction * maxDistance;

        CellPosition startTile = new(
            Mathf.FloorToInt(origin.x),
            Mathf.FloorToInt(origin.y)
        );
        CellPosition endTile = steps.Count > 0 ? steps[^1].Cell : startTile;

        return new GridRay(
            startTile,
            endTile,
            origin,
            endPos,
            maxDistance,
            steps
        );
    }

    internal readonly struct GridRayStep
    {
        public readonly CellPosition Cell;
        public readonly int DistanceTiles;

        public GridRayStep(CellPosition cell, int distanceTiles)
        {
            Cell = cell;
            DistanceTiles = distanceTiles;
        }
    }

    internal sealed class GridRay : IEnumerable<GridRayStep>
    {
        public CellPosition StartTile { get; }
        public CellPosition EndTile { get; }
        public Vec2 StartPos { get; }
        public Vec2 EndPos { get; }
        public int MaxDistance { get; }

        private readonly IReadOnlyList<GridRayStep> _steps;

        internal GridRay(
            CellPosition startTile,
            CellPosition endTile,
            Vec2 startPos,
            Vec2 endPos,
            int maxDistance,
            IReadOnlyList<GridRayStep> steps)
        {
            StartTile = startTile;
            EndTile = endTile;
            StartPos = startPos;
            EndPos = endPos;
            MaxDistance = maxDistance;
            _steps = steps;
        }

        public IEnumerator<GridRayStep> GetEnumerator() => _steps.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
