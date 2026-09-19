using csharp_codegen;
using csharp_codegen.Generators;

try
{
    var repoRoot = SchemaLoader.FindRepoRoot();
    var config = SchemaLoader.LoadConfig(repoRoot).CsharpCodegen;

    var tablesDir = Path.Combine(repoRoot, config.TablesDir);
    var enumsDir = Path.Combine(repoRoot, config.EnumsDir);
    var outputDir = Path.Combine(repoRoot, config.OutputDir);

    var tables = SchemaLoader.LoadTables(tablesDir);
    var enums = SchemaLoader.LoadEnums(enumsDir);

    CleanDir(outputDir);
    var modelsDir = Directory.CreateDirectory(Path.Combine(outputDir, "Models")).FullName;
    var enumsOutDir = Directory.CreateDirectory(Path.Combine(outputDir, "Enums")).FullName;

    foreach (var enumDefinition in enums)
    {
        var source = EnumGenerator.Generate(enumDefinition, config.Namespace);
        File.WriteAllText(Path.Combine(enumsOutDir, $"{enumDefinition.Enum}.cs"), source);
        Console.WriteLine($"generated: Enums/{enumDefinition.Enum}.cs");
    }

    foreach (var table in tables.Where(t => t.IsClientTarget))
    {
        var className = PocoClassGenerator.GetClassName(table);
        var source = PocoClassGenerator.Generate(table, config.Namespace);
        File.WriteAllText(Path.Combine(modelsDir, $"{className}.cs"), source);
        Console.WriteLine($"generated: Models/{className}.cs");
    }

    var aesCryptoSource = AesCryptoGenerator.Generate(config.Namespace);
    File.WriteAllText(Path.Combine(outputDir, "AesCrypto.cs"), aesCryptoSource);
    Console.WriteLine("generated: AesCrypto.cs");

    var contentHash = ContentHashCalculator.Compute(tables, enums);
    var loaderSource = MasterDataLoaderGenerator.Generate(config.Namespace, contentHash);
    File.WriteAllText(Path.Combine(outputDir, "MasterDataLoader.cs"), loaderSource);
    Console.WriteLine("generated: MasterDataLoader.cs");

    // csharp_converter(Phase 4)が同一のcontent_hashを暗号化パスワードとして再利用するための
    // 副産物。out/generated_csharp/*.cs と同様、pipeline内で完結する直接参照として読ませる。
    File.WriteAllText(Path.Combine(outputDir, "content_hash.txt"), contentHash);
    Console.WriteLine($"content_hash: {contentHash}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"エラー: {ex.Message}");
    return 1;
}

static void CleanDir(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }

    Directory.CreateDirectory(path);
}
