using StaticCollisionConverter.Converters;
using WolvenKit.Common.Model.Arguments;
using WolvenKit.Common.PhysX;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;

namespace StaticCollisionConverter.Services;

public class GenerateCMesh
{
    public static void Generate(string meshPath, ulong sectorHash, ulong shapeHash, string outPath)
    {
        var wkit = WolvenKitWrapper.Instance;
        var colMesh = wkit.GeometryCacheService.GetEntry(sectorHash, shapeHash);
        if (colMesh == null)
            return;
        Generate(meshPath, colMesh, outPath);
    }
    
    public static void Generate(string meshPath, PhysXMesh colMesh, string outPath)
    {
        var wkit = WolvenKitWrapper.Instance;
        using var meshFileStream = new FileStream(meshPath, FileMode.Open, FileAccess.Read);
        var cr2wfile = wkit.Red4ParserService.ReadRed4File(meshFileStream);
        if (cr2wfile?.RootChunk is not CMesh { RenderResourceBlob.Chunk: rendRenderMeshBlob } mesh)
            throw new InvalidDataException();
        
        var tempGlbPath = Path.GetTempFileName() + ".glb";
        try
        {
            switch (colMesh)
            {
                case BV4TriangleMesh bv4Mesh:
                    BV4ToGltfConverter.Convert(bv4Mesh, tempGlbPath);
                    break;
                case ConvexMesh convexMesh:
                    ConvexToGltfConverter.Convert(convexMesh, tempGlbPath);
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

            using var meshStream = new MemoryStream();
            using (var writer = new CR2WWriter(meshStream))
            {
                writer.WriteFile(cr2wfile);
            }
            
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            File.WriteAllBytes(outPath, meshStream.ToArray());
            
            using var redFs = new FileStream(outPath, FileMode.Open, FileAccess.ReadWrite);
            
            if (!wkit.ModTools.ImportMesh(new FileInfo(tempGlbPath), redFs, gltfImportArgs))
                Console.WriteLine("Failed to import mesh via WolvenKit.");
        }
        finally
        {
            if (File.Exists(tempGlbPath))
                File.Delete(tempGlbPath);
        }
    }
}