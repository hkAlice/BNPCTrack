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

    public static List<Cluster> Run(
        IList<Vector3> points,
        float epsilon,
        int minPoints)
    {
        var labels = new Dictionary<Vector3, int>();
        foreach(var p in points)
            labels[p] = (int)PointLabel.Unclassified;

        var clusters = new List<Cluster>();
        int clusterId = 0;

        foreach(var p in points)
        {
            if(labels[p] != (int)PointLabel.Unclassified)
                continue;

            if(ExpandCluster(points, labels, p, clusterId, epsilon, minPoints))
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
        IList<Vector3> points,
        Dictionary<Vector3, int> labels,
        Vector3 point,
        int clusterId,
        float epsilon,
        int minPoints)
    {
        var neighbors = RegionQuery(points, point, epsilon);
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
                var newNeighbors = RegionQuery(points, current, epsilon);
                if (newNeighbors.Count >= minPoints)
                    neighbors.AddRange(newNeighbors.Where(n => !neighbors.Contains(n)));
            }
            i++;
        }
        return true;
    }

    private static List<Vector3> RegionQuery(IList<Vector3> points, Vector3 point, float epsilon)
    {
        return points
            .Where(p => Vector3.Distance(p, point) <= epsilon)
            .ToList();
    }
}
