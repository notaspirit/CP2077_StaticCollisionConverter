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

public static class ConvexToGltfConverter
{
    public static void Convert(ConvexMesh convex, string outputPath)
    {
        var hullData = convex.HullData;
        var vertices = hullData.HullVertices;
        var polygons = hullData.Polygons;
        var vertexData8 = hullData.VertexData8;

        var meshBuilder = new MeshBuilder<VertexPositionNormalTangent, VertexTexture1, VertexEmpty>("ConvexMesh");
        var material = new MaterialBuilder("DefaultMaterial")
            .WithDoubleSide(true);

        var primitive = meshBuilder.UsePrimitive(material);

        foreach (var poly in polygons)
        {
            if (poly.NbVerts < 3)
                continue;

            // Polygons are defined by a list of indices in vertexData8
            // starting at poly.VRef8
            var polyIndices = new List<int>();
            for (int i = 0; i < poly.NbVerts; i++)
            {
                int vertexIndex = vertexData8[poly.VRef8 + i];
                polyIndices.Add(vertexIndex);
            }

            // Triangulate the polygon (it's convex, so a simple fan works)
            for (int i = 1; i < poly.NbVerts - 1; i++)
            {
                int idx0 = polyIndices[0];
                int idx1 = polyIndices[i];
                int idx2 = polyIndices[i + 1];

                var v0 = vertices[idx0];
                var v1 = vertices[idx1];
                var v2 = vertices[idx2];

                var p0 = new Vector3(v0.X, v0.Y, v0.Z);
                var p1 = new Vector3(v1.X, v1.Y, v1.Z);
                var p2 = new Vector3(v2.X, v2.Y, v2.Z);

                // Calculate Normal
                var edge1 = p1 - p0;
                var edge2 = p2 - p0;
                var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

                // UVs: a triangle that covers half of the UV space
                var uv0 = new Vector2(0, 0);
                var uv1 = new Vector2(1, 0);
                var uv2 = new Vector2(0, 1);

                // Calculate Tangent (same as BV4ToGltfConverter)
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
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);

        var model = scene.ToGltf2();

        // Ensure mesh and node names are set for WolvenKit identification
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
