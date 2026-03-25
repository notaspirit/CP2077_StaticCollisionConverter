using System.Text;
using WolvenKit.Common.Model.Arguments;
using WolvenKit.Common.PhysX;
using WolvenKit.Core.Extensions;
using WolvenKit.Modkit.RED4.GeneralStructs;
using WolvenKit.RED4.Types;
using WolvenKit.Modkit;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using StaticCollisionConverter.Converters;
using WolvenKit.Modkit.RED4;

namespace StaticCollisionConverter.Services;

public class ProcessingService
{
    private WolvenKitWrapper wkit;
    
    public ProcessingService(WolvenKitWrapper wkit)
    {
        this.wkit = wkit;
    }

    public void ProcessTriangle(string meshPath)
    {
        var (mesh, cr2wfile) = ReadMesh(meshPath);
        var colMesh = ReadBV4TrianglePhysXMesh();

        // 1. Convert BV4 to GLB
        var tempGlbPath = Path.GetTempFileName() + ".glb";
        try
        {
            BV4ToGltfConverter.Convert(colMesh, tempGlbPath);

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
            
            var outPath = meshPath.Replace(".mesh", "_new.mesh");
            outPath = Path.Combine("E:\\SCC", outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            File.WriteAllBytes(outPath, meshStream.ToArray());
            
            using var redFs = new FileStream(outPath, FileMode.Open, FileAccess.ReadWrite);

            var success = wkit.ModTools.ImportMesh(new FileInfo(tempGlbPath), redFs, gltfImportArgs);
            if (!success)
            {
                Console.WriteLine("Failed to import mesh via WolvenKit.");
                return;
            }
        }
        finally
        {
            if (File.Exists(tempGlbPath))
                File.Delete(tempGlbPath);
        }
    }

    public void Process(string meshPath)
    {
        var (mesh, cr2wfile) = ReadMesh(meshPath);
        var colMesh = ReadConvexMesh();

        // 1. Convert Convex to GLB
        var tempGlbPath = Path.GetTempFileName() + ".glb";
        try
        {
            ConvexToGltfConverter.Convert(colMesh, tempGlbPath);

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
            
            var outPath = meshPath.Replace(".mesh", "_new.mesh");
            outPath = Path.Combine("E:\\SCC", outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            File.WriteAllBytes(outPath, meshStream.ToArray());
            
            using var redFs = new FileStream(outPath, FileMode.Open, FileAccess.ReadWrite);

            var success = wkit.ModTools.ImportMesh(new FileInfo(tempGlbPath), redFs, gltfImportArgs);
            if (!success)
            {
                Console.WriteLine("Failed to import mesh via WolvenKit.");
                return;
            }
        }
        finally
        {
            if (File.Exists(tempGlbPath))
                File.Delete(tempGlbPath);
        }
    }

    private (CMesh mesh, CR2WFile file) ReadMesh(string meshPath)
    {
        var cr2wfile = wkit.ArchiveManager.GetCR2WFile(meshPath);
        if (cr2wfile?.RootChunk is CMesh { RenderResourceBlob.Chunk: rendRenderMeshBlob } mesh)
            return (mesh, cr2wfile);
        Console.WriteLine("File is not a mesh.");
        throw new InvalidDataException();
    }

    private BV4TriangleMesh ReadBV4TrianglePhysXMesh()
    {
        ulong sectorHash = 12717457377011094652;
        ulong shapeHash = 1000903821159525457;
        
        var collisionShape = wkit.GeometryCacheService.GetEntry(sectorHash, shapeHash);
        var colMesh = collisionShape as BV4TriangleMesh;
        return colMesh!;
    }

    private ConvexMesh ReadConvexMesh()
    {
        ulong sectorHash = 1506287456064029993;
        ulong shapeHash = 15728430803346557784;
        
        var collisionShape = wkit.GeometryCacheService.GetEntry(sectorHash, shapeHash);
        var colMesh = collisionShape as ConvexMesh;
        return colMesh!;
    }
}