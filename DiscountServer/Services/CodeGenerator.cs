using System.Security.Cryptography;
using System;

namespace DiscountServer.Services
{
    public static class CodeGenerator
    {
        private const string Crockford32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string RandomCode(int length)
        {
            var ulidBytes = UlidBytes();
            var encoded = EncodeCrockford32(ulidBytes);
            if (length <= encoded.Length)
                return encoded.Substring(0, length);
            var pad = new char[length - encoded.Length];
            Span<byte> extra = stackalloc byte[pad.Length];
            RandomNumberGenerator.Fill(extra);
            for (int i = 0; i < pad.Length; i++) pad[i] = Crockford32[extra[i] % Crockford32.Length];
            return encoded + new string(pad);
        }

        private static byte[] UlidBytes()
        {
            var bytes = new byte[16];
            var time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bytes[0] = (byte)(time >> 40);
            bytes[1] = (byte)(time >> 32);
            bytes[2] = (byte)(time >> 24);
            bytes[3] = (byte)(time >> 16);
            bytes[4] = (byte)(time >> 8);
            bytes[5] = (byte)(time);
            Span<byte> rnd = stackalloc byte[10];
            RandomNumberGenerator.Fill(rnd);
            for (int i = 0; i < 10; i++) bytes[6 + i] = rnd[i];
            return bytes;
        }

        private static string EncodeCrockford32(byte[] data)
        {
            int outputLen = (int)Math.Ceiling(data.Length * 8 / 5.0);
            char[] chars = new char[outputLen];
            int bitBuffer = 0;
            int bitsInBuffer = 0;
            int idx = 0;
            foreach (var b in data)
            {
                bitBuffer = (bitBuffer << 8) | b;
                bitsInBuffer += 8;
                while (bitsInBuffer >= 5)
                {
                    bitsInBuffer -= 5;
                    int val = (bitBuffer >> bitsInBuffer) & 0x1F;
                    chars[idx++] = Crockford32[val];
                }
            }
            if (bitsInBuffer > 0)
            {
                int val = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
                chars[idx++] = Crockford32[val];
            }
            if (idx < chars.Length)
                return new string(chars, 0, idx);
            return new string(chars);
        }
    }
}
