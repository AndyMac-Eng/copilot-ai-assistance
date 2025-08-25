using CustomerService.Models;
using CustomerService.Models.DTOs;

namespace CustomerService.Services.Interfaces
{
    /// <summary>
    /// Service interface for authentication operations
    /// </summary>
    public interface IAuthenticationService
    {
        Task<string> GenerateJwtTokenAsync(Customer customer);
        Task<string> GenerateRefreshTokenAsync();
        Task<bool> ValidateTokenAsync(string token);
        Task<string?> GetCustomerIdFromTokenAsync(string token);
        string HashPassword(string password, string salt);
        string GenerateSalt();
        bool VerifyPassword(string password, string hash, string salt);
    }
}
