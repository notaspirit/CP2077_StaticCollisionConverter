using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StaticCollisionConverter.Services;
using WolvenKit.Common.FNV1A;
using WolvenKit.Core.Compression;
using WolvenKit.Core.Extensions;
using WolvenKit.Core.Interfaces;
using WolvenKit.RED4.Archive;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;
using WolvenKit.RED4.Types.Pools;
using Index = System.Index;

namespace StaticCollisionConverter.WolvenKitExtensions;

public class MemoryArchiveWriter
{
    private WolvenKitWrapper _wkit;
    private ILoggerService _loggerService;

    private MethodInfo? _writeHeader;
    private MethodInfo? _compressAndWrite;
    private MethodInfo? _writeIndex;
    
    private FieldInfo? _s_uncompressedFiles;
    private FieldInfo? _s_alignedFiles;
    private FieldInfo? _s_soundBanksFile;

    public MemoryArchiveWriter(WolvenKitWrapper wkit)
    {
        _wkit = wkit;
        _loggerService = _wkit.LoggerService;
        
        _writeHeader = typeof(ArchiveWriter).GetMethod("WriteHeader", BindingFlags.NonPublic | BindingFlags.Instance);
        _compressAndWrite = typeof(ArchiveWriter).GetMethod("CompressAndWrite", BindingFlags.NonPublic | BindingFlags.Instance);
        _writeIndex = typeof(ArchiveWriter).GetMethod("WriteIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        
        _s_uncompressedFiles = typeof(ArchiveWriter).GetField("s_uncompressedFiles", BindingFlags.NonPublic | BindingFlags.Static);
        _s_alignedFiles = typeof(ArchiveWriter).GetField("s_alignedFiles", BindingFlags.NonPublic | BindingFlags.Static);
        _s_soundBanksFile = typeof(ArchiveWriter).GetField("s_soundBanksFile", BindingFlags.NonPublic | BindingFlags.Static);
        
        ArgumentNullException.ThrowIfNull(wkit, nameof(wkit));
        ArgumentNullException.ThrowIfNull(wkit.LoggerService, nameof(wkit.LoggerService));
        
        ArgumentNullException.ThrowIfNull(_writeHeader, nameof(_writeHeader));
        ArgumentNullException.ThrowIfNull(_compressAndWrite, nameof(_compressAndWrite));
        ArgumentNullException.ThrowIfNull(_writeIndex, nameof(_writeIndex));
        
        ArgumentNullException.ThrowIfNull(_s_uncompressedFiles, nameof(_s_uncompressedFiles));
        ArgumentNullException.ThrowIfNull(_s_alignedFiles, nameof(_s_alignedFiles));
        ArgumentNullException.ThrowIfNull(_s_soundBanksFile, nameof(_s_soundBanksFile));
    }
    
    private List<string> s_uncompressedFiles => (List<string>)_s_uncompressedFiles.GetValue(_wkit.ArchiveWriter);
    private List<string> s_alignedFiles => (List<string>)_s_alignedFiles.GetValue(_wkit.ArchiveWriter);
    private string s_soundBanksFile => (string)_s_soundBanksFile.GetValue(_wkit.ArchiveWriter);
    
    private void WriteHeader(BinaryWriter bw, Header header) => _writeHeader?.Invoke(_wkit.ArchiveWriter, [bw, header]);
    private (uint, uint) CompressAndWrite(BinaryWriter bw, byte[] inbuffer) => ((uint, uint))_compressAndWrite?.Invoke(_wkit.ArchiveWriter, [bw, inbuffer]);
    private void WriteIndex(BinaryWriter bw, WolvenKit.RED4.Archive.Index index) => _writeIndex?.Invoke(_wkit.ArchiveWriter, [bw, index]);
    
    private string GetCleanExtension(string path) => Path.GetExtension(path).ToLowerInvariant().TrimStart('.');
    
    public bool WriteArchive(Dictionary<string, byte[]> memFiles, Stream outStream)
    {
        if (memFiles.Count == 0)
            return false;
        
        if (!CompressionSettings.Get().UseOodle)
        {
            _loggerService.Warning("Oodle couldn't be loaded. Using Kraken.dll instead could cause errors.");
        }

        var regex = new Regex("^(\\d+)\\.");

        // get files
        var supportedExtensions = Enum.GetNames<ERedExtension>().ToList();
        supportedExtensions.Add("bin");

        var customPaths = new List<string>();

        var fileDict = new Dictionary<ulong, List<string>>();
        foreach (var relPath in memFiles.Keys)
        {
            if (!supportedExtensions.Contains(GetCleanExtension(relPath)))
            {
                _loggerService.Warning($"Unknown file extension for \"{relPath}\". Skipping");
                continue;
            }

            ulong hash;
            var match = regex.Match(relPath);
            if (match.Success)
            {
                if (!ulong.TryParse(match.Groups[1].Value, out hash))
                {
                    _loggerService.Warning($"Couldn't extract hash for \"{relPath}\". Skipping");
                    continue;
                }
            }
            else
            {
                var sanitizedPath = ResourcePath.SanitizePath(relPath);
                hash = FNV1A64HashAlgorithm.HashString(sanitizedPath);

                if (!ResourcePathPool.IsNative(sanitizedPath))
                {
                    customPaths.Add(sanitizedPath);
                }
            }

            if (!fileDict.ContainsKey(hash))
            {
                fileDict.Add(hash, []);
            }
            fileDict[hash].Add(relPath);
        }

        var duplicateFound = false;
        foreach (var (hash, fileEntries) in fileDict)
        {
            if (fileEntries.Count == 1)
            {
                continue;
            }

            duplicateFound = true;

            _loggerService.Error($"The following files have the same hash ({hash}):");
            foreach (var relPath in fileEntries)
            {
                _loggerService.Error($"\t{relPath}");
            }
        }

        if (duplicateFound)
        {
            _loggerService.Error($"Duplicated files found. Aborting");
            return false;
        }

        fileDict = fileDict.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

        var ar = new Archive(string.Empty);
        using var bw = new BinaryWriter(outStream, Encoding.UTF8, true);

        #region write header

        WriteHeader(bw, ar.Header);
        bw.Write(new byte[132]); // some weird padding


        #region write custom data

        long customDataLength = 0;
        if (customPaths.Count > 0)
        {
            var wfooter = new LxrsFooter(customPaths);
            wfooter.Write(bw);
            customDataLength = bw.BaseStream.Position - Header.EXTENDED_SIZE;
        }

        #endregion

        #endregion write header

        #region write files

        HashSet<ulong> importsHashSet = new();
        

        var progress = 0;
        foreach (var (hash, fileEntries) in fileDict)
        {
            var fileName = fileEntries[0];

            if (s_uncompressedFiles.Contains(GetCleanExtension(fileName).ToLower()))
            {
                _loggerService.Error($"{fileName} is too large. Maximum size for uncompressed files is {uint.MaxValue} bytes.");
                return false;
            }

            // TODO: This is due to max byte[] size (MS also uses byte[]) is int.MaxValue - 56 and we need it for compression
            if (!s_uncompressedFiles.Contains(GetCleanExtension(fileName).ToLower()) &&
                                              memFiles[fileName].Length > int.MaxValue - 57)
            {
                _loggerService.Error($"{fileName} is too large. Maximum size for compressed files is {int.MaxValue - 57} bytes.");
                return false;
            }
            using var memFileStream = new MemoryStream(memFiles[fileName]);
            using var fileBinaryReader = new BinaryReader(memFileStream);
            using var reader = new CR2WReader(fileBinaryReader);

            // fileinfo data
            var firstimportidx = (uint)importsHashSet.Count;
            var lastimportidx = (uint)importsHashSet.Count;
            var firstoffsetidx = (uint)ar.Index.FileSegments.Count;
            uint lastoffsetidx;
            var flags = 0;

            EFileReadErrorCodes readStatus;
            CR2WFileInfo? info;

            try
            {
                readStatus = reader.ReadFileInfo(out info, _loggerService);
            }
            catch (Exception)
            {
                _loggerService.Error($"Could not read \"{fileName}\".");
                return false;
            }

            if (readStatus == EFileReadErrorCodes.NoError)
            {
                // kraken the file and write
                var cr2wfilesize = (int)info!.FileHeader.objectsEnd;
                fileBinaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
                var cr2winbuffer = fileBinaryReader.ReadBytes(cr2wfilesize);
                var offset = bw.BaseStream.Position;

                var (zsize, _) = CompressAndWrite(bw, cr2winbuffer);
                ar.Index.FileSegments.Add(new FileSegment(
                    (ulong)offset,
                    zsize,
                    (uint)cr2winbuffer.Length));

                var savedSpace = cr2winbuffer.Length - zsize;

                // HINT: each cr2w needs to have the buffer already kraken'd
                // foreach buffer write
                foreach (var bufferInfo in info.BufferInfo)
                {
                    var bufferBuffer = fileBinaryReader.ReadBytes((int)bufferInfo.diskSize);

                    var bsize = bufferInfo.memSize;
                    var bzsize = bufferInfo.diskSize;
                    var boffset = bw.BaseStream.Position;

                    bw.Write(bufferBuffer);
                    ar.Index.FileSegments.Add(new FileSegment(
                        (ulong)boffset,
                        bzsize,
                        bsize));
                }

                // Disable this. Prevents other mods to overwrite files in this archive if present

                //register imports
                //foreach (var cr2WImportWrapper in reader.ImportsList)
                //{
                //    // maybe only .Default, not sure as nothing else is used
                //    if (cr2WImportWrapper.Flags is not InternalEnums.EImportFlags.Soft and not InternalEnums.EImportFlags.Embedded)
                //    {
                //        importsHashSet.Add(cr2WImportWrapper.DepotPath);
                //    }
                //}

                lastimportidx = (uint)importsHashSet.Count;

                lastoffsetidx = (uint)ar.Index.FileSegments.Count;

                flags = info.BufferInfo.Length > 0 ? info.BufferInfo.Length - 1 : 0;
            }
            else
            {
                memFileStream.Seek(0, SeekOrigin.Begin);

                if (s_alignedFiles.Contains(GetCleanExtension(fileName).ToLower()) || fileName.EndsWith(s_soundBanksFile, StringComparison.CurrentCultureIgnoreCase))
                {
                    bw.PadUntilPage();
                }

                var offset = (ulong)bw.BaseStream.Position;

                if (s_uncompressedFiles.Contains(GetCleanExtension(fileName).ToLower()) || fileName.EndsWith(s_soundBanksFile, StringComparison.CurrentCultureIgnoreCase))
                {
                    memFileStream.CopyTo(outStream);
                    var size = (uint)memFileStream.Length;

                    ar.Index.FileSegments.Add(new FileSegment(offset, size, size));
                }
                else
                {
                    var cr2winbuffer = memFileStream.ToByteArray();
                    var size = (uint)cr2winbuffer.Length;

                    // kraken the file and write
                    var (zsize, _) = CompressAndWrite(bw, cr2winbuffer);
                    ar.Index.FileSegments.Add(new FileSegment(offset, zsize, size));
                }

                lastoffsetidx = (uint)ar.Index.FileSegments.Count;
            }

            // save table data
            using var sha1 = SHA1.Create();
            var sha1hash = sha1.ComputeHash(memFileStream); //TODO: this is only correct for files with no buffer
            var item = new FileEntry(
                hash,
                DateTime.Now,
                (uint)flags,
                firstoffsetidx,
                lastoffsetidx,
                firstimportidx,
                lastimportidx,
                sha1hash);
            ar.Index.FileEntries.Add(hash, item);

            Interlocked.Increment(ref progress);
        }

        ar.Index.Dependencies = importsHashSet.Select(_ => new Dependency(_)).ToList();


        #endregion write files

        #region write footer

        bw.PadUntilPage();

        // write tables
        var tableoffset = bw.BaseStream.Position;
        WriteIndex(bw, ar.Index);
        var tablesize = bw.BaseStream.Position - tableoffset;

        // padding to page (4096 bytes)
        bw.PadUntilPage();
        var filesize = bw.BaseStream.Position;

        #endregion write footer


        // write the header again
        ar.Header.IndexPosition = (ulong)tableoffset;
        ar.Header.IndexSize = (uint)tablesize;
        ar.Header.Filesize = (ulong)filesize;
        bw.BaseStream.Seek(0, SeekOrigin.Begin);
        WriteHeader(bw, ar.Header);
        bw.Write(customDataLength);

        bw.Flush();

        return true;
    }
}