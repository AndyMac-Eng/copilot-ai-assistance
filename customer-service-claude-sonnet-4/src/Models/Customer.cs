using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace CustomerService.Models
{
    /// <summary>
    /// Customer account entity for storage and business logic operations
    /// </summary>
    public class Customer
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("partitionKey")]
        [JsonPropertyName("partitionKey")]
        public string PartitionKey => Id; // Using Id as partition key for even distribution

        [Required]
        [EmailAddress]
        [JsonProperty("email")]
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        [JsonProperty("firstName")]
        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        [JsonProperty("lastName")]
        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [Phone]
        [JsonProperty("phoneNumber")]
        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [JsonProperty("passwordHash")]
        [JsonPropertyName("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonProperty("passwordSalt")]
        [JsonPropertyName("passwordSalt")]
        public string PasswordSalt { get; set; } = string.Empty;

        [JsonProperty("isEmailVerified")]
        [JsonPropertyName("isEmailVerified")]
        public bool IsEmailVerified { get; set; } = false;

        [JsonProperty("isActive")]
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonProperty("createdAt")]
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updatedAt")]
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("lastLoginAt")]
        [JsonPropertyName("lastLoginAt")]
        public DateTime? LastLoginAt { get; set; }

        [JsonProperty("_etag")]
        [JsonPropertyName("_etag")]
        public string? ETag { get; set; }

        // Computed property for display purposes
        [JsonProperty("fullName")]
        [JsonPropertyName("fullName")]
        public string FullName => $"{FirstName} {LastName}";
    }
}
