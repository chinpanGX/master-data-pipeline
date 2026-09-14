using csharp_codegen.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace csharp_codegen;

/// <summary>
/// config.yaml / schema/tables/*.yaml / schema/enums/*.yaml の読み込みを担当する。
/// </summary>
public static class SchemaLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>AppContext.BaseDirectory から上に遡って config.yaml を探す。</summary>
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "config.yaml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"config.yaml が見つかりません(探索起点: {AppContext.BaseDirectory})");
    }

    public static PipelineConfig LoadConfig(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "config.yaml");
        using var reader = new StreamReader(path);
        return Deserializer.Deserialize<PipelineConfig>(reader) ??
               throw new InvalidOperationException($"{path} の読み込みに失敗しました");
    }

    public static List<TableDefinition> LoadTables(string tablesDir)
    {
        return LoadYamlDir<TableDefinition>(tablesDir);
    }

    public static List<EnumDefinition> LoadEnums(string enumsDir)
    {
        return LoadYamlDir<EnumDefinition>(enumsDir);
    }

    private static List<TDefinition> LoadYamlDir<TDefinition>(string dir)
    {
        if (!Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException($"スキーマディレクトリが見つかりません: {dir}");
        }

        var results = new List<TDefinition>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.yaml").OrderBy(f => f, StringComparer.Ordinal))
        {
            using var reader = new StreamReader(file);
            var value = Deserializer.Deserialize<TDefinition>(reader);
            if (value is null)
            {
                throw new InvalidOperationException($"{file} の読み込みに失敗しました");
            }

            results.Add(value);
        }

        return results;
    }
}
