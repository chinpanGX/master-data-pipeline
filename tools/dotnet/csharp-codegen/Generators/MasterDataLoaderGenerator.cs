namespace csharp_codegen.Generators;

/// <summary>
/// MasterDataLoader.cs を生成する。Roslyn構文木ではなく固定テンプレートに content_hash を
/// 埋め込む方式(復号+MemoryDatabase構築ロジックはUnity/MagicOnionで完全に共通のため、
/// テーブルごとの構造に依存しない)。
///
/// MasterMemory 3.x は Source Generator方式で、コンパイル対象プロジェクトの
/// RootNamespace 上に MemoryDatabase / DatabaseBuilder / MasterMemoryResolver を
/// 生成する(本ツールの生成物ではない)。そのため、このファイルをコピーする
/// client / realtime_server 側のプロジェクトは、RootNamespace を config.yaml の
/// csharp_codegen.namespace(= rootNamespace引数)と一致させる必要がある(Phase 8で設定)。
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
                    // GeneratedMessagePackResolver は MessagePack.SourceGenerator が
                    // POCO([MessagePackObject])用に生成するアセンブリ固定名前空間のResolver。
                    private static readonly IFormatterResolver Resolver =
                        CompositeResolver.Create(
                            MasterMemoryResolver.Instance,
                            MessagePack.GeneratedMessagePackResolver.Instance,
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
