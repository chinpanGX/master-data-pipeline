using csharp_codegen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace csharp_codegen.Generators;

/// <summary>
/// schema/tables/*.yaml 1件から Roslyn構文木で MasterMemory 用POCOクラスの .cs ソースを組み立てる。
/// targets に "client" を含む列だけを抽出する(サーバー専用列は出力しない)。
/// [MemoryTable] [MessagePackObject(true)] をクラスに、[PrimaryKey] を該当プロパティに付与する
/// ([SecondaryKey]は生成しない)。
/// </summary>
public static class PocoClassGenerator
{
    /// <summary>
    /// 生成するクラス名(テーブル名のPascalCase + "Data")。MasterMemoryのSource Generatorが
    /// このクラス名を元に "{ClassName}Table" というテーブルアクセサを自動生成するため、
    /// 結果的に "○○DataTable" という命名になる。
    /// </summary>
    public static string GetClassName(TableDefinition table) => NameConversion.ToPascalCase(table.Name) + "Data";

    public static string Generate(TableDefinition table, string rootNamespace)
    {
        var className = GetClassName(table);
        var clientFields = table.Fields.Where(f => f.IsClientTarget).ToArray();

        var properties = clientFields.Select(BuildProperty).ToArray();

        var classDeclaration = ClassDeclaration(className)
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.PartialKeyword))
            .AddAttributeLists(
                AttributeList(SingletonSeparatedList(
                    Attribute(ParseName("MemoryTable"))
                        .AddArgumentListArguments(AttributeArgument(
                            LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(table.Name)))))),
                AttributeList(SingletonSeparatedList(
                    Attribute(ParseName("MessagePackObject"))
                        .AddArgumentListArguments(AttributeArgument(
                            LiteralExpression(SyntaxKind.TrueLiteralExpression))))))
            .AddMembers(properties);

        var namespaceDeclaration = NamespaceDeclaration(ParseName($"{rootNamespace}.Models"))
            .AddUsings(
                UsingDirective(ParseName("MasterMemory")),
                UsingDirective(ParseName("MessagePack")),
                UsingDirective(ParseName($"{rootNamespace}.Enums")))
            .AddMembers(classDeclaration);

        var compilationUnit = CompilationUnit()
            .AddMembers(namespaceDeclaration)
            .NormalizeWhitespace();

        return GeneratedFileHeader.Text + compilationUnit.ToFullString() + Environment.NewLine;
    }

    private static PropertyDeclarationSyntax BuildProperty(ColumnDefinition field)
    {
        var typeName = NameConversion.ResolveCSharpType(field.Type, field.EnumType);
        var propertyName = NameConversion.ToPascalCase(field.Name);

        var property = PropertyDeclaration(ParseTypeName(typeName), propertyName)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        if (field.PrimaryKey)
        {
            property = property.AddAttributeLists(
                AttributeList(SingletonSeparatedList(Attribute(ParseName("PrimaryKey")))));
        }

        return property;
    }
}
