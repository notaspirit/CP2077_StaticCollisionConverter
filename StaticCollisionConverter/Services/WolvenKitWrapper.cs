using System;
using System.IO;
using StaticCollisionConverter.WolvenKitExtensions;
using WolvenKit;
using WolvenKit.Common.Services;
using WolvenKit.Core.Compression;
using WolvenKit.Core.Interfaces;
using WolvenKit.Core.Services;
using WolvenKit.Modkit.RED4;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.CR2W;
using WolvenKit.RED4.CR2W.Archive;

namespace StaticCollisionConverter.Services;

public class WolvenKitWrapper
{
    private static WolvenKitWrapper? instance;

    public HashService HashService;
    public HookService HookService;
    public Red4ParserService Red4ParserService;
    public ArchiveManager ArchiveManager;
    public GeometryCacheService GeometryCacheService;
    public IProgressService<double> ProgressService;
    public ILoggerService LoggerService;
    public ModTools ModTools;
    public ArchiveWriter ArchiveWriter;
    public MemoryArchiveWriter MemoryArchiveWriter;
    public WolvenKitExtensions.ModToolsExtension ModToolExtensions;
    
    private WolvenKitWrapper(string gameExePath, bool enableMods = false)
    {
        if (string.IsNullOrEmpty(gameExePath)) throw new ArgumentNullException(nameof(gameExePath), "Game executable path cannot be null or empty.");
        if (!File.Exists(gameExePath)) throw new FileNotFoundException("Game executable path cannot be found.");
        
        Oodle.Load();
        
        LoggerService = new SerilogWrapper();
        ProgressService = new ProgressService<double>();
        HashService = new HashService();
        HookService = new HookService();
        Red4ParserService = new Red4ParserService(HashService, LoggerService, HookService);
        ArchiveManager = new ArchiveManager(HashService, Red4ParserService, LoggerService, ProgressService);
        
        ArchiveManager.Initialize(new FileInfo(gameExePath), enableMods);
        
        GeometryCacheService = new GeometryCacheService(ArchiveManager, Red4ParserService);
        
        ModTools = new ModTools(LoggerService, ProgressService, HashService, Red4ParserService, ArchiveManager, HookService);
        ArchiveWriter = new ArchiveWriter(HashService, LoggerService);
        MemoryArchiveWriter = new MemoryArchiveWriter(this);
        
        ModToolExtensions = new ModToolsExtension(this);
    }

    /// <summary>
    /// Initializes the service (should only be called once when interactive mode is being opened), does not return an instance
    /// </summary>
    /// <param name="gameExePath"></param>
    /// <param name="enableMods"></param>
    /// <returns></returns>
    public static bool Initialize(string gameExePath, bool enableMods = false)
    {
        if (instance != null) return true;
        try
        {
            instance = new WolvenKitWrapper(gameExePath, enableMods);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error initializing WolvenKitWrapper: " + e.Message);
            return false;
        }
    }
    /// <summary>
    /// Get the current Instance
    /// </summary>
    /// <exception cref="ArgumentException">if wrapper is not initialized</exception>
    public static WolvenKitWrapper Instance
    {
        get
        {
            if (instance != null)
                return instance;
            throw new ArgumentException("Instance is not initialized");
        }
    }
}