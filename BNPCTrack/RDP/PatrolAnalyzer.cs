using FFXIVClientStructs.FFXIV.Common.Math;
using System.Collections.Generic;

public static class PatrolAnalyzer
{
    // check if patrol loops or reverses/ping-pongs
    public static bool IsLoop(List<System.Numerics.Vector3> simplified, float threshold = 1.0f)
    {
        // todo: i'm not 100% on this tbh
        if(simplified.Count < 2)
            return false;

        return Vector3.Distance(simplified[0], simplified[^1]) < threshold;
    }
}
