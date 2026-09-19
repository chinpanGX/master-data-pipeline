namespace csharp_codegen.Models;

/// <summary>schema/tables/*.yaml 1ファイル分。</summary>
public sealed class TableDefinition
{
    private static readonly string[] DefaultTargets = ["client", "server"];

    public string Name { get; set; } = "";
    public string InputCsv { get; set; } = "";
    public List<ColumnDefinition> Fields { get; set; } = [];

    /// <summary>
    /// テーブル単位でclient(Unity/MagicOnion) / server(Rust API)のどちらに出力するかを制御する。
    /// 省略時は[client, server](両方)。DB保存・check_relationの検証対象としては残しつつ、
    /// クライアントのMemoryTableとしては生成したくないテーブル(例: 参照専用のグループ定義)に使う。
    /// </summary>
    public List<string>? Targets { get; set; }

    /// <summary>targets省略時は [client, server] がデフォルト。</summary>
    public IReadOnlyList<string> ResolvedTargets =>
        Targets is { Count: > 0 } ? Targets : DefaultTargets;

    public bool IsClientTarget => ResolvedTargets.Contains("client");
}
