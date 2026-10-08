using System;
using System.Security.Cryptography;
using System.Text;

namespace SkillConfig
{
    public static class SkillConfigHash
    {
        public static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
