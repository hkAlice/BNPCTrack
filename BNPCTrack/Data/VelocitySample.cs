using System;
using System.Collections.Generic;
using System.Numerics;

public class VelocitySample
{
    public Vector3 Vector;   // per-axis velocity
    public float Speed;      // magnitude (unified)
    public DateTime Time;    // timestamp for reference
}

class Velocity
{
    public static List<VelocitySample> ComputeVelocities(List<SnapshotDataEntry> entries)
    {
        var result = new List<VelocitySample>();
        if(entries.Count < 2)
            return result;

        for(int i = 1; i < entries.Count; i++)
        {
            Vector3 delta = entries[i].Position - entries[i - 1].Position;
            double deltaTime = (entries[i].Time - entries[i - 1].Time).TotalSeconds;

            if(deltaTime <= 0)
                deltaTime = 1.0; // avoid div by zero

            Vector3 velocity = delta / (float)deltaTime;       // per-axis velocity
            float speed = velocity.Length();                   // unified velocity

            result.Add(new VelocitySample
            {
                Vector = velocity,
                Speed = speed,
                Time = entries[i].Time
            });
        }

        return result;
    }
}
public class SmoothedVelocityAnalyzer
{
    private readonly int _windowSize;
    private readonly Queue<SnapshotDataEntry> _window;

    public SmoothedVelocityAnalyzer(int windowSize = 5)
    {
        _windowSize = Math.Max(2, windowSize); // need moar
        _window = new Queue<SnapshotDataEntry>();
    }

    public VelocitySample ProcessNext(SnapshotDataEntry entry)
    {
        _window.Enqueue(entry);
        if(_window.Count < 2)
            return null; // not enuff BACK OFF !!

        if(_window.Count > _windowSize)
            _window.Dequeue();

        // compute average velocity over window
        Vector3 deltaSum = Vector3.Zero;
        double timeSum = 0.0;

        var arr = _window.ToArray();
        for(int i = 1; i < arr.Length; i++)
        {
            Vector3 delta = arr[i].Position - arr[i - 1].Position;
            double dt = (arr[i].Time - arr[i - 1].Time).TotalSeconds;
            if(dt <= 0)
                dt = 1.0; // avoid div by zero

            deltaSum += delta / (float)dt;
            timeSum += dt;
        }

        Vector3 smoothedVelocity = deltaSum / (arr.Length - 1); // average per-axis velocity
        float speed = smoothedVelocity.Length();

        return new VelocitySample
        {
            Vector = smoothedVelocity,
            Speed = speed,
            Time = entry.Time
        };
    }
}
