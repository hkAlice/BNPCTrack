using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using System;
using System.Linq;

public class SampleTracker
{
    private readonly int binSizeMs;
    private readonly int timelineWindowMs;
    private readonly int binCount;
    private readonly bool[] triggerBins;
    private long timelineStartMs;

    private readonly Queue<long> lastTriggerTimes = new Queue<long>();
    private const int SPSWindowMs = 1000;

    // Lock object for thread safety - protects all mutable state
    private readonly object _lock = new object();

    public long LastSampleTime { get; private set; }

    public SampleTracker(int binSizeMs = 100, int timelineWindowMs = 5000)
    {
        if(binSizeMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(binSizeMs), "Bin size must be positive");
        if(timelineWindowMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timelineWindowMs), "Timeline window must be positive");
        if(timelineWindowMs < binSizeMs)
            throw new ArgumentException("Timeline window must be at least as large as bin size");

        this.binSizeMs = binSizeMs;
        this.timelineWindowMs = timelineWindowMs;
        binCount = timelineWindowMs / binSizeMs;
        triggerBins = new bool[binCount];
        timelineStartMs = Environment.TickCount64;
    }

    public void AddSample()
    {
        long now = Environment.TickCount64;

        lock(_lock)
        {
            LastSampleTime = now;

            // update timeline bins
            long elapsed = now - timelineStartMs;
            if(elapsed >= timelineWindowMs)
            {
                int cycles = (int)(elapsed / binSizeMs);
                cycles = Math.Min(cycles, binCount);
                Array.Clear(triggerBins, 0, cycles);
                timelineStartMs += cycles * binSizeMs;
            }

            int bin = (int)((now - timelineStartMs) / binSizeMs);
            if(bin >= 0 && bin < binCount)
                triggerBins[bin] = true;

            // Update SPS queue
            lastTriggerTimes.Enqueue(now);
            while(lastTriggerTimes.Count > 0 && now - lastTriggerTimes.Peek() > SPSWindowMs)
                lastTriggerTimes.Dequeue();
        }
    }

    public float GetSPS()
    {
        lock(_lock)
        {
            return lastTriggerTimes.Count;
        }
    }

    public long LastSampleAgoMs
    {
        get
        {
            lock(_lock)
            {
                return LastSampleTime == 0 ? 0 : Environment.TickCount64 - LastSampleTime;
            }
        }
    }

    // draw timeline strip
    public void DrawTimelineUI()
    {
        // Snapshot the bins under lock for UI rendering
        bool[] binsSnapshot;
        lock(_lock)
        {
            binsSnapshot = (bool[])triggerBins.Clone();
        }

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(0, 0));
        for(int i = 0; i < binCount; i++)
        {
            bool triggered = binsSnapshot[i];
            var color = triggered
                ? new System.Numerics.Vector4(0f, 1f, 0f, 1f)
                : new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 1f);

            ImGui.SameLine(0, 0);
            ImGui.TextColored(color, "■");
        }
        ImGui.PopStyleVar();

        ImGui.NewLine();
    }
}
