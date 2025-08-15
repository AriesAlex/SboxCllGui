using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace SboxCllGui;

public static class CllCodec
{
    private const int MAGIC_VALUE = 0x41434D47; // "GMCA" в little-endian

    public static CllFile ExtractCll(string cllPath, string outputFolder)
    {
        var bytes = File.ReadAllBytes(cllPath);

        if (!IsCllFile(bytes))
        {
            bytes = TryDecompressAsGZip(bytes);
            if (!IsCllFile(bytes))
                throw new FormatException("Not a valid CLL file even after GZip check.");
        }

        var cllFile = ParseCll(bytes);
        var finalOutput = Path.Combine(outputFolder, cllFile.PackageIdent);
        Directory.CreateDirectory(finalOutput);

        foreach (var block in cllFile.TextBlocks)
        foreach (var file in block.TextFiles)
        {
            if (file.LocalPath == null) continue;
            var writePath = Path.Combine(finalOutput, file.LocalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(writePath) ?? finalOutput);
            File.WriteAllText(writePath, file.Text ?? "");
        }

        return cllFile;
    }

    public static void PackCll(
        string folderToPack,
        string outputCllFile,
        string packageIdent,
        string compilerSettings,
        string projectReferences
    )
    {
        var csBlock = new TextBlock();
        var razorBlock = new TextBlock();

        foreach (var path in Directory.GetFiles(folderToPack, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".cs" || ext == ".razor")
            {
                var tf = new TextFile
                {
                    LocalPath = MakeRelativePath(folderToPack, path),
                    Text = File.ReadAllText(path)
                };
                if (ext == ".cs") csBlock.TextFiles.Add(tf);
                else razorBlock.TextFiles.Add(tf);
            }
        }

        var cll = new CllFile
        {
            PackageIdent = packageIdent,
            CompilerSettings = compilerSettings,
            ProjectReferences = projectReferences,
            TextBlocks = new List<TextBlock> { csBlock, razorBlock }
        };

        var cllBytes = BuildCllBytes(cll);
        File.WriteAllBytes(outputCllFile, cllBytes);
    }

    private static bool IsCllFile(byte[] data)
    {
        if (data.Length < 8) return false;
        var magic = BitConverter.ToInt32(data, 4);
        return magic == MAGIC_VALUE;
    }

    private static byte[] TryDecompressAsGZip(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return data;
        }
    }

    private static CllFile ParseCll(byte[] data)
    {
        using var ms = new MemoryStream(data, false);
        ms.Seek(16, SeekOrigin.Begin);

        var pkg = ms.ReadLengthPrependedAsciiString() ?? "";
        var comp = ms.ReadLengthPrependedAsciiString() ?? "";
        var proj = ms.ReadLengthPrependedAsciiString() ?? "";

        var csRaw = ms.ReadLengthPrependedAsciiString() ?? "[]";
        var csBlock = JsonSerializer.Deserialize<TextBlock>($"{{\"TextFiles\":{csRaw}}}")
                      ?? new TextBlock();

        var razorRaw = ms.ReadLengthPrependedAsciiString() ?? "[]";
        var razorBlock = JsonSerializer.Deserialize<TextBlock>($"{{\"TextFiles\":{razorRaw}}}")
                         ?? new TextBlock();

        return new CllFile
        {
            PackageIdent = pkg,
            CompilerSettings = comp,
            ProjectReferences = proj,
            TextBlocks = new List<TextBlock> { csBlock, razorBlock }
        };
    }

    private static byte[] BuildCllBytes(CllFile cll)
    {
        using var ms = new MemoryStream();

        ms.WriteInt32(0);
        ms.WriteInt32(MAGIC_VALUE);
        ms.WriteInt32(1005);
        ms.WriteInt32(0);

        ms.WriteLengthPrependedAsciiString(cll.PackageIdent);
        ms.WriteLengthPrependedAsciiString(cll.CompilerSettings);
        ms.WriteLengthPrependedAsciiString(cll.ProjectReferences);

        var cs = cll.TextBlocks.Count > 0 ? cll.TextBlocks[0] : new TextBlock();
        var csArray = JsonSerializer.Serialize(cs.TextFiles);
        ms.WriteLengthPrependedAsciiString(csArray);

        var rz = cll.TextBlocks.Count > 1 ? cll.TextBlocks[1] : new TextBlock();
        var rzArray = JsonSerializer.Serialize(rz.TextFiles);
        ms.WriteLengthPrependedAsciiString(rzArray);

        return ms.ToArray();
    }

    public static string MakeRelativePath(string baseDir, string filePath)
    {
        var baseUri = new Uri(baseDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fileUri = new Uri(filePath, UriKind.Absolute);
        var relUri = baseUri.MakeRelativeUri(fileUri);
        return Uri.UnescapeDataString(relUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }
}