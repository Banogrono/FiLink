using System;
using System.IO;
using System.Security.Cryptography;

namespace FiLink.Models
{
    public static class Encryption
    {
        // =============================================================================================================
        // Public Methods
        // =============================================================================================================

        /// <summary>
        /// This is encrypting method that uses Rijndael algorithm to encrypt data using key of lenght of 64. 
        /// </summary>
        /// <param name="data">Data to be encrypted.</param>
        /// <param name="seed">Seed or key with which data will be encrypted.</param>
        /// <returns>Encrypted data in form of byte array.</returns>
        /// <exception cref="Exception">Seed cannot equal 0</exception>
        public static byte[] Encrypt(byte[] data, int seed)
        {
            if (seed == 0)
            {
                throw new Exception("Seed cannot equal 0.");
            }

            var key = UtilityMethods.GenerateKey(seed, 64);
            DeriveBytes pdb = new PasswordDeriveBytes(key, new byte[]
            {
                0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d,
                0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76
            });

            Rijndael alg = Rijndael.Create();
            alg.Key = pdb.GetBytes(32);
            alg.IV = pdb.GetBytes(16);

            var ms = new MemoryStream();
            var cs = new CryptoStream(ms, alg.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(data, 0, data.Length);
            cs.Close();
            var encryptedData = ms.ToArray();

            return encryptedData;
        }

        /// <summary>
        /// This is decrypting method that uses Rijndael algorithm to decrypt data using key of lenght of 64. 
        /// </summary>
        /// <param name="data">Data to be decrypted.</param>
        /// <param name="seed">Seed or key with which data will be decrypted.</param>
        /// <returns>Decrypted data in form of byte array.</returns>
        /// <exception cref="Exception">Seed cannot equal 0</exception>
        public static byte[] Decrypt(byte[] data, int seed)
        {
            if (seed == 0)
            {
                throw new Exception("Seed cannot equal 0.");
            }

            var key = UtilityMethods.GenerateKey(seed, 64);
            DeriveBytes pdb = new PasswordDeriveBytes(key, new byte[]
            {
                0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d,
                0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76
            });

            var alg = Rijndael.Create();
            alg.Key = pdb.GetBytes(32);
            alg.IV = pdb.GetBytes(16);

            var ms = new MemoryStream();
            var cs = new CryptoStream(ms, alg.CreateDecryptor(), CryptoStreamMode.Write);

            cs.Write(data, 0, data.Length);
            cs.Close();
            var decryptedData = ms.ToArray();

            return decryptedData;
        }
    }
}