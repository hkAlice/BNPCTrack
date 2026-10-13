using System;
using System.Collections.Generic;
using System.Numerics;

public struct VelocitySample
{
    public Vector3 Vector;   // per-axis velocity
    public float Speed;      // magnitude (unified)
    public DateTime Time;    // timestamp for reference

    public bool IsValid => Time != default;
}

public readonly struct VelocityOption
{
    private readonly VelocitySample? _value;

    private VelocityOption(VelocitySample value)
    {
        _value = value;
    }

    public static VelocityOption Some(VelocitySample value) => new(value);
    public static VelocityOption None => default;

    public bool HasValue => _value.HasValue;
    public VelocitySample Value => _value ?? throw new InvalidOperationException("No value present");

    public T Match<T>(Func<VelocitySample, T> some, T none)
        => _value.HasValue ? some(_value.Value) : none;

    public void Match(Action<VelocitySample> some, Action none)
    {
        if (_value.HasValue)
            some(_value.Value);
        else
            none();
    }
}

class Velocity
{
    public static List<VelocitySample> ComputeVelocities(List<SnapshotDataEntry> entries)
    {
        if(entries == null)
            throw new ArgumentNullException(nameof(entries));

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
        if(windowSize < 2)
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be at least 2");

        _windowSize = windowSize;
        _window = new Queue<SnapshotDataEntry>();
    }

    public VelocityOption ProcessNext(SnapshotDataEntry entry)
    {
        if(entry == null)
            throw new ArgumentNullException(nameof(entry));

        _window.Enqueue(entry);

        if(_window.Count < 2)
            return VelocityOption.None;

        if(_window.Count > _windowSize)
            _window.Dequeue();

        // Compute smoothed velocity as total displacement / total time
        // This properly weights by time for irregular sampling intervals
        var arr = _window.ToArray();
        Vector3 totalDisplacement = arr[^1].Position - arr[0].Position;
        double totalTime = (arr[^1].Time - arr[0].Time).TotalSeconds;

        if(totalTime <= 0)
            totalTime = 1.0; // avoid div by zero

        Vector3 smoothedVelocity = totalDisplacement / (float)totalTime;
        float speed = smoothedVelocity.Length();

        var sample = new VelocitySample
        {
            Vector = smoothedVelocity,
            Speed = speed,
            Time = entry.Time
        };

        return VelocityOption.Some(sample);
    }
}
