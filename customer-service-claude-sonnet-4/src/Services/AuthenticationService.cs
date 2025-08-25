using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using CustomerService.Configuration;
using CustomerService.Models;
using CustomerService.Services.Interfaces;

namespace CustomerService.Services
{
    /// <summary>
    /// Service for handling authentication operations including JWT token generation and password hashing
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly JwtConfiguration _jwtConfig;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            IOptions<JwtConfiguration> jwtConfig,
            ILogger<AuthenticationService> logger)
        {
            _jwtConfig = jwtConfig.Value;
            _logger = logger;
        }

        public async Task<string> GenerateJwtTokenAsync(Customer customer)
        {
            try
            {
                _logger.LogInformation("Generating JWT token for customer: {CustomerId}", customer.Id);

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtConfig.SecretKey);

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, customer.Id),
                    new(ClaimTypes.Email, customer.Email),
                    new(ClaimTypes.Name, customer.FullName),
                    new("customerId", customer.Id),
                    new("email", customer.Email),
                    new("firstName", customer.FirstName),
                    new("lastName", customer.LastName)
                };

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(_jwtConfig.ExpirationMinutes),
                    Issuer = _jwtConfig.Issuer,
                    Audience = _jwtConfig.Audience,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                _logger.LogInformation("Successfully generated JWT token for customer: {CustomerId}", customer.Id);
                return await Task.FromResult(tokenString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for customer: {CustomerId}", customer.Id);
                throw;
            }
        }

        public async Task<string> GenerateRefreshTokenAsync()
        {
            try
            {
                var randomNumber = new byte[32];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(randomNumber);
                var refreshToken = Convert.ToBase64String(randomNumber);

                _logger.LogInformation("Generated refresh token");
                return await Task.FromResult(refreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refresh token");
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtConfig.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtConfig.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtConfig.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                return await Task.FromResult(validatedToken != null);
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning(ex, "Invalid token provided");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        public async Task<string?> GetCustomerIdFromTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);

                var customerId = jsonToken.Claims.FirstOrDefault(x => x.Type == "customerId")?.Value;
                return await Task.FromResult(customerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting customer ID from token");
                return null;
            }
        }

        public string HashPassword(string password, string salt)
        {
            try
            {
                using var sha256 = SHA256.Create();
                var saltedPassword = string.Concat(password, salt);
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
                return Convert.ToBase64String(hashedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing password");
                throw;
            }
        }

        public string GenerateSalt()
        {
            try
            {
                var salt = new byte[32];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(salt);
                return Convert.ToBase64String(salt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating salt");
                throw;
            }
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            try
            {
                var hashedPassword = HashPassword(password, salt);
                return hashedPassword == hash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password");
                return false;
            }
        }
    }
}
