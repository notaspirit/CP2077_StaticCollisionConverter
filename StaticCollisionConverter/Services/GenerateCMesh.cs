using StaticCollisionConverter.Converters;
using WolvenKit.Common.Model.Arguments;
using WolvenKit.Common.PhysX;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;

namespace StaticCollisionConverter.Services;

public class GenerateCMesh
{
    private CR2WFile? donorMesh;
    private WolvenKitWrapper wkit = WolvenKitWrapper.Instance;

    public void SetDonorMesh(string meshPath)
    {
        using var meshFileStream = new FileStream(meshPath, FileMode.Open, FileAccess.Read);
        var cr2wfile = wkit.Red4ParserService.ReadRed4File(meshFileStream);
        if (cr2wfile?.RootChunk is not CMesh { RenderResourceBlob.Chunk: rendRenderMeshBlob } mesh)
            throw new InvalidDataException();
        
        donorMesh = cr2wfile;
    }
    
    public void ReleaseDonorMesh() => donorMesh = null;
    
    
    public CR2WFile? Generate(ulong sectorHash, ulong shapeHash)
    {
        if (donorMesh == null)
            throw new InvalidDataException("Donor mesh is not set.");
        
        var colMesh = wkit.GeometryCacheService.GetEntry(sectorHash, shapeHash);
        return colMesh == null ? null : Generate(colMesh);
    }
    
    public CR2WFile? Generate(PhysXMesh colMesh)
    {
        if (donorMesh == null)
            throw new InvalidDataException("Donor mesh is not set.");

        try
        {
            ArraySegment<byte> glb;
            switch (colMesh)
            {
                case BV4TriangleMesh bv4Mesh:
                    glb = BV4ToGltfConverter.Convert(bv4Mesh);
                    break;
                case ConvexMesh convexMesh:
                    glb = ConvexToGltfConverter.Convert(convexMesh);
                    break;
                default:
                    throw new NotSupportedException();
            }

            var gltfImportArgs = new GltfImportArgs
            {
                ImportFormat = GltfImportAsFormat.Mesh,
                Keep = false,
                ImportMaterials = false,
                ImportGarmentSupport = false,
                ShowVerboseLogOutput = true
            };
            
            return wkit.ModToolExtensions.ImportMesh(glb, donorMesh, gltfImportArgs);
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed to import mesh via WolvenKit: " + e);
            return null;
        }
    }
}