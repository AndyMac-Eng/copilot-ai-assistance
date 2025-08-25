using System;
using System.ComponentModel.DataAnnotations;

namespace CustomerMicroservice.Models
{
    public class CustomerAccount
    {
        [Key]
        public string Id { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string PasswordHash { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }
}
