using System;
using System.Runtime.InteropServices;

namespace StaticCollisionConverter.Services;

[StructLayout(LayoutKind.Sequential)]
public struct PxBCookedMeshResult
{
    public IntPtr data;    // maps to uint8_t* in C++
    public uint size;      // maps to uint32_t
}

public static class PxBridge
{
    private const string DllName = "PhysX34Bridge";

    // ----------------------
    // Lifetime
    // ----------------------
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)] // C++ bool is 1 byte
    public static extern bool PxBInit();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void PxBDestroy();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void PxBFreeBuffer(IntPtr buffer);

    // ----------------------
    // Operations
    // ----------------------
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern PxBCookedMeshResult PxBCookTriangleMesh(
        [In] float[] vertices,
        uint vertexCount,
        [In] uint[] indices,
        uint indexCount);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern PxBCookedMeshResult PxBCookConvexMesh(
        [In] float[] vertices,
        uint vertexCount);
}