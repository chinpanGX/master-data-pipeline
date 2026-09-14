using System.Security.Cryptography;
using System.Text;
using csharp_codegen.Models;

namespace csharp_codegen.Generators;

/// <summary>
/// schema/tables/*.yaml + schema/enums/*.yaml の内容だけから決定的に算出するハッシュ
/// (content_hash)。テーブル構造・Enum定義が変わらない限り常に同じ値になり、暗号化
/// パスワードとして csharp_converter (Phase 4) にもそのまま使われる。
///
/// あくまで「スキーマの指紋」であり、CSVの実データは対象に含めない
/// (実データはスキーマを変えずに更新されることが多く、その都度パスワード=content_hashが
/// 変わってしまうと運用上不便なため)。schemaを変更した場合のみ値が変わり、
/// 結果として古い masterdata.bytes は新しい MasterDataLoader では復号できなくなる
/// (想定どおりの挙動)。
/// </summary>
public static class ContentHashCalculator
{
    public static string Compute(IReadOnlyList<TableDefinition> tables, IReadOnlyList<EnumDefinition> enums)
    {
        var sb = new StringBuilder();

        foreach (var table in tables.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            sb.Append("table:").Append(table.Name).Append(';');
            foreach (var field in table.Fields)
            {
                sb.Append(field.Name).Append(':').Append(field.Type);
                if (field.EnumType is not null)
                {
                    sb.Append('<').Append(field.EnumType).Append('>');
                }

                if (field.PrimaryKey)
                {
                    sb.Append("!pk");
                }

                sb.Append(':').Append(string.Join(',', field.ResolvedTargets));
                sb.Append(';');
            }
        }

        foreach (var enumDefinition in enums.OrderBy(e => e.Enum, StringComparer.Ordinal))
        {
            sb.Append("enum:").Append(enumDefinition.Enum).Append(';');
            foreach (var member in enumDefinition.Members.OrderBy(m => m.Id))
            {
                sb.Append(member.Id).Append('=').Append(member.Name).Append(';');
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
