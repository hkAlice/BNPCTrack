using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;
using System.Linq;
using static Dbscan3D;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using BNPCTrack;

namespace BNPCTrack.Windows;

public class MainWindow : Window, IDisposable
{
    private BNPCTrackPlugin Plugin;

    public MainWindow(BNPCTrackPlugin plugin)
        : base("BNPCTrack##welcome to hell", ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    uint GetClusterColor(int clusterId)
    {
        if(clusterId == (int)PointLabel.Noise)
            return ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1));

        var rng = new Random(clusterId * 7919); // deterministic per ID
        return ImGui.GetColorU32(new Vector4(
            (float)rng.NextDouble(),
            (float)rng.NextDouble(),
            (float)rng.NextDouble(),
            1f));
    }

    public unsafe override void Draw()
    {
        var targetObject = Plugin.CurrentTarget;

        long lastSampleAgo = -1;
        if(targetObject != null && Plugin.LastSampleTimes.Count() > 0)
            lastSampleAgo = Environment.TickCount64 - Plugin.LastSampleTimes.Last();
        float sps = Plugin.LastSampleTimes.Count;

        ImGui.Text($"Last Sample: {lastSampleAgo} ms");
        ImGui.SameLine(400f);
        ImGui.Text($"Samples/s: {sps:0.##}");
        ImGui.SameLine(200f);
        ImGui.Text($"BNPCs: {Plugin.BNPCCount}");

        // sample presets
        if(ImGui.BeginCombo("Presets", GetPresetLabel(Plugin.SamplingIntervalMs)))
        {
            if(ImGui.Selectable("Every Frame", Plugin.SamplingIntervalMs == 0))
                Plugin.SamplingIntervalMs = 0;
            if(ImGui.Selectable("10 Hz (100ms)", Plugin.SamplingIntervalMs == 100))
                Plugin.SamplingIntervalMs = 100;
            if(ImGui.Selectable("5 Hz (200ms)", Plugin.SamplingIntervalMs == 200))
                Plugin.SamplingIntervalMs = 200;
            if(ImGui.Selectable("1 Hz (1000ms)", Plugin.SamplingIntervalMs == 1000))
                Plugin.SamplingIntervalMs = 1000;

            ImGui.EndCombo();
        }

        ImGui.Text($"Sampling Timeline (last {BNPCTrackPlugin.TimelineWindowMs}ms)");

        ImGui.BeginChild("timeline", new System.Numerics.Vector2(0, 30), false);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
        for (int i = 0; i < BNPCTrackPlugin.BinCount; i++)
        {
            bool triggered = Plugin.TriggerBins[i];

            var color = triggered
                ? new Vector4(0f, 1f, 0f, 1f)       // green = sampled
                : new Vector4(0.2f, 0.2f, 0.2f, 1f); // gray = no sample
            ImGui.SameLine();
            ImGui.TextColored(color, "■");
        }
        ImGui.PopStyleVar();

        ImGui.EndChild();

        if(targetObject != null)
        {
            var recColor = lastSampleAgo == 0 ? new Vector4(1f, 0f, 0f, 1f) : new Vector4(0.2f, 0.2f, 0.2f, 1f);
            ImGui.TextColored(recColor, "●");
            ImGui.SameLine(24);
            ImGui.TextUnformatted("Currently capturing " + targetObject.Name);
        }

        if(Plugin.SnapshotData != null)
        {
            var diff = Plugin.SnapshotData.Entries.LastOrDefault().Time.Subtract(Plugin.SnapshotData.StartTime);
            
            ImGui.TextUnformatted("Capture Data");
            ImGui.TextUnformatted("Name: " + Plugin.SnapshotData.Name.ToString());
            ImGui.TextUnformatted("Pos: " + Plugin.SnapshotData.Entries.LastOrDefault().Position.ToString());
            ImGui.TextUnformatted("Rot: " + Plugin.SnapshotData.Entries.LastOrDefault().Rotation.ToString());
            ImGui.TextUnformatted("Entries: " + Plugin.SnapshotData.Entries.Count.ToString() + " points");
            ImGui.TextUnformatted("Time Elapsed: " + diff.ToString("mm\\:ss\\.ff"));

            var velocitySamplesAll = Velocity.ComputeVelocities(Plugin.SnapshotData.Entries);

            ImGui.TextUnformatted("Velocity: " + velocitySamplesAll.LastOrDefault().Vector.ToString());
            ImGui.TextUnformatted("Speed: " + velocitySamplesAll.LastOrDefault().Speed.ToString());

            ImGui.Separator();
            ImGui.TextUnformatted("RDP (Continuous patrol paths)");
            // epsilon (distance threshold)
            float rdpEpsilon = Plugin.RDPEpsilon;
            ImGui.InputFloat("Epsilon", ref rdpEpsilon, 0.01f, 10.0f, "%.2f");
            Plugin.RDPEpsilon = rdpEpsilon;

            if(ImGui.Button("Run RDP"))
            {
                Plugin.RunRDP();
            }

            if(Plugin.SnapshotData != null && Plugin.SnapshotData.Entries.Count > 1 
                && Plugin.RDPSimplifiedResult != null && Plugin.RDPSimplifiedResult.Points.Count > 1)
            {
                ImGui.TextUnformatted("Loops: " + Plugin.RDPSimplifiedResult.IsLoop.ToString());
                ImGui.TextUnformatted("Reverse: " + Plugin.RDPSimplifiedResult.IsReverse.ToString());

                var entries = Plugin.SnapshotData.Entries;
                var rawPoints = entries.Select(e => e.Position).ToList();
                var simplified = Plugin.RDPSimplifiedResult.Points;
                var velocityAnalyzer = new SmoothedVelocityAnalyzer(windowSize: 5);

                // smoothed velocity for raw points
                var velocitySamples = new List<VelocitySample>();
                foreach(var e in entries)
                {
                    var v = velocityAnalyzer.ProcessNext(e);
                    if(v != null)
                        velocitySamples.Add(v);
                }

                Vector2 plotSize = new Vector2(400, 400);
                Vector2 plotPos = ImGui.GetCursorScreenPos();
                ImGui.Dummy(plotSize);

                var drawList = ImGui.GetWindowDrawList();
                drawList.AddRect(plotPos, plotPos + plotSize, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)));

                // plot padding so it doesn't draw on border
                float minX = MathF.Min(rawPoints.Min(p => p.X), simplified.Min(p => p.X));
                float maxX = MathF.Max(rawPoints.Max(p => p.X), simplified.Max(p => p.X));
                float minZ = MathF.Min(rawPoints.Min(p => p.Z), simplified.Min(p => p.Z));
                float maxZ = MathF.Max(rawPoints.Max(p => p.Z), simplified.Max(p => p.Z));

                float paddingX = (maxX - minX) * 0.05f;
                float paddingZ = (maxZ - minZ) * 0.05f;
                minX -= paddingX; maxX += paddingX;
                minZ -= paddingZ; maxZ += paddingZ;

                Func<Vector3, Vector2> toScreen = (p) =>
                {
                    float xNorm = (p.X - minX) / (maxX - minX);
                    float zNorm = (p.Z - minZ) / (maxZ - minZ);
                    return new Vector2(
                        plotPos.X + xNorm * plotSize.X,
                        plotPos.Y + (1 - zNorm) * plotSize.Y
                    );
                };

                // raw path (gray)
                foreach(var p in rawPoints)
                {
                    Vector2 pos = toScreen(p);
                    drawList.AddCircleFilled(pos, 2f, ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 0.3f)));
                }

                // simplified RDP paths
                // velocity to color (blue = slow, red = fast)
                float maxSpeed = velocitySamples.Max(v => v.Speed);
                float minSpeed = velocitySamples.Min(v => v.Speed);

                for(int i = 0; i < simplified.Count - 1; i++)
                {
                    Vector2 a = toScreen(simplified[i]);
                    Vector2 b = toScreen(simplified[i + 1]);

                    // find approx speed for this segment using nearest sample
                    float speed = velocitySamples.Count > i ? velocitySamples[i].Speed : 0f;
                    float t = (speed - minSpeed) / MathF.Max(0.01f, maxSpeed - minSpeed);
                    Vector4 colorVec = new Vector4(t, 0, 1 - t, 1); // interpolate blue -> red
                    uint lineColor = ImGui.GetColorU32(colorVec);

                    drawList.AddLine(a, b, lineColor, 2.0f);

                    // direction arrow or attempt to lol
                    Vector2 dir = Vector2.Normalize(b - a);
                    Vector2 perp = new Vector2(-dir.Y, dir.X);
                    float arrowSize = 14.0f;
                    Vector2 tip = b;
                    Vector2 left = b - dir * arrowSize + perp * (arrowSize * 0.5f);
                    Vector2 right = b - dir * arrowSize - perp * (arrowSize * 0.5f);
                    drawList.AddTriangleFilled(tip, left, right, lineColor);
                }

                // waypoints
                for(int i = 0; i < simplified.Count; i++)
                {
                    Vector2 pos = toScreen(simplified[i]);
                    uint color = (i == 0) ? ImGui.GetColorU32(new Vector4(0, 1, 1, 1))    // start
                             : (i == simplified.Count - 1) ? ImGui.GetColorU32(new Vector4(1, 0, 0, 1)) // end
                             : ImGui.GetColorU32(new Vector4(1, 1, 0, 1)); // middle
                    drawList.AddCircleFilled(pos, 4f, color);
                }
            }




            ImGui.Separator();
            ImGui.TextUnformatted("DBSCAN (Use for BNPCs that stop or have cluster positions)");

            // min points per cluster
            int minPoints = Plugin.DBScanMinPointsPerCluster;
            ImGui.InputInt("Cluster Point Count", ref minPoints);
            if(minPoints < 1)
                minPoints = 1;

            Plugin.DBScanMinPointsPerCluster = minPoints;

            // epsilon (distance threshold)
            float epsilon = Plugin.DBScanEpsilon;
            ImGui.InputFloat("Epsilon", ref epsilon, 0.01f, 1.0f, "%.2f");
            Plugin.DBScanEpsilon = epsilon;

            int impl = Plugin.DBScanImpl;
            if(ImGui.RadioButton("3D Simple", impl == 0)) { impl = 0; }
            ImGui.SameLine();
            if(ImGui.RadioButton("2D Rbush", impl == 1)) { impl = 1; }
            ImGui.SameLine();

            Plugin.DBScanImpl = impl;

            if(ImGui.Button("Run DBSCAN"))
            {
                Plugin.RunDBSCAN();
            }

            if(Plugin.DBScanResult != null)
            {
                var clusters = Plugin.DBScanResult;
                ImGui.TextUnformatted("Cluster counter: " + clusters.Count());
                ImGui.Separator();

                Vector2 plotSize = new Vector2(400, 400);
                Vector2 plotPos = ImGui.GetCursorScreenPos();
                ImGui.Dummy(plotSize);

                var drawList = ImGui.GetWindowDrawList();
                drawList.AddRect(plotPos, plotPos + plotSize, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)));

                // flatten to normalize
                var allPoints = clusters.SelectMany(c => c.Points).ToList();
                float minX = allPoints.Min(p => p.X), maxX = allPoints.Max(p => p.X);
                float minZ = allPoints.Min(p => p.Z), maxZ = allPoints.Max(p => p.Z);

                // plot clusters
                foreach(var cluster in clusters)
                {
                    uint color = GetClusterColor(cluster.Id);

                    foreach(var p in cluster.Points)
                    {
                        float xNorm = (p.X - minX) / (maxX - minX);
                        float zNorm = (p.Z - minZ) / (maxZ - minZ);

                        Vector2 pos = new Vector2(
                            plotPos.X + xNorm * plotSize.X,
                            plotPos.Y + (1 - zNorm) * plotSize.Y // flip Z for top-down
                        );

                        drawList.AddCircleFilled(pos, 3f, color);
                    }
                }
            }

            if(ImGui.BeginTable("DataTable", 5, ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg))
            {
                // set table headers
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.None);
                ImGui.TableSetupColumn("Position X", ImGuiTableColumnFlags.None);
                ImGui.TableSetupColumn("Position Y", ImGuiTableColumnFlags.None);
                ImGui.TableSetupColumn("Position Z", ImGuiTableColumnFlags.None);
                ImGui.TableSetupColumn("Rotation", ImGuiTableColumnFlags.None);
                ImGui.TableHeadersRow();

                float scrollY = ImGui.GetScrollY();
                float maxScrollY = ImGui.GetScrollMaxY();
                bool isBottom = (scrollY >= maxScrollY - 50.0f); // :flushed:

                var clipper = new ImGuiListClipper();
                clipper.Begin(Plugin.SnapshotData.Entries.Count);

                while(clipper.Step())
                {
                    for(int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                    {
                        var data = Plugin.SnapshotData.Entries[row];

                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(data.Time.ToString("HH:mm:ss.fff"));

                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(data.Position.X.ToString("F6"));

                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text(data.Position.Y.ToString("F6"));

                        ImGui.TableSetColumnIndex(3);
                        ImGui.Text(data.Position.Z.ToString("F6"));

                        ImGui.TableSetColumnIndex(4);
                        ImGui.Text(data.Rotation.ToString("F6"));
                    }
                }

                clipper.End();

                if(isBottom)
                {
                    
                    ImGui.SetScrollY(maxScrollY);
                }


                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextUnformatted("Focus Target an actor to begin");
        }

        ImGui.Spacing();
    }

    private string GetPresetLabel(long ms)
    {
        return ms switch
        {
            0 => "Every Frame",
            100 => "10 Hz (100ms)",
            200 => "5 Hz (200ms)",
            1000 => "1 Hz (1000ms)",
            _ => $"{1000f / ms:0.##} Hz ({ms}ms)"
        };
    }
}
