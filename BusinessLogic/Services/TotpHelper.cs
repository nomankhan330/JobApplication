using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BusinessLogic.Services
{
    // RFC 6238 TOTP (HMAC-SHA1, 30s period, 6 digits) + RFC 4648 Base32 helpers.
    public static class TotpHelper
    {
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private const int TimeStepSeconds = 30;
        private const int Digits = 6;

        public static string GenerateSecret(int byteLength = 20)
        {
            var data = new byte[byteLength];
            RandomNumberGenerator.Fill(data);
            return Base32Encode(data);
        }

        public static string GetProvisioningUri(string issuer, string accountName, string secret)
        {
            var safeIssuer = Uri.EscapeDataString(issuer ?? "C&F Management System");
            var safeAccount = Uri.EscapeDataString(accountName ?? "");
            return $"otpauth://totp/{safeIssuer}:{safeAccount}?secret={secret}&issuer={safeIssuer}&algorithm=SHA1&digits=6&period=30";
        }

        public static string ComputeTotp(string base32Secret, DateTime utcNow)
        {
            long counter = (long)(new DateTimeOffset(utcNow).ToUnixTimeSeconds() / TimeStepSeconds);
            return ComputeHmac(Base32Decode(base32Secret), counter);
        }

        public static bool ValidateTotp(string base32Secret, string code, int window = 1)
        {
            if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var normalized = code.Trim();
            var secretBytes = Base32Decode(base32Secret);
            long currentCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeStepSeconds;

            for (long i = currentCounter - window; i <= currentCounter + window; i++)
            {
                if (FixedTimeEquals(ComputeHmac(secretBytes, i), normalized))
                {
                    return true;
                }
            }

            return false;
        }

        public static string[] GenerateRecoveryCodes(int count = 8)
        {
            var codes = new string[count];
            for (int i = 0; i < count; i++)
            {
                var bytes = new byte[9];
                RandomNumberGenerator.Fill(bytes);
                var base32 = Base32Encode(bytes).Replace("=", "");
                codes[i] = $"{base32.Substring(0, 5)}-{base32.Substring(5, 5)}-{base32.Substring(10, 5)}";
            }
            return codes;
        }

        // Returns true if the given recovery code matches one of the stored codes (case-insensitive).
        // Consumed codes are removed from the stored list.
        public static bool TryConsumeRecoveryCode(ref string storedCodes, string inputCode)
        {
            if (string.IsNullOrWhiteSpace(storedCodes) || string.IsNullOrWhiteSpace(inputCode))
            {
                return false;
            }

            var normalized = inputCode.Trim().ToUpperInvariant();
            var codes = storedCodes
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < codes.Length; i++)
            {
                if (string.Equals(codes[i].Trim(), normalized, StringComparison.Ordinal))
                {
                    var remaining = codes.Where((_, idx) => idx != i).ToArray();
                    storedCodes = string.Join(",", remaining);
                    return true;
                }
            }

            return false;
        }

        private static string ComputeHmac(byte[] secret, long counter)
        {
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            using (var hmac = new HMACSHA1(secret))
            {
                var hash = hmac.ComputeHash(counterBytes);
                int offset = hash[hash.Length - 1] & 0x0F;
                int binary =
                    ((hash[offset] & 0x7F) << 24) |
                    ((hash[offset + 1] & 0xFF) << 16) |
                    ((hash[offset + 2] & 0xFF) << 8) |
                    (hash[offset + 3] & 0xFF);

                int otp = binary % (int)Math.Pow(10, Digits);
                return otp.ToString("D6");
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        public static string Base32Encode(byte[] data)
        {
            var sb = new StringBuilder();
            int bits = 0, value = 0;
            foreach (var b in data)
            {
                value = (value << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Base32Alphabet[(value >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0)
            {
                sb.Append(Base32Alphabet[(value << (5 - bits)) & 31]);
            }
            return sb.ToString();
        }

        public static byte[] Base32Decode(string base32)
        {
            var cleaned = new string((base32 ?? "")
                .ToUpperInvariant()
                .Where(c => Base32Alphabet.Contains(c))
                .ToArray());

            int bits = 0, value = 0;
            var result = new List<byte>();
            foreach (var c in cleaned)
            {
                value = (value << 5) | Base32Alphabet.IndexOf(c);
                bits += 5;
                if (bits >= 8)
                {
                    result.Add((byte)((value >> (bits - 8)) & 0xFF));
                    bits -= 8;
                }
            }
            return result.ToArray();
        }
    }
}