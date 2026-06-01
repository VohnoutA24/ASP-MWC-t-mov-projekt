using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace sum.Services
{
    public static class EncryptionHelper
    {
        private static byte[]? _key;
        private static readonly byte[] FallbackKey = Encoding.UTF8.GetBytes("A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6"); // 32 bytes = 256 bits

        public static void Initialize(string? keyString)
        {
            if (string.IsNullOrEmpty(keyString))
            {
                _key = FallbackKey;
                return;
            }

            // Ensure key is exactly 256 bits (32 bytes) by hashing the configured key string
            using var sha256 = SHA256.Create();
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        }

        private static byte[] GetKey()
        {
            return _key ?? FallbackKey;
        }

        public static string Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText ?? string.Empty;

            using var aes = Aes.Create();
            aes.Key = GetKey();
            aes.GenerateIV(); // Generate random IV

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            
            // Prepend IV to ciphertext
            ms.Write(aes.IV, 0, aes.IV.Length);

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText ?? string.Empty;

            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = GetKey();

                var iv = new byte[aes.BlockSize / 8]; // 16 bytes (128 bits)
                if (fullCipher.Length < iv.Length) return cipherText; // Invalid ciphertext

                var cipher = new byte[fullCipher.Length - iv.Length];

                Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(cipher);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch
            {
                // Fallback for older plaintext messages or failed decryption
                return cipherText;
            }
        }
    }
}
