using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenSol.Core
{
    public static class SecurityUtils
    {
        // LEGACY: older versions encrypted tokens with this key, which is public in the source code.
        // It is only kept to read and migrate those tokens: the server stores hashes (HashSecret)
        // and the clients use the operating system's protected storage.
        private static readonly string SharedKey = "OpenSolari_Security_Key_2026!"; 
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("OpenSolSalt");

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(SharedKey, Salt, 1000, HashAlgorithmName.SHA256);
                    encryptor.Key = pdb.GetBytes(32);

                    // IV extraction attempt (first 16 bytes)
                    if (cipherBytes.Length < 16) return cipherText; // Too short

                    byte[] iv = new byte[16];
                    Buffer.BlockCopy(cipherBytes, 0, iv, 0, 16);
                    encryptor.IV = iv;

                    try 
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                            {
                                cs.Write(cipherBytes, 16, cipherBytes.Length - 16);
                                cs.Close();
                            }
                            return Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                    catch
                    {
                        // Fallback for old tokens (which used IV derived from key)
                        encryptor.IV = pdb.GetBytes(16);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                            {
                                cs.Write(cipherBytes, 0, cipherBytes.Length);
                                cs.Close();
                            }
                            return Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                }
            }
            catch
            {
                // If decryption fails (e.g. text is still clear), return original text
                return cipherText;
            }
        }

        /// <summary>Minimum length of a user code: shorter codes can be guessed.</summary>
        public const int MinCodeLength = 16;

        // No look-alike characters (0/O, 1/l/I) so a code can be read out or typed by hand
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

        /// <summary>
        /// Creates a random user code from a cryptographic random source.
        /// </summary>
        public static string GenerateCode(int length = 24)
        {
            return RandomNumberGenerator.GetString(CodeAlphabet, Math.Max(length, MinCodeLength));
        }

        private const string HashPrefix = "pbkdf2$";
        private const int HashIterations = 20000;

        /// <summary>
        /// Hashes a secret with a random salt (PBKDF2-SHA256). Format: pbkdf2$iterations$salt$hash
        /// Used by the server to store user codes without any key that could decrypt them.
        /// </summary>
        public static string HashSecret(string secret)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, HashIterations, HashAlgorithmName.SHA256, 32);
            return $"{HashPrefix}{HashIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool IsHashedSecret(string? stored)
        {
            return stored != null && stored.StartsWith(HashPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks a secret against a value produced by HashSecret, in constant time.
        /// </summary>
        public static bool VerifySecret(string secret, string? stored)
        {
            if (string.IsNullOrEmpty(secret) || !IsHashedSecret(stored)) return false;

            var parts = stored!.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations <= 0) return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Compares two strings in constant time to mitigate Timing Attacks.
        /// </summary>
        public static bool FixedTimeEquals(string? a, string? b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
