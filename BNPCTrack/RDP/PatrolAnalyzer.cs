using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
public class PatrolSegment
{
    public List<Vector3> Points;
    public bool IsLoop;
    public bool IsReverse;
}

public static class PatrolAnalyzer
{
    public static List<int> FindSharpRotations(List<float> rotations, float threshold = 150f)
    {
        List<int> reversalIndices = new List<int>();
        for(int i = 1; i < rotations.Count; i++)
        {
            float delta = DeltaAngle(rotations[i - 1], rotations[i]);
            if(MathF.Abs(delta) >= threshold)
                reversalIndices.Add(i);
        }
        return reversalIndices;
    }

    // check if patrol loops or reverses/ping-pongs
    public static bool IsLoop(List<Vector3> points, float loopTolerance = 0.15f)
    {
        if(points.Count < 2)
            return false;
        return Vector3.Distance(points[0], points[^1]) <= loopTolerance;
    }
    public static bool IsReverse(List<Vector3> points, float loopTolerance = 0.15f)
    {
        if(points.Count < 2)
            return false;
        int mid = points.Count / 2;
        for(int i = 0; i < mid; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[points.Count - 1 - i];
            if(Vector3.Distance(a, b) > loopTolerance)
                return false;
        }
        return true;
    }

    public static List<Vector3> TrimToFirstCycle(List<Vector3> points, float loopTolerance = 1.0f, int minLoopLength = 15)
    {
        if(points.Count < minLoopLength * 2)
            return points;

        var distances = points.Select(p => Vector3.Distance(p, points[0])).ToList();
        
        int bestEnd = -1;
        float minDist = float.MaxValue;
        
        for(int i = minLoopLength; i < distances.Count - minLoopLength; i++)
        {
            if(distances[i] < loopTolerance &&
                distances[i] < distances[i-1] &&
                distances[i] < distances[i+1])
            {
                if(distances[i] < minDist)
                {
                    minDist = distances[i];
                    bestEnd = i;
                }
            }
        }

        if(bestEnd >= minLoopLength)
        {
            var loopPoints = points.Take(bestEnd + 1).ToList();
            loopPoints.Add(points[0]);
            return loopPoints;
        }
        
        return points;
    }

    public static List<Vector3> TrimToFullLoop(List<Vector3> points, float loopTolerance = 1f, int minLoopLength = 5)
    {
        if(points.Count < minLoopLength * 2)
            return points;

        for(int i = minLoopLength; i < points.Count; i++)
        {
            if(Vector3.Distance(points[0], points[i]) <= loopTolerance)
            {
                // full loop found
                var loopPoints = points.Take(i + 1).ToList();
                loopPoints.Add(points[0]); // close the loop
                return loopPoints;
            }
        }

        return points; // no loop
    }

    public static List<Vector3> RotateStart(List<Vector3> points)
    {
        int bestStartIndex = 0;
        float minDistanceSum = float.MaxValue;

        for(int i = 0; i < points.Count; i++)
        {
            float distanceSum = 0f;
            for(int j = 0; j < points.Count; j++)
            {
                Vector3 a = points[(i + j) % points.Count];
                Vector3 b = points[(i + j + 1) % points.Count];
                distanceSum += Vector3.Distance(a, b);
            }

            if(distanceSum < minDistanceSum)
            {
                minDistanceSum = distanceSum;
                bestStartIndex = i;
            }
        }

        return points.Skip(bestStartIndex).Concat(points.Take(bestStartIndex)).ToList();
    }

