using System.Drawing;
using System.Numerics;
using Dbscan;
using Dbscan.RBush;

public class Point2D : IPointData
{
    public Vector3 Position { get; }

    public Point2D(Vector3 pos)
    {
        Position = pos;
    }

    // required by IPointData
    public Dbscan.Point Point => new Dbscan.Point(
        (int)Position.X,
        (int)Position.Z   // or Y, depending on which plane you care about lol - prefilling for FFXIV
    );
}
