using System.Runtime.InteropServices;
using StaticCollisionConverter.Services;
using WolvenKit.Common.PhysX;

namespace StaticCollisionConverter.Converters;

public class BV4ToDynCollMesh
{
    public static byte[] Convert(BV4TriangleMesh bv4)
    {
        Console.WriteLine("Repacking vertices...");
        
        var verts = new float[bv4.NbVertices * 3];
        var iv = 0;
        foreach (var vert in bv4.Vertices)
        {
            verts[iv] = vert.X;
            verts[iv + 1] = vert.Y;
            verts[iv + 2] = vert.Z;
            iv += 3;
        }
        
        Console.WriteLine("Repacking triangles...");

        var tris = new uint[bv4.NbTriangles * 3];
        var it = 0;
        foreach (var tri in bv4.Triangles)
        {
            tris[it] = tri[0];
            tris[it + 1] = tri[1];
            tris[it + 2] = tri[2];
            it += 3;
        }
        
        Console.WriteLine("Cooking mesh...");
        
        var cookedGeo = PxBridge.PxBCookTriangleMesh(verts, (uint)verts.Length, tris, (uint)tris.Length);
        if (cookedGeo.size == 0)
            return [];
        
        var buffer = new byte[cookedGeo.size];
        Marshal.Copy(cookedGeo.data, buffer, 0, (int)cookedGeo.size);
        
        Console.WriteLine("Freeing buffer");
        
        PxBridge.PxBFreeBuffer(cookedGeo.data);
        
        Console.WriteLine("Done!");
        return buffer;
    }
}