using System.Reflection;
using StaticCollisionConverter.Converters;
using WolvenKit.Common.PhysX;
using WolvenKit.Common.Services;
using WolvenKit.RED4.Archive.Buffer;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;

namespace StaticCollisionConverter.Services;

public class GenerateAllGeometryCacheEntries
{
    private static WolvenKitWrapper wkit = WolvenKitWrapper.Instance;
    
    public static void Generate(string donorMesh, string projectPath, string relativeMeshDir, string relativeEntDir)
    {
        Directory.CreateDirectory(Path.Join(projectPath, relativeEntDir));
        Directory.CreateDirectory(Path.Join(projectPath, relativeMeshDir));
        
        wkit.GeometryCacheService.Load();
        
        var field = typeof(GeometryCacheService)
            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
        
        var fieldValue = field.GetValue(wkit.GeometryCacheService);
        if (fieldValue is not Dictionary<ulong, Dictionary<ulong, PhysXMesh>> geoCache)
            throw new Exception("WolvenKits GeometryCacheService._entries is not a Dictionary<ulong, Dictionary<ulong, PhysXMesh>>! Aborting...");

        /*
        var sectorTasks = geoCache.Select(kvp => Task.Run(() => ProcessSectorHash(kvp.Key, kvp.Value)));

        Task.WhenAll(sectorTasks).Wait();
        */
        
        foreach (var sectorEntry in geoCache)
            ProcessSectorHash(sectorEntry.Key, sectorEntry.Value);

        return;
        
        void ProcessSectorHash(ulong sectorHash, Dictionary<ulong, PhysXMesh> shapeEntries)
        {
            /*
            var shapeTasks = shapeEntries.Select(kvp => Task.Run(() => ProcessShape(sectorHash, kvp.Key, kvp.Value)));

            Task.WhenAll(shapeTasks);
            */
            
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
            
            if (File.Exists(Path.Join(projectPath, relativeMeshDir, $"{filename}.mesh")) &&
                File.Exists(Path.Join(projectPath, relativeEntDir, $"{filename}.ent")))
            {
                Console.WriteLine($"Skipping shape for {sectorHash}, {shapeHash} because it already exists");
                return;
            }
            
            Console.WriteLine($"Processing shape for {sectorHash}, {shapeHash} with type  {shape.GetType()}");
            
            GenerateCMesh.Generate(donorMesh, shape, Path.Join(projectPath, relativeMeshDir, $"{filename}.mesh"));
            
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
            
            var ent = GenerateEntity.Generate(Path.Join(relativeMeshDir, $"{filename}.ent"), [cookedColl], colType);
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