namespace csharp_codegen.Generators;

/// <summary>
/// AesCrypto.cs を生成する。content_hashをパスワードとしたAES-256-GCMによる暗号化/復号の
/// 固定テンプレート(Roslyn構文木ではなく文字列埋め込み)。
/// csharp_converter(暗号化側)と MasterDataLoader.cs(復号側)の両方が、この1つの実装を
/// 直接参照する(pipeline内で完結する参照のため直接参照、複製しない)。
/// </summary>
public static class AesCryptoGenerator
{
    public static string Generate(string rootNamespace)
    {
        return $$"""
            {{GeneratedFileHeader.Text}}using System.Security.Cryptography;
            using System.Text;

            namespace {{rootNamespace}}
            {
                public static class AesCrypto
                {
                    private const int NonceSize = 12;
                    private const int TagSize = 16;

                    public static byte[] Encrypt(byte[] plainData, string password)
                    {
                        var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                        var nonce = new byte[NonceSize];
                        RandomNumberGenerator.Fill(nonce);

                        var cipherText = new byte[plainData.Length];
                        var tag = new byte[TagSize];

                        using var aesGcm = new AesGcm(key, TagSize);
                        aesGcm.Encrypt(nonce, plainData, cipherText, tag);

                        var result = new byte[NonceSize + TagSize + cipherText.Length];
                        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
                        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
                        Buffer.BlockCopy(cipherText, 0, result, NonceSize + TagSize, cipherText.Length);
                        return result;
                    }

                    public static byte[] Decrypt(byte[] encryptedData, string password)
                    {
                        var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                        var nonce = new byte[NonceSize];
                        var tag = new byte[TagSize];
                        var cipherTextLength = encryptedData.Length - NonceSize - TagSize;
                        var cipherText = new byte[cipherTextLength];

                        Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
                        Buffer.BlockCopy(encryptedData, NonceSize, tag, 0, TagSize);
                        Buffer.BlockCopy(encryptedData, NonceSize + TagSize, cipherText, 0, cipherTextLength);

                        var plainData = new byte[cipherTextLength];
                        using var aesGcm = new AesGcm(key, TagSize);
                        aesGcm.Decrypt(nonce, cipherText, tag, plainData);
                        return plainData;
                    }
                }
            }
            """;
    }
}
