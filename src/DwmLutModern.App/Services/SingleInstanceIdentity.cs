using System;
using System.Security.Cryptography;
using System.Text;

namespace DwmLutModern.Services
{
    /// <summary>Builds a stable, current-user instance/pipe suffix with optional test isolation.</summary>
    public static class SingleInstanceIdentity
    {
        public static string Create(string userIdentity, string? isolationScope)
        {
            var safeUser = Sanitize(string.IsNullOrWhiteSpace(userIdentity) ? "unknown" : userIdentity);
            if (string.IsNullOrWhiteSpace(isolationScope)) return safeUser;

            var normalizedScope = isolationScope.Trim().ToUpperInvariant();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedScope));
            return safeUser + "_" + Convert.ToHexString(hash, 0, 8);
        }

        private static string Sanitize(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                var asciiLetter = character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
                var digit = character is >= '0' and <= '9';
                result.Append(asciiLetter || digit ? character : '_');
            }
            return result.ToString();
        }
    }
}
