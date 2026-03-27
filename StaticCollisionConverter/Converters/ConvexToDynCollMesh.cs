using System.Runtime.InteropServices;
using StaticCollisionConverter.Services;
using WolvenKit.Common.PhysX;

namespace StaticCollisionConverter.Converters;

public static class ConvexToDynCollMesh
{
    public static byte[] Convert(ConvexMesh convex)
    {
        var verts = new float[convex.HullData.HullVertices.Count * 3];
        var iv = 0;
        foreach (var vert in convex.HullData.HullVertices)
        {
            verts[iv] = vert.X;
            verts[iv + 1] = vert.Y;
            verts[iv + 2] = vert.Z;
            iv += 3;
        }
        
        // Console.WriteLine($"Convex mesh has {convex.HullData.HullVertices.Count} / {verts.Length} vertices");

        if (convex.HullData.HullVertices.Count >= 256)
        {
            Console.WriteLine($"WARNING: Convex mesh has too many vertices! {convex.HullData.HullVertices.Count}");
            return [];
        }
        
        var cookedGeo = PxBridge.PxBCookConvexMesh(verts, (uint)verts.Length);
        if (cookedGeo.size == 0)
            return [];
        
        var buffer = new byte[cookedGeo.size];
        Marshal.Copy(cookedGeo.data, buffer, 0, (int)cookedGeo.size);
        
        PxBridge.PxBFreeBuffer(cookedGeo.data);
        
        return buffer;
    }
}