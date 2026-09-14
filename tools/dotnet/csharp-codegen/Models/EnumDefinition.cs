namespace csharp_codegen.Models;

/// <summary>schema/enums/*.yaml 1ファイル分。</summary>
public sealed class EnumDefinition
{
    /// <summary>yaml上のキーは "enum"。C#生成物のenum型名になる。</summary>
    public string Enum { get; set; } = "";

    public List<EnumMemberDefinition> Members { get; set; } = [];
}

public sealed class EnumMemberDefinition
{
    /// <summary>出力される数値。並べ替えても変わらない固定値。</summary>
    public int Id { get; set; }

    /// <summary>スプレッドシート上で入力される名前。</summary>
    public string Name { get; set; } = "";
}
