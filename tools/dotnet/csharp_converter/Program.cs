using System.Collections;
using System.Globalization;
using System.Reflection;
using csharp_codegen;
using csharp_codegen.Generators;
using csharp_codegen.Models;
using CsvHelper;

try
{
    var repoRoot = SchemaLoader.FindRepoRoot();
    var config = SchemaLoader.LoadConfig(repoRoot);
    var rootNamespace = config.CsharpCodegen.Namespace;
    var converterConfig = config.CsharpConverter;

    var tablesDir = Path.Combine(repoRoot, converterConfig.TablesDir);
    var csvDir = Path.Combine(repoRoot, converterConfig.CsvDir);
    var generatedCsharpDir = Path.Combine(repoRoot, converterConfig.GeneratedCsharpDir);
    var outputPath = Path.Combine(repoRoot, converterConfig.OutputPath);

    var tables = SchemaLoader.LoadTables(tablesDir);
    var assembly = Assembly.GetExecutingAssembly();

    // DatabaseBuilder / AesCrypto は MasterMemory の Source Generator / csharp-codegen が
    // config.yaml の csharp_codegen.namespace(= rootNamespace)と同名の名前空間に生成する
    // (プロジェクトの RootNamespace をこの値と一致させてある。csharp_converter.csproj参照)。
    // 型名を1つもソースコードに固定で書かないよう、ここも他のPOCO/Enum解決と同様に
    // すべてリフレクションで解決する。
    var builderType = assembly.GetType($"{rootNamespace}.DatabaseBuilder") ??
        throw new InvalidOperationException(
            $"DatabaseBuilder型が見つかりません: {rootNamespace}.DatabaseBuilder" +
            "(csharp_converter.csprojのRootNamespaceがconfig.yamlのnamespaceと一致しているか確認してください)");
    var builder = Activator.CreateInstance(builderType)!;

    foreach (var table in tables)
    {
        var className = NameConversion.ToPascalCase(table.Name);
        var pocoType = assembly.GetType($"{rootNamespace}.Models.{className}") ??
            throw new InvalidOperationException(
                $"POCO型が見つかりません: {rootNamespace}.Models.{className}(先にcsharp-codegenを実行してください)");

        var clientFields = table.Fields.Where(f => f.IsClientTarget).ToArray();
        var rows = LoadRows(csvDir, table, clientFields, pocoType, rootNamespace, assembly);

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(pocoType);
        var appendMethod = builderType.GetMethod("Append", [enumerableType]) ??
            throw new InvalidOperationException($"DatabaseBuilder.Append({pocoType.Name}) が見つかりません");
        appendMethod.Invoke(builder, [rows]);

        Console.WriteLine($"appended: {table.Name} ({rows.Count}行)");
    }

    var databaseBinary = (byte[])builderType.GetMethod("Build")!.Invoke(builder, null)!;

    var contentHashPath = Path.Combine(generatedCsharpDir, "content_hash.txt");
    if (!File.Exists(contentHashPath))
    {
        throw new FileNotFoundException(
            $"content_hashが見つかりません: {contentHashPath}(先にcsharp-codegenを実行してください)");
    }

    var contentHash = File.ReadAllText(contentHashPath).Trim();

    var aesCryptoType = assembly.GetType($"{rootNamespace}.AesCrypto") ??
        throw new InvalidOperationException($"AesCrypto型が見つかりません: {rootNamespace}.AesCrypto");
    var encryptMethod = aesCryptoType.GetMethod("Encrypt", [typeof(byte[]), typeof(string)])!;
    var encrypted = (byte[])encryptMethod.Invoke(null, [databaseBinary, contentHash])!;

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllBytes(outputPath, encrypted);
    Console.WriteLine($"generated: {outputPath}({encrypted.Length} bytes, content_hash={contentHash})");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"エラー: {ex.Message}");
    return 1;
}

static IList LoadRows(
    string csvDir,
    TableDefinition table,
    ColumnDefinition[] clientFields,
    Type pocoType,
    string rootNamespace,
    Assembly assembly)
{
    var csvPath = Path.Combine(csvDir, $"{table.InputCsv}.csv");
    if (!File.Exists(csvPath))
    {
        throw new FileNotFoundException(
            $"正規化CSVが見つかりません: {csvPath}(先にnormalize_csv.py等の前処理を実行してください)");
    }

    var listType = typeof(List<>).MakeGenericType(pocoType);
    var list = (IList)Activator.CreateInstance(listType)!;

    using var reader = new StreamReader(csvPath);
    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
    csv.Read();
    csv.ReadHeader();

    while (csv.Read())
    {
        var instance = Activator.CreateInstance(pocoType)!;
        foreach (var field in clientFields)
        {
            var raw = csv.GetField(field.Name) ?? "";
            var propertyName = NameConversion.ToPascalCase(field.Name);
            var property = pocoType.GetProperty(propertyName) ??
                throw new InvalidOperationException($"{pocoType.Name}にプロパティ'{propertyName}'がありません");
            property.SetValue(instance, ConvertValue(raw, field, rootNamespace, assembly));
        }

        list.Add(instance);
    }

    return list;
}

static object ConvertValue(string raw, ColumnDefinition field, string rootNamespace, Assembly assembly)
{
    return field.Type switch
    {
        "int" => int.Parse(raw, CultureInfo.InvariantCulture),
        "string" => raw,
        "bool" => ParseBool(raw),
        "enum" => ParseEnum(raw, field.EnumType!, rootNamespace, assembly),
        _ => throw new InvalidOperationException($"未対応の型です: {field.Type}"),
    };
}

// bool表記ゆれの正規化(ハードコード)。Google Sheetsのチェックボックスは "TRUE"/"FALSE" を
// 出力するが、手入力での大小文字ゆれ・"1"/"0"表記も許容する。
static bool ParseBool(string raw) => raw.Trim().ToUpperInvariant() switch
{
    "TRUE" or "1" => true,
    "FALSE" or "0" or "" => false,
    _ => throw new InvalidOperationException($"bool値として解釈できません: '{raw}'"),
};

static object ParseEnum(string raw, string enumTypeName, string rootNamespace, Assembly assembly)
{
    var enumType = assembly.GetType($"{rootNamespace}.Enums.{enumTypeName}") ??
        throw new InvalidOperationException($"Enum型が見つかりません: {rootNamespace}.Enums.{enumTypeName}");
    return Enum.ToObject(enumType, int.Parse(raw, CultureInfo.InvariantCulture));
}
