namespace SboxCllGui;

public class CllFile
{
    public string PackageIdent { get; set; } = "";
    public string CompilerSettings { get; set; } = "";
    public string ProjectReferences { get; set; } = "";
    public List<TextBlock> TextBlocks { get; set; } = [];
}

public class TextBlock
{
    public List<TextFile> TextFiles { get; set; } = [];
}

public class TextFile
{
    public string? Text { get; set; }
    public string? LocalPath { get; set; }
}