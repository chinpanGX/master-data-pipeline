namespace csharp_codegen.Generators;

/// <summary>
/// AesCrypto.cs を生成する。content_hashをパスワードとしたAES-CBC(PBKDF2キー導出)による
/// 暗号化/復号の固定テンプレート(Roslyn構文木ではなく文字列埋め込み)。
/// csharp_converter(暗号化側)と MasterDataLoader.cs(復号側)の両方が、この1つの実装を
/// 直接参照する(pipeline内で完結する参照のため直接参照、複製しない)。
///
/// 以前はAesGcm(AES-GCM)を使っていたが、一部のUnityプラットフォーム(IL2CPPビルドの実機)で
/// ネイティブのAEAD実装が無く PlatformNotSupportedException になり動作しなかったため、
/// ネイティブAEADに依存しないAes(CBCモード)+ Rfc2898DeriveBytes(PBKDF2)に変更した
/// (Supplement(Atlas/Supplement)のCryptography実装と同じ方式。ただしmaster-data-pipelineは
/// Supplementに依存させず、この生成テンプレート内で完結させている)。
/// </summary>
public static class AesCryptoGenerator
{
    public static string Generate(string rootNamespace)
    {
        return $$"""
            {{GeneratedFileHeader.Text}}using System;
            using System.Security.Cryptography;

            namespace {{rootNamespace}}
            {
                public static class AesCrypto
                {
                    // フォーマット: [ Salt(16) | IV(16) | Cipher ]
                    private const int KeySizeInBytes = 16; // AES-128
                    private const int SaltSizeInBytes = 16;
                    private const int Pbkdf2IterationCount = 1000;
                    private static readonly HashAlgorithmName Pbkdf2HashAlgorithm = HashAlgorithmName.SHA256;

                    public static byte[] Encrypt(byte[] plainData, string password)
                    {
                        var salt = new byte[SaltSizeInBytes];
                        RandomNumberGenerator.Fill(salt);

                        using var aes = Aes.Create();
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = DeriveKey(password, salt);
                        aes.GenerateIV();

                        using var encryptor = aes.CreateEncryptor();
                        var cipherText = encryptor.TransformFinalBlock(plainData, 0, plainData.Length);

                        var result = new byte[SaltSizeInBytes + aes.IV.Length + cipherText.Length];
                        Buffer.BlockCopy(salt, 0, result, 0, SaltSizeInBytes);
                        Buffer.BlockCopy(aes.IV, 0, result, SaltSizeInBytes, aes.IV.Length);
                        Buffer.BlockCopy(cipherText, 0, result, SaltSizeInBytes + aes.IV.Length, cipherText.Length);
                        return result;
                    }

                    public static byte[] Decrypt(byte[] encryptedData, string password)
                    {
                        using var aes = Aes.Create();
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        var salt = new byte[SaltSizeInBytes];
                        Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSizeInBytes);

                        var iv = new byte[aes.IV.Length];
                        Buffer.BlockCopy(encryptedData, SaltSizeInBytes, iv, 0, iv.Length);
                        aes.IV = iv;

                        aes.Key = DeriveKey(password, salt);

                        var cipherOffset = SaltSizeInBytes + iv.Length;
                        var cipherLength = encryptedData.Length - cipherOffset;
                        var cipherText = new byte[cipherLength];
                        Buffer.BlockCopy(encryptedData, cipherOffset, cipherText, 0, cipherLength);

                        using var decryptor = aes.CreateDecryptor();
                        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                    }

                    private static byte[] DeriveKey(string password, byte[] salt)
                    {
                        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, Pbkdf2IterationCount, Pbkdf2HashAlgorithm);
                        return deriveBytes.GetBytes(KeySizeInBytes);
                    }
                }
            }
            """;
    }
}
