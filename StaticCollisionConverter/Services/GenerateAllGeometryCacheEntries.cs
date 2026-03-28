using System.Reflection;
using StaticCollisionConverter.Converters;
using WolvenKit.Common.PhysX;
using WolvenKit.Common.Services;
using WolvenKit.Core.Compression;
using WolvenKit.RED4.Archive.Buffer;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;

namespace StaticCollisionConverter.Services;

public class GenerateAllGeometryCacheEntries
{
    private static WolvenKitWrapper wkit = WolvenKitWrapper.Instance;
    
    public static void Generate(string donorMesh, string projectPath, string relativeMeshDir, string relativeEntDir, bool skipMesh, bool skipEnt)
    { 
        if (skipMesh && skipEnt)
            return;
        
        Oodle.Load();
        
        Directory.CreateDirectory(Path.Join(projectPath, relativeEntDir));
        Directory.CreateDirectory(Path.Join(projectPath, relativeMeshDir));
        
        wkit.GeometryCacheService.Load();
        
        var field = typeof(GeometryCacheService)
            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
        
        var fieldValue = field.GetValue(wkit.GeometryCacheService);
        if (fieldValue is not Dictionary<ulong, Dictionary<ulong, PhysXMesh>> geoCache)
            throw new Exception("WolvenKits GeometryCacheService._entries is not a Dictionary<ulong, Dictionary<ulong, PhysXMesh>>! Aborting...");
        
        foreach (var sectorEntry in geoCache)
            ProcessSectorHash(sectorEntry.Key, sectorEntry.Value);

        return;
        
        void ProcessSectorHash(ulong sectorHash, Dictionary<ulong, PhysXMesh> shapeEntries)
        {
            foreach (var shapeEntry in shapeEntries)
            {
                try
                {
                    ProcessShape(sectorHash, shapeEntry.Key, shapeEntry.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to process shape for {sectorHash}, {shapeEntry.Key} with exception: {ex}, adding to skip list");
                    File.AppendAllText("E:\\scc_skip_shapes.txt", $"{sectorHash}_{shapeEntry.Key}: {ex}\n");
                }
            }
            
        }

        void ProcessShape(ulong sectorHash, ulong shapeHash, PhysXMesh shape)
        {
            var filename = $"{sectorHash}_{shapeHash}";
            
            if (
                (skipMesh || File.Exists(Path.Join(projectPath, relativeMeshDir, $"{filename}.mesh"))) && 
                (skipEnt || File.Exists(Path.Join(projectPath, relativeEntDir, $"{filename}.ent")))
                )
            {
                Console.WriteLine($"Skipping shape for {sectorHash}, {shapeHash} because it already exists");
                return;
            }
            
            Console.WriteLine($"Processing shape for {sectorHash}, {shapeHash} with type  {shape.GetType()}");
            if (!skipMesh)
                GenerateCMesh.Generate(donorMesh, shape, Path.Join(projectPath, relativeMeshDir, $"{filename}.mesh"));
            
            if (skipEnt)
                return;
            
            dynCollMeshType colType;
            byte[] cookedColl;
            switch (shape)
            {
                case BV4TriangleMesh bv4Mesh:
                    colType = dynCollMeshType.TriangleMesh;
                    cookedColl = BV4ToDynCollMesh.Convert(bv4Mesh);
                    break;
                case ConvexMesh convexMesh:
                    colType = dynCollMeshType.ConvexMesh;
                    cookedColl = ConvexToDynCollMesh.Convert(convexMesh);
                    break;
                default:
                    return;
            }
            
            if (cookedColl.Length == 0)
            {
                Console.WriteLine($"Failed to cook shape for {sectorHash}, {shapeHash} for entity");
                File.AppendAllText("E:\\scc_failed_cook_shapes.txt", $"{sectorHash} {shapeHash}\n");
                return;
            }
            
            // don't generate the mesh component for world builder, it gets attached at runtime
            var ent = GenerateEntity.Generate(null, [cookedColl], colType);
            var cr2went = new CR2WFile()
            {
                RootChunk = ent
            };

            using var meshStream = new MemoryStream();
            using var writer = new CR2WWriter(meshStream);
            writer.WriteFile(cr2went);
            
            File.WriteAllBytes(Path.Join(projectPath, relativeEntDir, $"{filename}.ent"), meshStream.ToArray());
        }
    }
}