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
using Dalamud.Interface.ImGuiFileDialog;
using CsvHelper;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using static FFXIVClientStructs.FFXIV.Common.Component.BGCollision.MeshPCB;
using System.Collections;

namespace BNPCTrack.Windows;

public class MainWindow : Window, IDisposable
{
    private BNPCTrackPlugin Plugin;
    private FileDialogManager FileDialogManager { get; }

    public MainWindow(BNPCTrackPlugin plugin)
        : base("BNPCTrack##welcome to hell", ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.FileDialogManager = new FileDialogManager
        {
            AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
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

        long lastSampleAgo = Plugin.SampleTracker.LastSampleAgoMs;
        bool triggered = lastSampleAgo == 0;
        if(targetObject == null) 
            lastSampleAgo = 0;

        ImGui.Text($"Last Sample: {lastSampleAgo} ");
        ImGui.SameLine(120f);
        ImGui.Text(" ms");
        ImGui.SameLine(180f);
        ImGui.Text($"Samples/s: {Plugin.SampleTracker.GetSPS():0.##}");
        ImGui.SameLine(320f);
        ImGui.Text($"BNPCs: {Plugin.BNPCCount}");

        // sample presets
        ImGui.PushItemWidth(150f);
        if(ImGui.BeginCombo("Sampling Rate", GetPresetLabel(Plugin.SamplingIntervalMs)))
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
        ImGui.PopItemWidth();

        ImGui.NewLine();

        Plugin.SampleTracker.DrawTimelineUI();

        DrawBNPCSelector();

        ImGui.Separator();

        if(targetObject != null)
        {
            
            var recColor = triggered ? new Vector4(1f, 0f, 0f, 1f) : new Vector4(0.2f, 0.2f, 0.2f, 1f);
            ImGui.TextColored(recColor, "●");
            ImGui.SameLine(24);
            ImGui.TextUnformatted("Currently capturing " + targetObject.Name);
        }

        if(Plugin.SnapshotData != null && Plugin.SnapshotData.Entries.Count > 0)
        {
            var diff = Plugin.SnapshotData.Entries.LastOrDefault().Time.Subtract(Plugin.SnapshotData.StartTime);
            
            ImGui.TextUnformatted("Name: " + Plugin.SnapshotData.Name.ToString());
            if(Plugin.SnapshotData.HadAggro)
            {
                ImGui.SameLine(400f);
                ImGui.TextColored(new Vector4(1.0f, 0.1f, 0.4f, 1.0f), "BNPC has had aggro");
            }
            ImGui.TextUnformatted("Pos: " + Plugin.SnapshotData.Entries.LastOrDefault().Position.ToString());
            ImGui.SameLine(400f);
            ImGui.TextUnformatted("Rot: " + Plugin.SnapshotData.Entries.LastOrDefault().Rotation.ToString());
            ImGui.TextUnformatted("Entries: " + Plugin.SnapshotData.Entries.Count.ToString() + " points");
            ImGui.SameLine(400f);
            ImGui.TextUnformatted("Time Elapsed: " + diff.ToString("mm\\:ss\\.ff"));

            var velocitySamplesAll = Velocity.ComputeVelocities(Plugin.SnapshotData.Entries);
            if(velocitySamplesAll != null && velocitySamplesAll.Count() > 0)
            {
                ImGui.TextUnformatted("Velocity: " + velocitySamplesAll.LastOrDefault().Vector.ToString());
                ImGui.SameLine(400f);
                ImGui.TextUnformatted("Speed: " + velocitySamplesAll.LastOrDefault().Speed.ToString());
            }

            ImGui.Separator();
            ImGui.TextUnformatted("RDP (Continuous patrol paths)");
            // epsilon (distance threshold)
            float rdpEpsilon = Plugin.RDPEpsilon;
            ImGui.PushItemWidth(100f);
            ImGui.InputFloat("Epsilon", ref rdpEpsilon, 0.01f, 10.0f, "%.2f");
            Plugin.RDPEpsilon = rdpEpsilon;
            ImGui.PopItemWidth();
            ImGui.SameLine();
            float rdpLoopTolerance = Plugin.LoopTolerance;
            ImGui.PushItemWidth(100f);
            ImGui.InputFloat("Loop Tolerance", ref rdpLoopTolerance, 0.01f, 10.0f, "%.2f");
            Plugin.LoopTolerance = rdpLoopTolerance;
            ImGui.PopItemWidth();

            if(ImGui.Button("Run RDP"))
            {
                Plugin.RunRDP();
            }

            if(Plugin.SnapshotData != null && Plugin.SnapshotData.Entries.Count > 1)
            {
                ImGui.TextUnformatted("Loops: " + Plugin.RDPSimplifiedResult.IsLoop.ToString());
                ImGui.SameLine(100f);
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

                // left side of column: plotter
                ImGui.BeginGroup();
                { 
                    Vector2 plotPos = ImGui.GetCursorScreenPos();
                    ImGui.Dummy(plotSize);

                    var drawList = ImGui.GetWindowDrawList();
                    drawList.AddRect(plotPos, plotPos + plotSize, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)));

                    // plot padding so it doesn't draw on border
                    float minX = MathF.Min(rawPoints.Min(p => p.X), simplified.Min(p => p.X));
                    float maxX = MathF.Max(rawPoints.Max(p => p.X), simplified.Max(p => p.X));
                    float minZ = MathF.Min(rawPoints.Min(p => p.Z), simplified.Min(p => p.Z));
                    float maxZ = MathF.Max(rawPoints.Max(p => p.Z), simplified.Max(p => p.Z));

                    float padX = (maxX - minX) * 0.05f;
                    float padZ = (maxZ - minZ) * 0.05f;
                    minX -= padX; maxX += padX;
                    minZ -= padZ; maxZ += padZ;

                    float rangeX = maxX - minX;
                    float rangeZ = maxZ - minZ;
                    float scale = MathF.Min(plotSize.X / rangeX, plotSize.Y / rangeZ);

                    float offsetX = (plotSize.X - rangeX * scale) * 0.5f;
                    float offsetZ = (plotSize.Y - rangeZ * scale) * 0.5f;

                    // flip Z i guess
                    bool flipZ = true;

                    Func<Vector3, Vector2> toScreen = (p) =>
                    {
                        float x = (p.X - minX) * scale + offsetX;
                        float z = (p.Z - minZ) * scale + offsetZ;

                        if(flipZ)
                            z = plotSize.Y - z;

                        return plotPos + new Vector2(x, z);
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

                        // Skip if too short
                        Vector2 seg = b - a;
                        if(seg.LengthSquared() < 1e-4f)
                            continue;

                        // Velocity-based color
                        float speed = velocitySamples.Count > i ? velocitySamples[i].Speed : 0f;
                        float t = (speed - minSpeed) / MathF.Max(0.01f, maxSpeed - minSpeed);
                        Vector4 colorVec = new Vector4(t, 0, 1 - t, 1); // blue → red
                        uint lineColor = ImGui.GetColorU32(colorVec);

                        // Draw line
                        drawList.AddLine(a, b, lineColor, 2.0f);

                        // Arrow tip
                        Vector2 dir = Vector2.Normalize(seg);
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
                ImGui.EndGroup();

                ImGui.SameLine();

                // right side point table
                ImGui.BeginGroup();
                float tableHeight = plotSize.Y;

                ImGui.BeginChild("RDPTableChild", new Vector2(0, tableHeight), false);
                {
                    if(ImGui.BeginTable("RDPPointsTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, tableHeight - 26)))
                    {
                        ImGui.TableSetupColumn("#");
                        ImGui.TableSetupColumn("X");
                        ImGui.TableSetupColumn("Y");
                        ImGui.TableSetupColumn("Z");
                        ImGui.TableSetupColumn("SegLen");
                        ImGui.TableSetupColumn("CumDist");
                        ImGui.TableHeadersRow();

                        float cumulative = 0f;

                        for(int i = 0; i < simplified.Count; i++)
                        {
                            float segLen = 0f;
                            if(i > 0)
                            {
                                segLen = Vector3.Distance(simplified[i], simplified[i - 1]);
                                cumulative += segLen;
                            }

                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0); ImGui.Text(i.ToString());
                            ImGui.TableSetColumnIndex(1); ImGui.Text($"{simplified[i].X:F2}");
                            ImGui.TableSetColumnIndex(2); ImGui.Text($"{simplified[i].Y:F2}");
                            ImGui.TableSetColumnIndex(3); ImGui.Text($"{simplified[i].Z:F2}");
                            ImGui.TableSetColumnIndex(4); ImGui.Text($"{segLen:F2}");
                            ImGui.TableSetColumnIndex(5); ImGui.Text($"{cumulative:F2}");
                        }

                        ImGui.EndTable();
                    }
                    if(ImGui.Button("Export CSV"))
                    {
                        string filename = $"RDP_{Plugin.CurrentTarget.GameObjectId}_{Plugin.CurrentTarget.Name}.csv";

                        float cumulative = 0f;

                        var records = simplified.Select((p, i) =>
                        {
                            float segLen = 0f;
                            if(i > 0)
                                segLen = Vector3.Distance(p, simplified[i - 1]);

                            cumulative += segLen;

                            return new
                            {
                                Index = i,
                                X = p.X,
                                Y = p.Y,
                                Z = p.Z,
                                SegmentLength = segLen,
                                CumulativeDistance = cumulative
                            };
                        }).ToList();

                        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                        {
                            HasHeaderRecord = true
                        };

                        ExportToCSV(filename, records);
                    }
                }

                ImGui.EndChild();

                ImGui.EndGroup();
            }

            ImGui.Separator();
            ImGui.TextUnformatted("DBSCAN (Use for BNPCs that stop or have cluster positions)");

            ImGui.PushItemWidth(100f);
            // epsilon (distance threshold)
            float epsilon = Plugin.DBScanEpsilon;
            ImGui.InputFloat("Epsilon", ref epsilon, 0.01f, 1.0f, "%.2f");
            Plugin.DBScanEpsilon = epsilon;
            ImGui.SameLine();
            // min points per cluster
            int minPoints = Plugin.DBScanMinPointsPerCluster;
            ImGui.InputInt("Cluster Point Count", ref minPoints);
            if(minPoints < 1)
                minPoints = 1;
            Plugin.DBScanMinPointsPerCluster = minPoints;
            ImGui.PopItemWidth();

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

            ImGui.Separator();

            if(ImGui.Button("Clear capture"))
            {
                Plugin.SnapshotData = null;
            }
            ImGui.SameLine();
            if(ImGui.Button("Export to CSV"))
            {
                string filename = $"CAPTURE_{Plugin.CurrentTarget.GameObjectId}_{Plugin.CurrentTarget.Name}.csv";

                var records = Plugin.SnapshotData.Entries.Select((p, i) =>
                {

                    return new
                    {
                        Index = i,
                        X = p.Position.X,
                        Y = p.Position.Y,
                        Z = p.Position.Z,
                        Rot = p.Rotation,
                        Time = p.Time.Millisecond
                    };
                }).ToList();

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                ExportToCSV(filename, records);
            }

            if(Plugin.SnapshotData != null && Plugin.SnapshotData.Entries.Count > 0)
            {
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
        }
        else
        {
            ImGui.TextUnformatted("Select an actor to begin capture");
        }

        ImGui.Spacing();

        FileDialogManager.Draw();
    }

    private void DrawBNPCSelector()
    {
        var bnpcs = Plugin.GetBNPCList();

        if(ImGui.BeginTable("BNPC Selector", 3, 
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders, new Vector2(0, 150)))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Id");
            ImGui.TableSetupColumn("Distance");
            ImGui.TableHeadersRow();

            foreach(var bnpc in bnpcs)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                bool isSelected = Plugin.SelectedBNPCId == bnpc.Id;
                string label = $"{bnpc.Name}##{bnpc.Id}";

                if(ImGui.Selectable(label, isSelected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    Plugin.SelectedBNPCId = isSelected ? (uint?)null : bnpc.Id; // toggle selection
                }

                ImGui.TableNextColumn();
                ImGui.Text(bnpc.Id.ToString());

                ImGui.TableNextColumn();
                ImGui.Text($"{bnpc.Distance:0.##}");
            }

            ImGui.EndTable();
        }
    }

    private void ExportToCSV(string filename, IEnumerable records)
    {
        FileDialogManager.SaveFileDialog("Export to CSV", "*.csv", filename, ".csv",
            (b, s) => {
                if(b)
                {
                    using (var writer = new StreamWriter(s))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(records);
                    }
                }
        }, null, true);
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
