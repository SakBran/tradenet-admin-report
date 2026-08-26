using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace API.Service.Security
{
    public static class TradenetPasswordCipher
    {
        private const string StrPermutation = "Tr@deNet2";
        private const int BytePermutation1 = 0x19;
        private const int BytePermutation2 = 0x59;
        private const int BytePermutation3 = 0x17;
        private const int BytePermutation4 = 0x41;

        public static string Encrypt(string value)
        {
            return Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(value)));
        }

        public static string Decrypt(string value)
        {
            return Encoding.UTF8.GetString(Decrypt(Convert.FromBase64String(value)));
        }

        private static byte[] Encrypt(byte[] value)
        {
            using var passbytes = CreatePasswordBytes();
            using var memstream = new MemoryStream();
            using var aes = Aes.Create();
            aes.Key = passbytes.GetBytes(aes.KeySize / 8);
            aes.IV = passbytes.GetBytes(aes.BlockSize / 8);

            using (var cryptostream = new CryptoStream(memstream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cryptostream.Write(value, 0, value.Length);
            }

            return memstream.ToArray();
        }

        private static byte[] Decrypt(byte[] value)
        {
            using var passbytes = CreatePasswordBytes();
            using var memstream = new MemoryStream();
            using var aes = Aes.Create();
            aes.Key = passbytes.GetBytes(aes.KeySize / 8);
            aes.IV = passbytes.GetBytes(aes.BlockSize / 8);

            using (var cryptostream = new CryptoStream(memstream, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cryptostream.Write(value, 0, value.Length);
            }

            return memstream.ToArray();
        }

        private static PasswordDeriveBytes CreatePasswordBytes()
        {
            return new PasswordDeriveBytes(
                StrPermutation,
                new byte[]
                {
                    BytePermutation1,
                    BytePermutation2,
                    BytePermutation3,
                    BytePermutation4
                });
        }
    }
}
