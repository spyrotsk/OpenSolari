using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenSol.Core
{
    public static class SecurityUtils
    {
        // NOTE: In a real production app, this key should not be hardcoded in the code
        // but managed via OS Secure Storage or protected parameters.
        // For this project, we use a fixed derived key to ensure portability between components.
        private static readonly string SharedKey = "OpenSolari_Security_Key_2026!"; 
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("OpenSolSalt");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";

            try 
            {
                byte[] clearBytes = Encoding.UTF8.GetBytes(plainText);
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(SharedKey, Salt, 1000, HashAlgorithmName.SHA256);
                    encryptor.Key = pdb.GetBytes(32);
                    
                    // Generates a random IV for each encryption
                    encryptor.GenerateIV();
                    byte[] iv = encryptor.IV;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        // We write the IV at the beginning of the packet to recover it during decryption
                        ms.Write(iv, 0, iv.Length);

                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(clearBytes, 0, clearBytes.Length);
                            cs.Close();
                        }
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                // In case of error, return the original text (graceful fallback for migration)
                return plainText;
            }
        }

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
