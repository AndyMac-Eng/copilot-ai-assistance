using System;

namespace CustomerService.Models
{
    public class CustomerAccount
    {
        public string? Id { get; set; } // Unique identifier (GUID)
        public string? Email { get; set; } // Unique, required
        public string? PasswordHash { get; set; } // Hashed password
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
        // Add additional fields as needed (e.g., phone, address)
    }
}
