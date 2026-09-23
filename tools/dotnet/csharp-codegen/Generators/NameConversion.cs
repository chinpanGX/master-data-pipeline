using System.Globalization;
using System.Text;

namespace csharp_codegen.Generators;

/// <summary>
/// スキーマ上の snake_case な名前(テーブル名・列名)を C# の識別子(PascalCase)に変換する。
/// </summary>
public static class NameConversion
{
    public static string ToPascalCase(string snakeCase)
    {
        var parts = snakeCase.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(char.ToUpper(part[0], CultureInfo.InvariantCulture));
            if (part.Length > 1)
            {
                sb.Append(part[1..]);
            }
        }

        return sb.ToString();
    }

    /// <summary>type: int/long/string/bool/enum を C# の型名に解決する。</summary>
    public static string ResolveCSharpType(string type, string? enumType)
    {
        return type switch
        {
            "int" => "int",
            "long" => "long",
            "string" => "string",
            "bool" => "bool",
            "enum" => enumType ?? throw new InvalidOperationException(
                "type: enum のフィールドには enum_type の指定が必須です"),
            _ => throw new InvalidOperationException($"未対応の型です: {type}"),
        };
    }
}
