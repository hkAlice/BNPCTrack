using System;
using System.Collections.Generic;
using System.Numerics;

public static class RdpAngled
{
    public static List<Vector3> Simplify(List<Vector3> points, float epsilon, float sharpAngleThresholdDeg = 150f)
    {
        if(points == null)
            throw new ArgumentNullException(nameof(points));
        if(epsilon <= 0)
            throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be positive");
        if(sharpAngleThresholdDeg < 0 || sharpAngleThresholdDeg > 180)
            throw new ArgumentOutOfRangeException(nameof(sharpAngleThresholdDeg), "Threshold must be between 0 and 180 degrees");

        if(points.Count < 3)
            return new List<Vector3>(points);

        // find all sharp turns/180 (we must keep these reverse points, possible edges)
        var mustKeep = FindSharpTurns(points, sharpAngleThresholdDeg);

        // keep first and last
        mustKeep.Add(0);
        mustKeep.Add(points.Count - 1);

        // Use HashSet for O(1) deduplication and lookup
        var resultSet = new HashSet<Vector3>(new Vector3Comparer());
        SimplifySection(points, 0, points.Count - 1, epsilon, resultSet, mustKeep);

        // Convert to list and ensure last point is included
        var result = new List<Vector3>(resultSet);
        if(!result.Contains(points[^1]))
            result.Add(points[^1]);

        // Sort to preserve original order
        result.Sort((a, b) => points.IndexOf(a).CompareTo(points.IndexOf(b)));

        return result;
    }

    private static void SimplifySection(List<Vector3> points, int start, int end, float epsilon, HashSet<Vector3> result, HashSet<int> mustKeep)
    {
        float maxDistance = 0;
        int index = -1;

        for(int i = start + 1; i < end; i++)
        {
            float dist = RdpSimplifier.PerpendicularDistance(points[i], points[start], points[end]);
            if(dist > maxDistance)
            {
                index = i;
                maxDistance = dist;
            }
        }

        // if we found a far-enough point OR one that must be preserved, split
        if(maxDistance > epsilon || (index >= 0 && mustKeep.Contains(index)))
        {
            SimplifySection(points, start, index, epsilon, result, mustKeep);
            SimplifySection(points, index, end, epsilon, result, mustKeep);
        }
        else
        {
            result.Add(points[start]);
        }
    }


    private static HashSet<int> FindSharpTurns(List<Vector3> points, float thresholdDeg)
    {
        var keep = new HashSet<int>();
        float thresholdRad = MathF.PI * thresholdDeg / 180f;

        for(int i = 1; i < points.Count - 1; i++)
        {
            Vector3 a = Vector3.Normalize(points[i] - points[i - 1]);
            Vector3 b = Vector3.Normalize(points[i + 1] - points[i]);

            if(a.LengthSquared() < 1e-6 || b.LengthSquared() < 1e-6)
                continue;

            float dot = Vector3.Dot(a, b);
            dot = Math.Clamp(dot, -1f, 1f);
            float angle = MathF.Acos(dot);

            // if angle is ~180, it's a sharp reverse
            if(angle > thresholdRad)
                keep.Add(i);
        }

        return keep;
    }

    private class Vector3Comparer : IEqualityComparer<Vector3>
    {
        private const float Epsilon = 1e-6f;

        public bool Equals(Vector3 a, Vector3 b)
        {
            return MathF.Abs(a.X - b.X) < Epsilon &&
                   MathF.Abs(a.Y - b.Y) < Epsilon &&
                   MathF.Abs(a.Z - b.Z) < Epsilon;
        }

        public int GetHashCode(Vector3 obj)
        {
            // Use a combined hash with reasonable precision
            int hashX = (int)(obj.X * 1000);
            int hashY = (int)(obj.Y * 1000);
            int hashZ = (int)(obj.Z * 1000);
            return HashCode.Combine(hashX, hashY, hashZ);
        }
    }
}
