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

    public void Process(string meshPath, ulong sectorHash, ulong shapeHash, string outPath)
    {
        using var meshFileStream = new FileStream(meshPath, FileMode.Open, FileAccess.Read);
        var cr2wfile = wkit.Red4ParserService.ReadRed4File(meshFileStream);
        if (cr2wfile?.RootChunk is not CMesh { RenderResourceBlob.Chunk: rendRenderMeshBlob } mesh)
            throw new InvalidDataException();

        var colMesh = wkit.GeometryCacheService.GetEntry(sectorHash, shapeHash);
        
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