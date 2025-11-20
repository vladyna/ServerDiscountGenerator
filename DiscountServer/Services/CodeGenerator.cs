using System.Security.Cryptography;

namespace DiscountServer.Services
{
    public static class CodeGenerator
    {
        #region Constants
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        #endregion

        #region API
        public static string RandomCode(int length)
        {
            Span<byte> bytes = stackalloc byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            var chars = new char[length];

            for (int i = 0; i < length; i++) 
                chars[i] = Alphabet[bytes[i] % Alphabet.Length];

            return new string(chars);
        }
        #endregion
    }
}
