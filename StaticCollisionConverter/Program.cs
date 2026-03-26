using System.CommandLine.Parsing;
using StaticCollisionConverter.Converters;
using StaticCollisionConverter.Services;
using WolvenKit.Common.PhysX;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;

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

                        GenerateCMesh.Generate(commandArray[1], ulong.Parse(commandArray[2]), ulong.Parse(commandArray[3]), commandArray[4]);
                        break;
                    case "convert-single-collision-to-entity":
                        if (commandArray.Length != 5)
                        {
                            Console.WriteLine("This command requires 4 parameters!");
                            Console.WriteLine(
                                "Usage: convert-single-collision <path to donor> <sectorHash> <shapeHash> <outputPath>");
                        }
                        
                        Console.WriteLine("Initializing PxBridge...");
                        
                        PxBridge.PxBInit();
                        
                        Console.WriteLine("Generating CMesh...");
                        
                        GenerateCMesh.Generate(commandArray[1], ulong.Parse(commandArray[2]),
                            ulong.Parse(commandArray[3]), commandArray[4].Replace(".ent", ".mesh"));
                        
                        Console.WriteLine("Loading BV4...");
                        
                        var bv4mesh =
                            WolvenKitWrapper.Instance.GeometryCacheService.GetEntry(ulong.Parse(commandArray[2]),
                                ulong.Parse(commandArray[3]));
                        
                        Console.WriteLine("Converting BV4 to DynCollMesh...");
                        
                        var cookedColl = BV4ToDynCollMesh.Convert(bv4mesh as BV4TriangleMesh);
                        
                        Console.WriteLine("Generating Entity...");
                        
                        var ent = GenerateEntity.Generate(commandArray[4], cookedColl, dynCollMeshType.TriangleMesh);
                        
                        Console.WriteLine("Writing Entity to CR2W...");
                        
                        var cr2wfile = new CR2WFile()
                        {
                            RootChunk = ent
                        };

                        using (var meshStream = new MemoryStream())
                        {
                            using (var writer = new CR2WWriter(meshStream))
                            {
                                writer.WriteFile(cr2wfile);
                            }
                
                            Directory.CreateDirectory(Path.GetDirectoryName(commandArray[4])!);

                            File.WriteAllBytes(commandArray[4], meshStream.ToArray());
                        }
                        
                        Console.WriteLine("Done!");
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
