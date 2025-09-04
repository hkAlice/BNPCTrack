using Serilog;
using System;
using System.Collections.Generic;
using System.Numerics;

public static class RdpAngled
{
    public static List<Vector3> Simplify(List<Vector3> points, float epsilon, float sharpAngleThresholdDeg = 150f)
    {
        if(points == null || points.Count < 3)
            return new List<Vector3>(points);

        // find all sharp turns/180 (we must keep these reverse points, possible edges)
        var mustKeep = FindSharpTurns(points, sharpAngleThresholdDeg);

        Log.Debug(string.Join(", ", mustKeep));

        // keep first and last
        mustKeep.Add(0);
        mustKeep.Add(points.Count - 1);

        var result = new List<Vector3>();
        SimplifySection(points, 0, points.Count - 1, epsilon, result, mustKeep);

        if(!result.Contains(points[^1]))
            result.Add(points[^1]);

        return result;
    }

    private static void SimplifySection(List<Vector3> points, int start, int end, float epsilon, List<Vector3> result, HashSet<int> mustKeep)
    {
        float maxDistance = 0;
        int index = -1;

        for(int i = start + 1; i < end; i++)
        {
            float dist = PerpendicularDistance(points[i], points[start], points[end]);
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
            if(!result.Contains(points[start]))
                result.Add(points[start]);
        }
    }

    private static float PerpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        if(lineStart == lineEnd)
            return Vector3.Distance(point, lineStart);

        Vector3 line = lineEnd - lineStart;
        Vector3 proj = lineStart + Vector3.Dot(point - lineStart, line) / line.LengthSquared() * line;
        return Vector3.Distance(point, proj);
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
}
