namespace csharp_codegen.Models;

/// <summary>schema/tables/*.yaml 1ファイル分。</summary>
public sealed class TableDefinition
{
    public string Name { get; set; } = "";
    public string InputCsv { get; set; } = "";
    public List<ColumnDefinition> Fields { get; set; } = [];
}
