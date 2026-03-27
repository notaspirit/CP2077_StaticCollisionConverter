using System.Runtime.InteropServices;
using SharpDX;
using StaticCollisionConverter.Services;
using WolvenKit.RED4.Types;

namespace StaticCollisionConverter.Converters;

public record SimpleSubMesh(float[] Vertices, uint[] PolygonIndices);

public static class CMeshToDynCollMesh
{
    public static List<byte[]> Convert(CMesh cMesh)
    {
        #region ParseCMesh into simple submeshes for cooking
        
        if (cMesh.RenderResourceBlob is not { Chunk: rendRenderMeshBlob rendBlob })
            return [];

        int lowestLod = 1;
        var rendInfos = rendBlob.Header.RenderChunkInfos;
        foreach (var rendInfo in rendInfos)
        {
            if (rendInfo.LodMask > lowestLod) lowestLod = rendInfo.LodMask;
        }

        using var ms = new MemoryStream(rendBlob.RenderBuffer.Buffer.GetBytes());
        var br = new BinaryReader(ms);

        var quantScale = new SharpDX.Vector4(rendBlob.Header.QuantizationScale.X,
            rendBlob.Header.QuantizationScale.Y,
            rendBlob.Header.QuantizationScale.Z,
            rendBlob.Header.QuantizationScale.W);
        var quantOffset = new SharpDX.Vector4(rendBlob.Header.QuantizationOffset.X,
            rendBlob.Header.QuantizationOffset.Y,
            rendBlob.Header.QuantizationOffset.Z,
            rendBlob.Header.QuantizationOffset.W);

        List<SimpleSubMesh> submeshes = new();
        
        for(int indexSubMesh = 0; indexSubMesh < rendInfos.Count; indexSubMesh++)
        {
            var rendInfo = rendInfos[indexSubMesh];
            if (rendInfo.LodMask != lowestLod) continue;
            
            var vertsOut = new float[rendInfo.NumVertices * 3];
            var indicesOut = new uint[rendInfo.NumIndices];

            for (int indexVertex = 0; indexVertex < rendInfo.NumVertices; indexVertex++)
            {
                br.BaseStream.Position = rendInfo.ChunkVertices.ByteOffsets[0] + (indexVertex * rendInfo.ChunkVertices.VertexLayout.SlotStrides[0]);
                
                vertsOut[indexVertex] = (br.ReadInt16() / 32767f * quantScale.X) + quantOffset.X;
                vertsOut[indexVertex + 1] = (br.ReadInt16() / 32767f * quantScale.Y) + quantOffset.Y;
                vertsOut[indexVertex + 2] = (br.ReadInt16() / 32767f * quantScale.Z) + quantOffset.Z;
            }

            br.BaseStream.Position = rendBlob.Header.IndexBufferOffset + rendInfo.ChunkIndices.TeOffset;
            for (int indexIndex = 0; indexIndex < rendInfo.NumIndices; indexIndex++)
            {
                indicesOut[indexIndex] = br.ReadUInt16();
            }

            submeshes.Add(new SimpleSubMesh(vertsOut, indicesOut));
        }
        
        #endregion

        #region Cook submeshes

        List<byte[]> cookedSubMeshes = new();

        foreach (var subMesh in submeshes)
        {
            var cookedSubMesh =
                PxBridge.PxBCookTriangleMesh(subMesh.Vertices, (uint)subMesh.Vertices.Length,
                    subMesh.PolygonIndices, (uint)subMesh.PolygonIndices.Length);
            if (cookedSubMesh.size == 0)
                continue;

            var buffer = new byte[cookedSubMesh.size];
            Marshal.Copy(cookedSubMesh.data, buffer, 0, (int)cookedSubMesh.size);
            PxBridge.PxBFreeBuffer(cookedSubMesh.data);
            
            cookedSubMeshes.Add(buffer);
        }

        #endregion
        
        return cookedSubMeshes;
    }
}