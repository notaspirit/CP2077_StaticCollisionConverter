using System.Runtime.InteropServices;
using StaticCollisionConverter.Services;
using WolvenKit.Common.PhysX;

namespace StaticCollisionConverter.Converters;

public class BV4ToDynCollMesh
{
    public static byte[] Convert(BV4TriangleMesh bv4)
    {
        var verts = new float[bv4.Vertices.Count * 3];
        var iv = 0;
        foreach (var vert in bv4.Vertices)
        {
            verts[iv] = vert.X;
            verts[iv + 1] = vert.Y;
            verts[iv + 2] = vert.Z;
            iv += 3;
        }

        var tris = new uint[bv4.Triangles.Count * 3];
        var it = 0;
        foreach (var tri in bv4.Triangles)
        {
            tris[it] = tri[0];
            tris[it + 1] = tri[1];
            tris[it + 2] = tri[2];
            it += 3;
        }
        
        var cookedGeo = PxBridge.PxBCookTriangleMesh(verts, (uint)verts.Length, tris, (uint)tris.Length);
        if (cookedGeo.size == 0)
            return [];
        
        var buffer = new byte[cookedGeo.size];
        Marshal.Copy(cookedGeo.data, buffer, 0, (int)cookedGeo.size);
        
        PxBridge.PxBFreeBuffer(cookedGeo.data);

        return buffer;
    }
}