namespace csharp_codegen.Models;

/// <summary>
/// ルートの config.yaml のうち csharp_codegen / csharp_converter セクションを読む
/// (後者は csharp_converter プロジェクトが本クラスをプロジェクト参照して使う)。
/// </summary>
public sealed class PipelineConfig
{
    public CSharpCodegenConfig CsharpCodegen { get; set; } = new();
    public CSharpConverterConfig CsharpConverter { get; set; } = new();
}

public sealed class CSharpCodegenConfig
{
    public string TablesDir { get; set; } = "";
    public string EnumsDir { get; set; } = "";
    public string OutputDir { get; set; } = "";
    public string Namespace { get; set; } = "";
}

public sealed class CSharpConverterConfig
{
    public string TablesDir { get; set; } = "";
    public string CsvDir { get; set; } = "";
    public string GeneratedCsharpDir { get; set; } = "";
    public string OutputPath { get; set; } = "";
}
