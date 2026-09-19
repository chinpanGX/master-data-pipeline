namespace csharp_codegen.Generators;

/// <summary>
/// MasterDataLoader.cs を生成する。Roslyn構文木ではなく固定テンプレートに content_hash を
/// 埋め込む方式(復号+MemoryDatabase構築ロジックはUnity/MagicOnionで完全に共通のため、
/// テーブルごとの構造に依存しない)。
///
/// MasterMemory 3.x / MessagePack 3.x は Source Generator方式で、コンパイル対象
/// アセンブリ内に MemoryDatabase / DatabaseBuilder / MasterMemoryResolver を生成する
/// (本ツールの生成物ではない)。これらは同一アセンブリ内でのみアクセス可能なため、
/// [MemoryTable]/[MessagePackObject]のPOCOモデルとMasterDataLoaderは必ず同じアセンブリ
/// (同じrootNamespace)に配置する(MasterMemory公式READMEのco-location方針。
/// Domain/Infrastructureのようなレイヤー分割で別アセンブリに分けることはできない)。
///
/// MessagePack側のSource Generator出力(internal公開の GeneratedMessagePackResolver)は
/// 直接参照しない。MessagePack公式ドキュメントの通り、StandardResolverが内部で
/// SourceGeneratedFormatterResolver経由でSource Generator出力を自動的に含むため、
/// StandardResolver.Instanceを合成するだけでよい。
///
/// 復号には同じ出力ディレクトリに生成される AesCrypto.cs(<see cref="AesCryptoGenerator"/>)を
/// そのまま使う。csharp_converter(Phase 4)側の暗号化と実装を共有するための、
/// pipeline内で完結する直接参照。
/// </summary>
public static class MasterDataLoaderGenerator
{
    public static string Generate(string rootNamespace, string contentHash)
    {
        return $$"""
            {{GeneratedFileHeader.Text}}using MasterMemory;
            using MessagePack;
            using MessagePack.Resolvers;

            namespace {{rootNamespace}}
            {
                /// <summary>
                /// masterdata.bytes の復号 + MemoryDatabase構築ロジック。
                /// Unity(クライアント)・MagicOnion(リアルタイムサーバー)の両方に同一の
                /// ファイルとしてコピーされる(bytesの取得方法だけが環境ごとに異なる)。
                /// </summary>
                public static class MasterDataLoader
                {
                    /// <summary>
                    /// schema/tables, schema/enums の内容から算出したcontent_hash。
                    /// 復号パスワードとして使う。スキーマが変わるとこの値も変わり、
                    /// 古い masterdata.bytes は復号できなくなる(想定どおりの挙動)。
                    /// </summary>
                    public const string ContentHash = "{{contentHash}}";

                    // MasterMemoryResolver / MemoryDatabase はこのプロジェクトのビルド時に
                    // MasterMemory.SourceGenerator が生成する(同一名前空間に存在する前提)。
                    // StandardResolverはMessagePackのSource Generator出力を内部で
                    // 自動的に含むため、POCO([MessagePackObject])用のResolverを
                    // 個別に参照する必要はない。
                    private static readonly IFormatterResolver Resolver =
                        CompositeResolver.Create(
                            MasterMemoryResolver.Instance,
                            StandardResolver.Instance);

                    public static MemoryDatabase Load(byte[] encryptedBytes)
                    {
                        var decrypted = AesCrypto.Decrypt(encryptedBytes, ContentHash);
                        return new MemoryDatabase(decrypted, formatterResolver: Resolver);
                    }
                }
            }
            """;
    }
}
