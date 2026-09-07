using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class EncryptionHelper
{
    // 32 Characters = 256 Bit Key
    private const string EncryptionKey = "12345678901234567890123456789012";

    // 16 Characters = 128 Bit IV
    private const string EncryptionIV = "1234567890123456";

    public static string Encrypt(string plainText)
    {
        byte[] key = Encoding.UTF8.GetBytes(EncryptionKey);
        byte[] iv = Encoding.UTF8.GetBytes(EncryptionIV);

        using Aes aes = Aes.Create();

        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream ms = new MemoryStream();

        using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            using StreamWriter sw = new StreamWriter(cs);

            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decrypt(string cipherText)
    {
        byte[] key = Encoding.UTF8.GetBytes(EncryptionKey);
        byte[] iv = Encoding.UTF8.GetBytes(EncryptionIV);

        byte[] buffer = Convert.FromBase64String(cipherText);

        using Aes aes = Aes.Create();

        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream ms = new MemoryStream(buffer);

        using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);

        using StreamReader sr = new StreamReader(cs);

        return sr.ReadToEnd();
    }
}