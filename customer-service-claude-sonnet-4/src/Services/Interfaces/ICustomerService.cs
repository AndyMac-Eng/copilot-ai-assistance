using CustomerService.Models;
using CustomerService.Models.DTOs;

namespace CustomerService.Services.Interfaces
{
    /// <summary>
    /// Service interface for customer business logic operations
    /// </summary>
    public interface ICustomerService
    {
        Task<ApiResponseDto<CustomerProfileDto>> RegisterAsync(CustomerRegistrationDto registrationDto);
        Task<ApiResponseDto<AuthenticationResponseDto>> LoginAsync(CustomerLoginDto loginDto);
        Task<ApiResponseDto<bool>> LogoutAsync(string customerId);
        Task<ApiResponseDto<CustomerProfileDto>> GetProfileAsync(string customerId);
        Task<ApiResponseDto<CustomerProfileDto>> UpdateProfileAsync(string customerId, CustomerProfileDto profileDto);
        Task<ApiResponseDto<bool>> DeactivateAccountAsync(string customerId);
    }
}
