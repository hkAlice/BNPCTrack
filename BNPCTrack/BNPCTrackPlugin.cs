using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dbscan.RBush;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dbscan;
using BNPCTrack.Windows;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
namespace BNPCTrack;

public sealed class BNPCTrackPlugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; }

    private const string CommandName = "/bnpc";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("BNPC Track");
    private MainWindow MainWindow { get; init; }
    public IGameObject? CurrentTarget { get; set; }
    public SnapshotData? SnapshotData { get; set; }
    public float DBScanEpsilon { get; set; }
    public int DBScanMinPointsPerCluster { get; set; }
    public int DBScanImpl { get; set; }
    public float RDPEpsilon { get; set; }
    public List<Dbscan3D.Cluster> DBScanResult { get; set; }
    public RDPResult RDPSimplifiedResult { get; set; }
    public long SamplingIntervalMs { get; set; }
    public long BNPCCount { get; set; }
    
    public SampleTracker SampleTracker = new SampleTracker();

    public BNPCTrackPlugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // todo: move this to config
        DBScanEpsilon = 0.5f;
        DBScanMinPointsPerCluster = 2;
        RDPEpsilon = 0.5f;
        SamplingIntervalMs = 100;

        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Displays BNPCTrack points and plots"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        Framework.Update += this.Update;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    public void RunRDP()
    {
        if(SnapshotData == null)
            return;

        Log.Information("Running RDP on {0} entries", SnapshotData.Entries.Count);

        var points = SnapshotData.Entries
            .Select(e => new System.Numerics.Vector3(e.Position.X, e.Position.Y, e.Position.Z))
            .ToList();
        
        // simplify with RDP
        var simplified = RdpSimplifier.Simplify(points, epsilon: RDPEpsilon);

        RDPSimplifiedResult = new RDPResult();
        RDPSimplifiedResult.Points = simplified;

        if(simplified != null)
        {
            if(PatrolAnalyzer.IsLoop(simplified))
            {
                RDPSimplifiedResult.IsLoop = true;
            }
            else
            {
                // todo: technically untrue
                RDPSimplifiedResult.IsReverse = true;
            }
        }

        
    }

    public void RunDBSCAN()
    {
        if(SnapshotData == null)
            return;

        if(DBScanImpl == 0)
        {
            // our very own dbscan with blackjack and hookah
            Log.Information("Running 3D DBSCAN on {0} entries", SnapshotData.Entries.Count);
            var points = SnapshotData.Entries
                .Select(e => new Vector3(e.Position.X, e.Position.Y, e.Position.Z))
                .ToList();

            var clusters = Dbscan3D.Run(points, epsilon: DBScanEpsilon, minPoints: DBScanMinPointsPerCluster);

            DBScanResult = [];
            foreach(var cluster in clusters)
            {
                DBScanResult.Add(cluster);
            }
        }
        else if(DBScanImpl == 1)
        {
            // 2D dbscan from nuggets
            Log.Information("Running DBSCAN.Rbush on {0} entries", SnapshotData.Entries.Count);
            var points = SnapshotData.Entries
                .Select(e => new Point2D(e.Position));

            var clusters = Dbscan.RBush.DbscanRBush.CalculateClusters(
                points,
                epsilon: 1.0,
                minimumPointsPerCluster: 4
            );

            DBScanResult = [];
            foreach(var cluster in clusters.Clusters)
            {
                var points3d = cluster.Objects
                    .Select(e => new Vector3(e.Position.X, e.Position.Y, e.Position.Z))
                    .ToList();

                var cluster3d = new Dbscan3D.Cluster(0);
                cluster3d.Points = points3d;
                DBScanResult.Add(cluster3d);
            }
        }
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private unsafe void Update(IFramework framework)
    {
        CurrentTarget = BNPCTrackPlugin.TargetManager.FocusTarget;

        BNPCCount = ObjectTable.Where(o => o is { ObjectKind: (Dalamud.Game.ClientState.Objects.Enums.ObjectKind)ObjectKind.BattleNpc }).Count();

        if(CurrentTarget == null)
            return;

        if(SnapshotData == null || SnapshotData.Name != CurrentTarget.Name.ToString())
        {
            Log.Information("Creating new SnapshotData for " + CurrentTarget.Name);
            SnapshotData = new SnapshotData
            {
                Name = CurrentTarget.Name.ToString(),
                Entries = new List<SnapshotDataEntry>(),
                StartTime = DateTime.Now
            };
        }

        long now = Environment.TickCount64;

        if(SamplingIntervalMs == 0 || SampleTracker.LastSampleTime == 0 || now - SampleTracker.LastSampleTime >= SamplingIntervalMs)
        {
            var entry = new SnapshotDataEntry
            {
                Position = CurrentTarget.Position,
                Rotation = CurrentTarget.Rotation,
                Time = DateTime.Now
            };
            SnapshotData.Entries.Add(entry);

            SampleTracker.AddSample();
        }
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleMainUI() => MainWindow.Toggle();
}
