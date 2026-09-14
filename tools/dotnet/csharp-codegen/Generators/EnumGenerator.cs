using csharp_codegen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace csharp_codegen.Generators;

/// <summary>schema/enums/*.yaml 1件から Roslyn構文木で enum の .cs ソースを組み立てる。</summary>
public static class EnumGenerator
{
    public static string Generate(EnumDefinition enumDefinition, string rootNamespace)
    {
        var members = enumDefinition.Members
            .Select(member => EnumMemberDeclaration(member.Name)
                .WithEqualsValue(EqualsValueClause(
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(member.Id)))))
            .ToArray();

        var enumDeclaration = EnumDeclaration(enumDefinition.Enum)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddMembers(members);

        var namespaceDeclaration = NamespaceDeclaration(ParseName($"{rootNamespace}.Enums"))
            .AddMembers(enumDeclaration);

        var compilationUnit = CompilationUnit()
            .AddMembers(namespaceDeclaration)
            .NormalizeWhitespace();

        return GeneratedFileHeader.Text + compilationUnit.ToFullString() + Environment.NewLine;
    }
}
