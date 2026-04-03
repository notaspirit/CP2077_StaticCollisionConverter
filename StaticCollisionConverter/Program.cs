using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;
using DynamicData.Kernel;
using StaticCollisionConverter.Converters;
using StaticCollisionConverter.Services;
using StaticCollisionConverter.WolvenKitExtensions;
using WolvenKit.Common.PhysX;
using WolvenKit.Common.Services;
using WolvenKit.Core.Interfaces;
using WolvenKit.RED4.Archive.Buffer;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;

namespace StaticCollisionConverter
{
    class Program
    {
        /// <summary>
        /// Starts the interactive mode
        /// </summary>
        /// <param name="gameExePath">path to the game exe</param>
        /// <param name="enableMods">optional: enable mod support, by default false </param>
        /// <returns></returns>
        static void StartInteractiveMode(string gameExePath, bool enableMods = false)
        {
            Console.WriteLine("Initializing InteractiveMode...");
            
            if (!WolvenKitWrapper.Initialize(gameExePath, enableMods)) return;
            
            Console.WriteLine("App started. Type 'exit' to quit, 'help' for help.");
            
            while (true)
            {
                Console.Write("> ");
                string? command = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(command))
                    continue;

                if (command.ToLower() == "exit")
                    break;

                HandleCommand(command);
            }
        
