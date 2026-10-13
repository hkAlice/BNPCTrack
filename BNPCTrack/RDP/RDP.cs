using System;
using System.Collections.Generic;
using System.Numerics;

public static class RdpSimplifier
{
    public static List<Vector3> Simplify(List<Vector3> points, float epsilon)
    {
        if(points == null)
            throw new ArgumentNullException(nameof(points));
        if(epsilon <= 0)
            throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be positive");

        if(points.Count < 3)
            return new List<Vector3>(points);

        // Use a bit array to mark which points to keep
        var keep = new bool[points.Count];
        keep[0] = true;                    // always keep first point
        keep[points.Count - 1] = true;     // always keep last point

        // Use explicit stack instead of recursion to avoid stack overflow
        var stack = new Stack<(int start, int end)>();
        stack.Push((0, points.Count - 1));

        while(stack.Count > 0)
        {
            var (start, end) = stack.Pop();

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
                // Mark the furthest point to keep
                keep[index] = true;

                // Process both segments
                stack.Push((index, end));
                stack.Push((start, index));
            }
        }

        // build result from marked points, preserving order
        var result = new List<Vector3>();
        for(int i = 0; i < points.Count; i++)
        {
            if(keep[i])
                result.Add(points[i]);
        }

        return result;
    }

    public static float PerpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        if(lineStart == lineEnd)
            return Vector3.Distance(point, lineStart);

        Vector3 line = lineEnd - lineStart;
        Vector3 proj = lineStart + Vector3.Dot(point - lineStart, line) / line.LengthSquared() * line;
        return Vector3.Distance(point, proj);
    }
}
