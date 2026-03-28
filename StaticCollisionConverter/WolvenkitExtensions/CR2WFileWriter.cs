using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;

namespace StaticCollisionConverter.WolvenKitExtensions;

public class CR2WFileWriter
{
    public static void Write(CR2WFile file, string path)
    {
        using var meshStream = new MemoryStream();
        using (var writer = new CR2WWriter(meshStream))
        {
            writer.WriteFile(file);
        }
                
        var parentDir = Path.GetDirectoryName(path);
        if (parentDir != null)
            Directory.CreateDirectory(parentDir);

        File.WriteAllBytes(path, meshStream.ToArray());
    }
}