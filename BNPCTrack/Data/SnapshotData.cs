using System;
using System.Collections.Generic;
using System.Numerics;

public class SnapshotData
{
    public string Name;
    public DateTime StartTime;
    public DateTime EndTime;
    public List<SnapshotDataEntry> Entries;
}

public class SnapshotDataEntry
{
    public System.Numerics.Vector3 Position;
    public float Rotation;
    public DateTime Time;
}