    // get segment similarity
    public static List<Vector3> ExtractCycleGreedy(
        List<Vector3> points,
        List<float>? rotations = null,
        int windowSize = 5,
        float similarityThreshold = 0.92f,
        float rotationWeight = 0.1f,
        float extendThreshold = 0.85f,
        float minSegmentLength = 1f)
    {
        if(points.Count < windowSize * 2)
            return points;

        // build segments
        List<Vector3> segments = new List<Vector3>();
        for(int i = 0; i < points.Count - 1; i++)
            segments.Add(points[i + 1] - points[i]);

        int loopStart = -1;
        int loopEnd = -1;

        int startSearch = Math.Min(windowSize, points.Count / 4);

        for(int i = startSearch; i < segments.Count - windowSize; i++)
        {
            for(int j = i + windowSize; j < segments.Count - windowSize; j++)
            {
                float avgSim = 0f;
                int count = 0;
                    
                // i need more letters
                for(int k = 0; k < windowSize; k++)
                {
                    Vector3 segA = segments[i + k];
                    Vector3 segB = segments[j + k];

                    // skip very short segments
                    if(segA.Length() < minSegmentLength || segB.Length() < minSegmentLength)
                        continue;

                    float posSim = SegmentSimilarity(segA, segB);
                    float rotSim = 1f;

                    if(rotations != null && rotations.Count == points.Count)
                        rotSim = 1f - MathF.Abs(DeltaAngle(rotations[i + k], rotations[j + k])) / 180f;

                    avgSim += posSim * (1f - rotationWeight) + rotSim * rotationWeight;
                    count++;
                }

                if(count == 0)
                    continue;

                avgSim /= count;

                if(avgSim >= similarityThreshold)
                {
                    // greedy extension
                    int extend = 0;
                    while(i + windowSize + extend < j + windowSize && j + windowSize + extend < segments.Count)
                    {
                        Vector3 segA = segments[i + windowSize + extend];
                        Vector3 segB = segments[j + windowSize + extend];

                        if(segA.Length() < minSegmentLength || segB.Length() < minSegmentLength)
                            break;

                        float posSimNext = SegmentSimilarity(segA, segB);
                        float rotSimNext = 1f;
                        if(rotations != null && rotations.Count == points.Count)
                            rotSimNext = 1f - MathF.Abs(DeltaAngle(rotations[i + windowSize + extend], rotations[j + windowSize + extend])) / 180f;

                        float simNext = posSimNext * (1f - rotationWeight) + rotSimNext * rotationWeight;
                        if(simNext < extendThreshold)
                            break;

                        extend++;
                    }

                    loopStart = i;
                    loopEnd = j + windowSize + extend;
                    break;
                }
            }
            if(loopStart != -1)
                break;
        }

        if(loopStart == -1)
            return points; // fallback

        return points.GetRange(loopStart, loopEnd - loopStart + 1);
    }

    public static List<List<Vector3>> SplitPathAtIndices(List<Vector3> points, List<int> splitIndices)
    {
        List<List<Vector3>> segments = new List<List<Vector3>>();
        int start = 0;
        foreach(int idx in splitIndices)
        {
            if(idx > start)
            {
                segments.Add(points.GetRange(start, idx - start));
                start = idx;
            }
        }
        if(start < points.Count)
            segments.Add(points.GetRange(start, points.Count - start));

        return segments;
    }

    public static List<PatrolSegment> AnalyzePath(List<Vector3> points, List<float> rotations, float rotationThreshold = 150f, float loopTolerance = 0.3f, int minLoopLength = 5)
    {
        List<int> sharpRotationIndices = FindSharpRotations(rotations, rotationThreshold);
        var rawSegments = SplitPathAtIndices(points, sharpRotationIndices);

        List<PatrolSegment> result = new List<PatrolSegment>();

        foreach(var seg in rawSegments)
        {
            var trimmed = TrimToFullLoop(seg, loopTolerance, minLoopLength);
            if(IsLoop(trimmed, loopTolerance))
                trimmed = RotateStart(trimmed);

            result.Add(new PatrolSegment
            {
                Points = trimmed,
                IsLoop = IsLoop(trimmed, loopTolerance),
                IsReverse = IsReverse(trimmed, loopTolerance)
            });
        }

        return result;
    }

    private static float SegmentSimilarity(Vector3 a, Vector3 b)
    {
        Vector3 dirA = Vector3.Normalize(a);
        Vector3 dirB = Vector3.Normalize(b);

        float dirSim = Vector3.Dot(dirA, dirB);
        float lengthSim = MathF.Min(a.Length(), b.Length()) / MathF.Max(a.Length(), b.Length());

        return dirSim * 0.8f + lengthSim * 0.2f; // weighted
    }

    public static float DeltaAngle(float current, float target)
    {
        float delta = (target - current) % 360f;

        if(delta < -180f)
            delta += 360f;
        else if(delta > 180f)
            delta -= 360f;

        return delta;
    }

}
