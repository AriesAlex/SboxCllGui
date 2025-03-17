using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace SboxCllGui;

public partial class MainWindow
{
    private const int MAGIC_VALUE = 0x41434D47; // "GMCA" in little-endian

    public MainWindow()
    {
        InitializeComponent();
    }

    //============================================================
    // Button Clicks
    //============================================================

    private void BtnExtract_Click(object sender, RoutedEventArgs e)
    {
        var cllPath = PickFileToOpen("CLL Files|*.cll|All Files|*.*");
        if (string.IsNullOrEmpty(cllPath)) return;

        var folderPath = PickFolder();
        if (string.IsNullOrEmpty(folderPath)) return;

        try
        {
            var cll = ExtractCll(cllPath, folderPath);
            // Fill metadata fields automatically
            TxtPackageIdent.Text = cll.PackageIdent;
            TxtCompilerSettings.Text = cll.CompilerSettings;
            TxtProjectReferences.Text = cll.ProjectReferences;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPack_Click(object sender, RoutedEventArgs e)
    {
        var folderToPack = PickFolder();
        if (string.IsNullOrEmpty(folderToPack)) return;

        var outputCllFile = PickFileToSave("CLL Files|*.cll|All Files|*.*", "my_package.cll");
        if (string.IsNullOrEmpty(outputCllFile)) return;

        try
        {
            PackCll(
                folderToPack,
                outputCllFile,
                TxtPackageIdent.Text,
                TxtCompilerSettings.Text,
                TxtProjectReferences.Text
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    //============================================================
    // Dialogs (no System.Windows.Forms)
    //============================================================

    private string? PickFileToOpen(string filter)
    {
        var dlg = new OpenFileDialog { Filter = filter };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private string? PickFileToSave(string filter, string defaultFileName)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = defaultFileName };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    // "Trick" to pick a folder with WPF: user "opens" a fake file in that folder.
    private string? PickFolder()
    {
        var dlg = new OpenFileDialog
        {
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            FileName = "SelectThisFolder"
        };
        return dlg.ShowDialog() == true ? Path.GetDirectoryName(dlg.FileName) : null;
    }

    //============================================================
    // CLL Extraction
    //============================================================

    private CllFile ExtractCll(string cllPath, string outputFolder)
    {
        var bytes = File.ReadAllBytes(cllPath);

        // If not recognized as CLL, try GZip
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

    //============================================================
    // CLL Packing
    //============================================================

    private void PackCll(
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

    //============================================================
    // Helpers
    //============================================================

    private bool IsCllFile(byte[] data)
    {
        if (data.Length < 8) return false;
        var magic = BitConverter.ToInt32(data, 4);
        return magic == MAGIC_VALUE;
    }

    private byte[] TryDecompressAsGZip(byte[] data)
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

    private CllFile ParseCll(byte[] data)
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

    private byte[] BuildCllBytes(CllFile cll)
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

    private string MakeRelativePath(string baseDir, string filePath)
    {
        var baseUri = new Uri(baseDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fileUri = new Uri(filePath, UriKind.Absolute);
        var relUri = baseUri.MakeRelativeUri(fileUri);
        return Uri.UnescapeDataString(relUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }
}

//============================================================
// Models
//============================================================

public class CllFile
{
    public string PackageIdent { get; set; } = "";
    public string CompilerSettings { get; set; } = "";
    public string ProjectReferences { get; set; } = "";

    public List<TextBlock> TextBlocks { get; set; } = new();
}

public class TextBlock
{
    public List<TextFile> TextFiles { get; set; } = new();
}

public class TextFile
{
    public string? Text { get; set; }
    public string? LocalPath { get; set; }
}

//============================================================
// Stream Extensions
//============================================================
public static class MemoryStreamExtensions
{
    public static void WriteInt32(this MemoryStream ms, int value)
    {
        var data = BitConverter.GetBytes(value);
        ms.Write(data, 0, data.Length);
    }

    public static void WriteLengthPrependedAsciiString(this MemoryStream ms, string? value)
    {
        if (value == null) value = "";
        var bytes = Encoding.ASCII.GetBytes(value);
        ms.WriteInt32(bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }

    public static int? ReadInt32(this MemoryStream ms)
    {
        if (ms.Position + 4 > ms.Length) return null;
        var buf = new byte[4];
        ms.Read(buf, 0, 4);
        return BitConverter.ToInt32(buf, 0);
    }

    public static string? ReadLengthPrependedAsciiString(this MemoryStream ms)
    {
        var len = ms.ReadInt32();
        if (len == null || len < 0) return null;
        if (ms.Position + len > ms.Length) return null;

        var buf = new byte[len.Value];
        ms.Read(buf, 0, len.Value);
        return Encoding.ASCII.GetString(buf);
    }
}