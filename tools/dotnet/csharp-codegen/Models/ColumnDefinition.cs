namespace csharp_codegen.Models;

/// <summary>
/// schema/tables/*.yaml の fields[] 1件分。セカンダリキーは無いため primary_key のみを持つ。
/// </summary>
public sealed class ColumnDefinition
{
    private static readonly string[] DefaultTargets = ["client", "server"];

    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? EnumType { get; set; }
    public bool PrimaryKey { get; set; }
    public List<string>? Targets { get; set; }

    /// <summary>targets省略時は [client, server] がデフォルト。</summary>
    public IReadOnlyList<string> ResolvedTargets =>
        Targets is { Count: > 0 } ? Targets : DefaultTargets;

    public bool IsClientTarget => ResolvedTargets.Contains("client");
}