            Console.WriteLine("Exiting...");
        }
        /// <summary>
        /// Dispatches / Runs the command in interactive mode
        /// </summary>
        /// <param name="command">string of arguments</param>
        /// <returns></returns>
        static void HandleCommand(string command)
        {
            var commandArray = ParseArguments(command);
            try
            {
                switch (commandArray[0])
                {
                    /*
                     * Add new Commands here
                     * Make sure to check for argument length before using them
                     * Add a short description to the help command
                     * Here WolvenKitWrapper is loaded
                     *
                     * It is best to have little logic here and just call a method
                     */
                    case "hello":
                        if (commandArray.Length < 2)
                        {
                            Console.WriteLine("This command requires 1 parameter!");
                            return;
                        }

                        Console.WriteLine($"Hello, {commandArray[1]}!");
                        break;
                    case "convert-single-collision-to-cmesh":
                        if (commandArray.Length != 5)
                        {
                            Console.WriteLine("This command requires 4 parameters!");
                            Console.WriteLine("Usage: convert-single-collision <path to donor> <sectorHash> <shapeHash> <outputPath>");
                            return;
                        }
                        
                        var genCMesh = new GenerateCMesh();
                        
                        genCMesh.SetDonorMesh(commandArray[1]);
                        
                        var cmesh = genCMesh.Generate(ulong.Parse(commandArray[2]), ulong.Parse(commandArray[3]));
                        
                        genCMesh.ReleaseDonorMesh();
                        
                        if (cmesh == null)
                        {
                            Console.WriteLine("Failed to generate CMesh!");
                            return;
                        }
                        
                        CR2WFileWriter.Write(cmesh, commandArray[4]);
                        break;
                    case "convert-single-collision-to-entity":
                        if (commandArray.Length != 6)
                        {
                            Console.WriteLine("This command requires 5 parameters!");
                            Console.WriteLine(
                                "Usage: convert-single-collision <path to donor> <sectorHash> <shapeHash> <projectPath> <relativeOutputPath>");
                            return;
                        }
                        
                        string donorPath = commandArray[1];
                        ulong sectorHash = ulong.Parse(commandArray[2]);
                        ulong shapeHash = ulong.Parse(commandArray[3]);
                        string projectPath = commandArray[4];
                        string relativeEntOutputPath = commandArray[5];
                        string relativeMeshOutputPath = relativeEntOutputPath.Replace(".ent", ".mesh");
                        
                        PxBridge.PxBInit();
                        var genCMeshForEnt = new GenerateCMesh();
                        genCMeshForEnt.SetDonorMesh(donorPath);
                        
                        var colMesh = WolvenKitWrapper.Instance.GeometryCacheService.GetEntry(sectorHash, shapeHash);
                        
                        var cmeshForEnt = genCMeshForEnt.Generate(colMesh);
                        if (cmeshForEnt == null)
                            throw new Exception("Failed to generate CMesh!");
                        genCMeshForEnt.ReleaseDonorMesh();
                        CR2WFileWriter.Write(cmeshForEnt, Path.Join(projectPath, relativeMeshOutputPath));
                        
                        dynCollMeshType colType;
                        byte[] cookedColl;
                        switch (colMesh)
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
                                throw new NotImplementedException();
                        }
                        
                        if (cookedColl.Length == 0)
                        {
                            Console.WriteLine($"Failed to cook shape for {sectorHash}, {shapeHash} for entity, {colType}");
                            return;
                        }
                        
                        var ent = GenerateEntity.Generate(relativeMeshOutputPath, [cookedColl], colType);
                        
                        CR2WFileWriter.Write(ent, Path.Join(projectPath, relativeEntOutputPath));
                        
                        Console.WriteLine("Done!");
                        break;
                    case "convert-single-cmesh-to-entity":
                        if (commandArray.Length != 4)
                        {
                            Console.WriteLine("This command requires 3 parameters!");
                            Console.WriteLine(
                                "Usage: convert-single-cmesh <projectPath> <relative path to cmesh> <relativeOutputPath>");
                            return;
                        }
                        
                        
                        var projPath = commandArray[1];
                        var meshPath = commandArray[2];
                        var relativeOutputPath = commandArray[3];
                        
                        PxBridge.PxBInit();
                        
                        var wkit = WolvenKitWrapper.Instance;
                        List<byte[]> collMeshes;
                        using (var meshFileStream = new FileStream(Path.Join(projPath, meshPath), FileMode.Open, FileAccess.Read))
                        {
                            var cr2wfileCmesh = wkit.Red4ParserService.ReadRed4File(meshFileStream);
                            if (cr2wfileCmesh?.RootChunk is not CMesh { RenderResourceBlob.Chunk: rendRenderMeshBlob } mesh)
                                throw new InvalidDataException();
                        
                            collMeshes = CMeshToDynCollMesh.Convert(mesh);
                        };
                        
                        var cr2wfileent = GenerateEntity.Generate(meshPath, collMeshes, dynCollMeshType.TriangleMesh);
                        
                        CR2WFileWriter.Write(cr2wfileent, Path.Join(projPath, relativeOutputPath));
                        break;
                    case "generate-all-geometry-cache-entries":
                        if (commandArray.Length < 5)
                        {
                            Console.WriteLine("This command requires 2 parameters!");
                            Console.WriteLine("Usage: generate-all-geometry-cache-entries <donormesh> <projectPath> <relativeMeshOut> <relativeEntOut> <skip (optional): CMesh | Ent >");
                            return;
                        }

                        if (!PxBridge.PxBInit())
                        {
                            Console.WriteLine("Failed to initialize PxBridge!");
                            return;
                        }
                        
                        var allDonorMesh = commandArray[1];
                        var allProjectPath = commandArray[2];
                        var allRelativeMeshDir = commandArray[3];
                        var allRelativeEntDir = commandArray[4];
                        var allSkipCmesh = false;
                        var allskipEnt = false;
                        if (commandArray.Length > 5)
                        {
                            switch (commandArray[5].ToLower())
                            {
                                case "cmesh":
                                    allSkipCmesh = true;
                                    break;
                                case "ent":
                                    allskipEnt = true;
                                    break;
                                default:
                                    Console.WriteLine("Invalid argument for skip parameter! Must be 'CMesh' or 'Ent' or not present");
                                    return;
                            }
                        }
                            

                        var sw = new Stopwatch();
                        sw.Start();
                        GenerateAllGeometryCacheEntries.Generate(allDonorMesh, allProjectPath, allRelativeMeshDir, allRelativeEntDir, allSkipCmesh, allskipEnt);
                        sw.Stop();
                        
                        Console.WriteLine($"Done! Took {FormatElapsedTime(sw.Elapsed)}");
                        break;
                    case "count-shapes":
                        wkit = WolvenKitWrapper.Instance;
                        wkit.GeometryCacheService.Load();
        
                        var field = typeof(GeometryCacheService)
                            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
        
                        var fieldValue = field.GetValue(wkit.GeometryCacheService);
                        if (fieldValue is not Dictionary<ulong, Dictionary<ulong, PhysXMesh>> geoCache)
                            throw new Exception("WolvenKits GeometryCacheService._entries is not a Dictionary<ulong, Dictionary<ulong, PhysXMesh>>! Aborting...");

                        ulong count = geoCache.Aggregate<KeyValuePair<ulong, Dictionary<ulong, PhysXMesh>>, ulong>(0, (current, sectorEntry) => current + (ulong)sectorEntry.Value.Count);
                        
                        Console.WriteLine($"There are {count} shapes in the GeometryCache!");
                        
                        break;
                    case "generate-wb-txt":
                        if (commandArray.Length != 2)
                        {
                            Console.WriteLine("This command requires 1 parameters!");
                            Console.WriteLine("Usage: generate-wb-txt <outPath>");
                            return;
                        }

                        wkit = WolvenKitWrapper.Instance;
                        wkit.GeometryCacheService.Load();

                        var geofield = typeof(GeometryCacheService)
                            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);

                        var geofieldValue = geofield.GetValue(wkit.GeometryCacheService);
                        if (geofieldValue is not Dictionary<ulong, Dictionary<ulong, PhysXMesh>> geoCacheForWB)
                            throw new Exception(
                                "WolvenKits GeometryCacheService._entries is not a Dictionary<ulong, Dictionary<ulong, PhysXMesh>>! Aborting...");

                        using (var fs = new FileStream(commandArray[1], FileMode.Create))
                        using (var swW = new StreamWriter(fs))
                        {
                            foreach (var sectorEntry in geoCacheForWB)
                            {
                                var sectorHashtxt = sectorEntry.Key;
                                if (sectorHashtxt == 0)
                                    sectorHashtxt = 18372265557566354072; // magic number go brrr, it's what the world sectors reference it has for whatever reason
                                
                                foreach (var shapeEntry in sectorEntry.Value)
                                {
                                    swW.WriteLine($"{sectorHashtxt} {shapeEntry.Key} {shapeEntry.Value.GetType().Name}");
                                }
                                fs.Flush();
                            }
                        }
                        break;
                    case "help":
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("  exit: exit - exits the program");
                        Console.WriteLine("  hello: hello - greets you");
                        Console.WriteLine("  help: help - shows this help message");
                        break;
                    default:
                        Console.WriteLine("Unknown command.");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to execute command: {command} with exception: {e}");   
            }
           
