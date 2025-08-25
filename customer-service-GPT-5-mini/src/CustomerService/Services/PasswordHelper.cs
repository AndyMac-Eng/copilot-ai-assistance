using System;
using System.Security.Cryptography;

namespace CustomerService.Services
{
    public static class PasswordHelper
    {
        public static (string Hash, string Salt) HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(32);
            using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var hash = derive.GetBytes(32);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }
    }
}
