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
    
    public static void Generate(string donorMesh, string outPath, string relativeMeshDir, string relativeEntDir, bool skipMesh, bool skipEnt)
    { 
        Console.WriteLine($"Generating all geometry cache entries for {donorMesh} to {outPath}");
        if (skipMesh && skipEnt)
            return;
        
        Console.WriteLine("Initializing Dependencies...");
        
        var memFiles = new Dictionary<string, byte[]>();
        
        var cmeshGen = new GenerateCMesh();
        cmeshGen.SetDonorMesh(donorMesh);
        
        var loadedTaskField = typeof(GeometryCacheService)
            .GetField("_loadedTask", BindingFlags.Instance | BindingFlags.NonPublic);
        if (loadedTaskField?.GetValue(wkit.GeometryCacheService) is not Task<bool> { IsCompleted: true })
            wkit.GeometryCacheService.Load();
        
        var entries = typeof(GeometryCacheService)
            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
        
        var fieldValue = entries.GetValue(wkit.GeometryCacheService);
        if (fieldValue is not Dictionary<ulong, Dictionary<ulong, PhysXMesh>> geoCache)
            throw new Exception("WolvenKits GeometryCacheService._entries is not a Dictionary<ulong, Dictionary<ulong, PhysXMesh>>! Aborting...");
        
        Console.WriteLine("Processing Geometry Cache Entries...");

        var processed = 0;
        foreach (var sectorEntry in geoCache)
        {
            ProcessSectorHash(sectorEntry.Key, sectorEntry.Value);
            Console.WriteLine($"Processed {processed++} / {geoCache.Keys.Count} entries");
        }
        
        Console.WriteLine("Packing Archive...");
        
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using var archiveStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);
        wkit.MemoryArchiveWriter.WriteArchive(memFiles, archiveStream);
        
        return;
        
        void ProcessSectorHash(ulong sectorHash, Dictionary<ulong, PhysXMesh> shapeEntries)
        {
            if (sectorHash == 0)
                sectorHash = 18372265557566354072; // magic number go brrr, it's what the world sectors reference it has for whatever reason
            
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
            var filename = $"{sectorHash}_{shapeHash}_{shape.GetType().Name.ToLower()}";
            var meshName = Path.Join(relativeMeshDir, $"{filename}.mesh");
            var entName = Path.Join(relativeEntDir, $"{filename}.ent");

            // Console.WriteLine($"Processing shape for {sectorHash}, {shapeHash} with type  {shape.GetType()}");
            if (!skipMesh)
            {
                var cmesh = cmeshGen.Generate(shape);
                using var meshStream = new MemoryStream();
                using (var meshWriter = new CR2WWriter(meshStream))
                {
                    meshWriter.WriteFile(cmesh);
                }
                memFiles.Add(meshName, meshStream.ToArray());
            }
            
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
            
            // a visual mesh component gets attached at runtime in wb
            var ent = GenerateEntity.Generate(null, [cookedColl], colType);

            using var entStream = new MemoryStream();
            using var entWriter = new CR2WWriter(entStream);
            entWriter.WriteFile(ent);
            
            memFiles.Add(entName, entStream.ToArray());
        }
    }
}