            static string FormatElapsedTime(TimeSpan elapsed)
            {
                var parts = new List<string>();
        
                if (elapsed.Hours > 0)
                {
                    parts.Add($"{elapsed.Hours} hour{(elapsed.Hours == 1 ? "" : "s")}");
                }
                if (elapsed.Minutes > 0)
                {
                    parts.Add($"{elapsed.Minutes} minute{(elapsed.Minutes == 1 ? "" : "s")}");
                }
                if (elapsed.Seconds > 0 || parts.Count == 0)
                {
                    parts.Add($"{elapsed.Seconds}.{elapsed.Milliseconds:D3} seconds");
                }
        
                return string.Join(", ", parts);
            }
            
        }
        /// <summary>
        /// Splits the string to an array of arguments
        /// </summary>
        /// <param name="input">string to split</param>
        /// <returns></returns>
        static string[] ParseArguments(string input)
        {
            return CommandLineStringSplitter.Instance.Split(input).ToArray();
        }
        
        /// <summary>
        /// Method called when app is run
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static void Main(string[] args)
        { 
            if (args.Length == 0)
            {
                Console.WriteLine("Error: No command provided.");
                Console.WriteLine("Usage: StaticCollisionConverter.exe <command>");
                return;
            }
            
            string command = args[0];
            
            switch (command)
            {
                /*
                 * This is only for commands outside the interactive mod
                 * => WolvenKitWrapper is *not* loaded
                 */
                case "start":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("start command requires 1 or 2 arguments: <game exe path> optional: <enable mods>");
                        return;
                    }
                    
                    bool enableMods = false;
                    if (args.Length > 2)
                    {
                        bool.TryParse(args[2], out enableMods);
                    }
                    
                    StartInteractiveMode(args[1], enableMods);
                    break;
                case "help":
                    Console.WriteLine("Available commands:");
                    Console.WriteLine(" start - starts the interactive mode");
                    Console.WriteLine(" help - Displays this help message");
                    break;
                default:
                    Console.WriteLine($"Error: Unknown command '{command}'.");
                    Console.WriteLine("Type 'StaticCollisionConverter.exe help' for a list of available commands.");
                    break;
            }
        }
    }
}
