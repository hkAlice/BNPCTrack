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

    public long LastSampleTime { get; private set; }

    public SampleTracker(int binSizeMs = 100, int timelineWindowMs = 5000)
    {
        this.binSizeMs = binSizeMs;
        this.timelineWindowMs = timelineWindowMs;
        binCount = timelineWindowMs / binSizeMs;
        triggerBins = new bool[binCount];
        timelineStartMs = Environment.TickCount64;
    }

    public void AddSample()
    {
        long now = Environment.TickCount64;
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

    public float GetSPS() => lastTriggerTimes.Count;

    public long LastSampleAgoMs => LastSampleTime == 0 ? 0 : Environment.TickCount64 - LastSampleTime;


    // draw timeline strip
    public void DrawTimelineUI()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(0, 0));
        for(int i = 0; i < binCount; i++)
        {
            bool triggered = triggerBins[i];
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
