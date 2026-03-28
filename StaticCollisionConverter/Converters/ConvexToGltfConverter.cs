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
    public static ArraySegment<byte> Convert(ConvexMesh convex)
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

                // Convert from WolvenKit space to glTF space
                // WolvenKit: (x, y, z) LHS Z+
                // glTF: (x, z, -y) RHS Y+
                // This is the inverse of WolvenKit's import: (x, -z, y)
                var p0 = new Vector3(v0.X, v0.Z, -v0.Y);
                var p1 = new Vector3(v1.X, v1.Z, -v1.Y);
                var p2 = new Vector3(v2.X, v2.Z, -v2.Y);

                // Calculate Normal in glTF space
                // Face winding is (vp1, vp0, vp2), so normal is Normalize(Cross(vp0 - vp1, vp2 - vp1))
                var normal = -Vector3.Normalize(Vector3.Cross(p0 - p1, p2 - p1));

                // UVs: a triangle that covers half of the UV space
                var uv0 = new Vector2(0, 0);
                var uv1 = new Vector2(1, 0);
                var uv2 = new Vector2(0, 1);

                // Calculate Tangent in glTF space
                // Tangent should be aligned with UV.x direction (uv1 - uv0)
                // Since we swap p0 and p1 in AddTriangle, we should ensure the tangent is consistent.
                // In the triangle (vp1, vp0, vp2) with UVs (uv1, uv0, uv2):
                // uv1 = (1,0), uv0 = (0,0), uv2 = (0,1)
                // Tangent is direction of (UV=1,0) - (UV=0,0), which is p1 - p0.
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

        return model.WriteGLB();
    }
}
