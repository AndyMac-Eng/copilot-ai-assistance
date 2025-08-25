using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CustomerService.Functions
{
    public static class JwtHelpers
    {
        public static string GenerateToken(string userId, string key, string issuer, int minutesValid = 60)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, userId), new Claim("uid", userId) };
            var token = new JwtSecurityToken(issuer, issuer, claims, expires: DateTime.UtcNow.AddMinutes(minutesValid), signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static (bool Valid, string UserId) ValidateToken(string token, string key, string issuer)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, parameters, out var validatedToken);
                var uid = principal.FindFirst("uid")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                return (true, uid ?? string.Empty);
            }
            catch
            {
                return (false, string.Empty);
            }
        }
    }
}
