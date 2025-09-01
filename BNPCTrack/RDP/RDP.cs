using System;
using System.Collections.Generic;
using System.Numerics;

public static class RdpSimplifier
{
    public static List<Vector3> Simplify(List<Vector3> points, float epsilon)
    {
        if(points == null || points.Count < 3)
            return new List<Vector3>(points);

        var result = new List<Vector3>();
        SimplifySection(points, 0, points.Count - 1, epsilon, result);
        result.Add(points[points.Count - 1]); // ensure last point
        return result;
    }

    private static void SimplifySection(List<Vector3> points, int start, int end, float epsilon, List<Vector3> result)
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

        if(maxDistance > epsilon)
        {
            SimplifySection(points, start, index, epsilon, result);
            SimplifySection(points, index, end, epsilon, result);
        }
        else
        {
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
}
