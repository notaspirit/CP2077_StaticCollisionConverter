using System;
using System.Collections.Generic;
using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using WolvenKit.Common.PhysX;

namespace StaticCollisionConverter.Converters;

public static class BV4ToGltfConverter
{
    public static void Convert(BV4TriangleMesh bv4, string outputPath)
    {
        var vertices = bv4.Vertices;
        var triangles = bv4.Triangles;

        // Use MeshBuilder with Position, Normal, Tangent, and Texture coordinates
        // Using VertexPositionNormalTangent as a common vertex type to include tangents
        var meshBuilder = new MeshBuilder<VertexPositionNormalTangent, VertexTexture1, VertexEmpty>("BV4Mesh");
        var material = new MaterialBuilder("DefaultMaterial")
            .WithDoubleSide(true);

        var primitive = meshBuilder.UsePrimitive(material);

        for (int i = 0; i < triangles.Count; i++)
        {
            var tri = triangles[i];
            var v0 = vertices[(int)tri[0]];
            var v1 = vertices[(int)tri[1]];
            var v2 = vertices[(int)tri[2]];

            var p0 = new Vector3(v0.X, v0.Y, v0.Z);
            var p1 = new Vector3(v1.X, v1.Y, v1.Z);
            var p2 = new Vector3(v2.X, v2.Y, v2.Z);

            // Calculate Normal
            var edge1 = p1 - p0;
            var edge2 = p2 - p0;
            var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

            // UVs: "a triangle that covers half of the UV space where each triangle corner is in the corner of the UV space"
            // (0,0), (1,0), (0,1)
            var uv0 = new Vector2(0, 0);
            var uv1 = new Vector2(1, 0);
            var uv2 = new Vector2(0, 1);

            // Calculate Tangent
            // deltaPos1 = deltaUV1.x * T + deltaUV1.y * B
            // deltaPos2 = deltaUV2.x * T + deltaUV2.y * B
            // deltaPos1 = p1 - p0, deltaUV1 = uv1 - uv0 = (1,0)
            // deltaPos2 = p2 - p0, deltaUV2 = uv2 - uv0 = (0,1)
            // p1 - p0 = 1 * T + 0 * B => T = p1 - p0
            // p2 - p0 = 0 * T + 1 * B => B = p2 - p0
            var tangentVec = Vector3.Normalize(p1 - p0);
            var tangent = new Vector4(tangentVec.X, tangentVec.Y, tangentVec.Z, 1.0f);

            var vp0 = new VertexPositionNormalTangent(p0, normal, tangent);
            var vp1 = new VertexPositionNormalTangent(p1, normal, tangent);
            var vp2 = new VertexPositionNormalTangent(p2, normal, tangent);

            var vt0 = new VertexTexture1(uv0);
            var vt1 = new VertexTexture1(uv1);
            var vt2 = new VertexTexture1(uv2);

            primitive.AddTriangle(
                (vp0, vt0),
                (vp1, vt1),
                (vp2, vt2)
            );
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);

        var model = scene.ToGltf2();

        // Ensure mesh and node names are set as WolvenKit uses them for LOD identification
        if (model.LogicalMeshes.Count > 0)
        {
            model.LogicalMeshes[0].Name = "LOD_1";
        }
        if (model.LogicalNodes.Count > 0)
        {
            model.LogicalNodes[0].Name = "LOD_1";
        }

        model.SaveGLB(outputPath);
    }
}
