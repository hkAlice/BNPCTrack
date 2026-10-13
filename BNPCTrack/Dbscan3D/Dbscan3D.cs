using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class Dbscan3D
{
    public enum PointLabel
    {
        Unclassified = 0,
        Noise = -1
    }
    public class Cluster
    {
        public int Id { get; }
        public List<Vector3> Points { get; set; }

        public Cluster(int id)
        {
            Id = id;
            Points = new List<Vector3>();
        }
    }

    private class SpatialGrid
    {
        private readonly float _cellSize;
        private readonly Dictionary<(int, int, int), List<Vector3>> _cells;

        public SpatialGrid(float cellSize)
        {
            _cellSize = cellSize;
            _cells = new Dictionary<(int, int, int), List<Vector3>>();
        }

        private (int, int, int) GetCellKey(Vector3 point)
        {
            return ((int)MathF.Floor(point.X / _cellSize),
                    (int)MathF.Floor(point.Y / _cellSize),
                    (int)MathF.Floor(point.Z / _cellSize));
        }

        public void Insert(Vector3 point)
        {
            var key = GetCellKey(point);
            if(!_cells.TryGetValue(key, out var cell))
            {
                cell = new List<Vector3>();
                _cells[key] = cell;
            }
            cell.Add(point);
        }

        public void InsertAll(IEnumerable<Vector3> points)
        {
            foreach(var p in points)
                Insert(p);
        }

        public List<Vector3> GetNeighborsInRange(Vector3 point, float epsilon)
        {
            var result = new List<Vector3>();
            int cellX = (int)MathF.Floor(point.X / _cellSize);
            int cellY = (int)MathF.Floor(point.Y / _cellSize);
            int cellZ = (int)MathF.Floor(point.Z / _cellSize);

            // Determine how many cells to check in each direction
            int cellsToCheck = (int)MathF.Ceiling(epsilon / _cellSize);

            for(int dx = -cellsToCheck; dx <= cellsToCheck; dx++)
            {
                for(int dy = -cellsToCheck; dy <= cellsToCheck; dy++)
                {
                    for(int dz = -cellsToCheck; dz <= cellsToCheck; dz++)
                    {
                        var key = (cellX + dx, cellY + dy, cellZ + dz);
                        if(_cells.TryGetValue(key, out var cell))
                        {
                            foreach(var p in cell)
                            {
                                if(Vector3.Distance(p, point) <= epsilon)
                                    result.Add(p);
                            }
                        }
                    }
                }
            }

            return result;
        }
    }

    public static List<Cluster> Run(
        IList<Vector3> points,
        float epsilon,
        int minPoints)
    {
        if(points == null)
            throw new ArgumentNullException(nameof(points));
        if(epsilon <= 0)
            throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be positive");
        if(minPoints < 1)
            throw new ArgumentOutOfRangeException(nameof(minPoints), "MinPoints must be at least 1");

        // handle empty or single point case
        if(points.Count == 0)
            return new List<Cluster>();
        if(points.Count == 1)
        {
            // single point is either noise or its own cluster depending on minPoints
            var cluster = new Cluster(0);
            if(minPoints <= 1)
                cluster.Points.Add(points[0]);
            return new List<Cluster> { cluster };
        }

        // build spatial index for efficient neighbor queries
        var grid = new SpatialGrid(epsilon);
        grid.InsertAll(points);

        var labels = new Dictionary<Vector3, int>();
        foreach(var p in points)
            labels[p] = (int)PointLabel.Unclassified;

        var clusters = new List<Cluster>();
        int clusterId = 0;

        foreach(var p in points)
        {
            if(labels[p] != (int)PointLabel.Unclassified)
                continue;

            if(ExpandCluster(grid, labels, p, clusterId, epsilon, minPoints))
            {
                var cluster = new Cluster(clusterId);
                foreach (var kv in labels.Where(kv => kv.Value == clusterId))
                    cluster.Points.Add(kv.Key);

                clusters.Add(cluster);
                clusterId++;
            }
            else
            {
                labels[p] = (int)PointLabel.Noise;
            }
        }

        return clusters;
    }

    private static bool ExpandCluster(
        SpatialGrid grid,
        Dictionary<Vector3, int> labels,
        Vector3 point,
        int clusterId,
        float epsilon,
        int minPoints)
    {
        var neighbors = grid.GetNeighborsInRange(point, epsilon);
        if(neighbors.Count < minPoints)
            return false;

        foreach(var n in neighbors)
            labels[n] = clusterId;

        var i = 0;
        while(i < neighbors.Count)
        {
            var current = neighbors[i];
            if(labels[current] == (int)PointLabel.Noise)
                labels[current] = clusterId;

            if(labels[current] == (int)PointLabel.Unclassified)
            {
                labels[current] = clusterId;
                var newNeighbors = grid.GetNeighborsInRange(current, epsilon);
                if(newNeighbors.Count >= minPoints)
                    neighbors.AddRange(newNeighbors.Where(n => !neighbors.Contains(n)));
            }
            i++;
        }
        return true;
    }
}
