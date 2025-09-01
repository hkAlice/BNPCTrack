using System.Numerics;
using Dbscan;

public class Point3D : IPointData
{
    public Vector3 Position { get; }

    public Point Point => throw new System.NotImplementedException();

    public Point3D(Vector3 pos)
    {
        Position = pos;
    }

    public double Distance(IPointData other)
    {
        var o = (Point3D)other;
        return Vector3.Distance(Position, o.Position);
    }
}
