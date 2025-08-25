using Microsoft.Extensions.Logging;
using CustomerService.Models;
using CustomerService.Models.DTOs;
using CustomerService.Repositories.Interfaces;
using CustomerService.Services.Interfaces;

namespace CustomerService.Services
{
    /// <summary>
    /// Service for handling customer business logic operations
    /// </summary>
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IAuthenticationService _authenticationService;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            ICustomerRepository customerRepository,
            IAuthenticationService authenticationService,
            ILogger<CustomerService> logger)
        {
            _customerRepository = customerRepository;
            _authenticationService = authenticationService;
            _logger = logger;
        }

        public async Task<ApiResponseDto<CustomerProfileDto>> RegisterAsync(CustomerRegistrationDto registrationDto)
        {
            try
            {
                _logger.LogInformation("Registering new customer with email: {Email}", registrationDto.Email);

                // Check if customer already exists
                if (await _customerRepository.ExistsAsync(registrationDto.Email))
                {
                    _logger.LogWarning("Customer registration failed - email already exists: {Email}", registrationDto.Email);
                    return new ApiResponseDto<CustomerProfileDto>
                    {
                        Success = false,
                        Message = "A customer with this email address already exists.",
                        Errors = new List<string> { "Email address is already registered." }
                    };
                }

                // Create new customer
                var salt = _authenticationService.GenerateSalt();
                var passwordHash = _authenticationService.HashPassword(registrationDto.Password, salt);

                var customer = new Customer
                {
                    Email = registrationDto.Email.ToLowerInvariant(),
                    FirstName = registrationDto.FirstName,
                    LastName = registrationDto.LastName,
                    PhoneNumber = registrationDto.PhoneNumber,
                    PasswordHash = passwordHash,
                    PasswordSalt = salt,
                    IsEmailVerified = false,
                    IsActive = true
                };

                var createdCustomer = await _customerRepository.CreateAsync(customer);

                var profileDto = new CustomerProfileDto
                {
                    Id = createdCustomer.Id,
                    Email = createdCustomer.Email,
                    FirstName = createdCustomer.FirstName,
                    LastName = createdCustomer.LastName,
                    FullName = createdCustomer.FullName,
                    PhoneNumber = createdCustomer.PhoneNumber,
                    IsEmailVerified = createdCustomer.IsEmailVerified,
                    CreatedAt = createdCustomer.CreatedAt,
                    LastLoginAt = createdCustomer.LastLoginAt
                };

                _logger.LogInformation("Successfully registered customer with ID: {CustomerId}", createdCustomer.Id);

                return new ApiResponseDto<CustomerProfileDto>
                {
                    Success = true,
                    Message = "Customer registered successfully.",
                    Data = profileDto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering customer with email: {Email}", registrationDto.Email);
                return new ApiResponseDto<CustomerProfileDto>
                {
                    Success = false,
                    Message = "An error occurred during registration.",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponseDto<AuthenticationResponseDto>> LoginAsync(CustomerLoginDto loginDto)
        {
            try
            {
                _logger.LogInformation("Customer login attempt for email: {Email}", loginDto.Email);

                var customer = await _customerRepository.GetByEmailAsync(loginDto.Email.ToLowerInvariant());
                if (customer == null)
                {
                    _logger.LogWarning("Login failed - customer not found: {Email}", loginDto.Email);
                    return new ApiResponseDto<AuthenticationResponseDto>
                    {
                        Success = false,
                        Message = "Invalid email or password.",
                        Errors = new List<string> { "Authentication failed." }
                    };
                }

                if (!customer.IsActive)
                {
                    _logger.LogWarning("Login failed - customer account is inactive: {Email}", loginDto.Email);
                    return new ApiResponseDto<AuthenticationResponseDto>
                    {
                        Success = false,
                        Message = "Account is inactive. Please contact support.",
                        Errors = new List<string> { "Account is inactive." }
                    };
                }

                if (!_authenticationService.VerifyPassword(loginDto.Password, customer.PasswordHash, customer.PasswordSalt))
                {
                    _logger.LogWarning("Login failed - invalid password for email: {Email}", loginDto.Email);
                    return new ApiResponseDto<AuthenticationResponseDto>
                    {
                        Success = false,
                        Message = "Invalid email or password.",
                        Errors = new List<string> { "Authentication failed." }
                    };
                }

                // Update last login time
                customer.LastLoginAt = DateTime.UtcNow;
                await _customerRepository.UpdateAsync(customer);

                // Generate tokens
                var token = await _authenticationService.GenerateJwtTokenAsync(customer);
                var refreshToken = await _authenticationService.GenerateRefreshTokenAsync();

                var profileDto = new CustomerProfileDto
                {
                    Id = customer.Id,
                    Email = customer.Email,
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    FullName = customer.FullName,
                    PhoneNumber = customer.PhoneNumber,
                    IsEmailVerified = customer.IsEmailVerified,
                    CreatedAt = customer.CreatedAt,
                    LastLoginAt = customer.LastLoginAt
                };

                var authResponse = new AuthenticationResponseDto
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(60), // Match JWT expiration
                    Customer = profileDto
                };

                _logger.LogInformation("Successful login for customer: {CustomerId}", customer.Id);

                return new ApiResponseDto<AuthenticationResponseDto>
                {
                    Success = true,
                    Message = "Login successful.",
                    Data = authResponse
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", loginDto.Email);
                return new ApiResponseDto<AuthenticationResponseDto>
                {
                    Success = false,
                    Message = "An error occurred during login.",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponseDto<bool>> LogoutAsync(string customerId)
        {
            try
            {
                _logger.LogInformation("Customer logout for ID: {CustomerId}", customerId);

                // In a more sophisticated implementation, you might want to:
                // 1. Invalidate the refresh token
                // 2. Add the JWT to a blacklist
                // 3. Update last activity timestamp

                // For now, we'll just log the logout event
                var customer = await _customerRepository.GetByIdAsync(customerId);
                if (customer != null)
                {
                    _logger.LogInformation("Successful logout for customer: {CustomerId}", customerId);
                }

                return new ApiResponseDto<bool>
                {
                    Success = true,
                    Message = "Logout successful.",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for customer: {CustomerId}", customerId);
                return new ApiResponseDto<bool>
                {
                    Success = false,
                    Message = "An error occurred during logout.",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponseDto<CustomerProfileDto>> GetProfileAsync(string customerId)
        {
            try
            {
                _logger.LogInformation("Retrieving profile for customer: {CustomerId}", customerId);

                var customer = await _customerRepository.GetByIdAsync(customerId);
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found: {CustomerId}", customerId);
                    return new ApiResponseDto<CustomerProfileDto>
                    {
                        Success = false,
                        Message = "Customer not found.",
                        Errors = new List<string> { "Customer does not exist." }
                    };
                }

                var profileDto = new CustomerProfileDto
                {
                    Id = customer.Id,
                    Email = customer.Email,
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    FullName = customer.FullName,
                    PhoneNumber = customer.PhoneNumber,
                    IsEmailVerified = customer.IsEmailVerified,
                    CreatedAt = customer.CreatedAt,
                    LastLoginAt = customer.LastLoginAt
                };

                return new ApiResponseDto<CustomerProfileDto>
                {
                    Success = true,
                    Message = "Profile retrieved successfully.",
                    Data = profileDto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving profile for customer: {CustomerId}", customerId);
                return new ApiResponseDto<CustomerProfileDto>
                {
                    Success = false,
                    Message = "An error occurred while retrieving profile.",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponseDto<CustomerProfileDto>> UpdateProfileAsync(string customerId, CustomerProfileDto profileDto)
        {
            try
            {
                _logger.LogInformation("Updating profile for customer: {CustomerId}", customerId);

                var customer = await _customerRepository.GetByIdAsync(customerId);
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for update: {CustomerId}", customerId);
                    return new ApiResponseDto<CustomerProfileDto>
                    {
                        Success = false,
                        Message = "Customer not found.",
                        Errors = new List<string> { "Customer does not exist." }
                    };
                }

                // Update customer properties
                customer.FirstName = profileDto.FirstName;
                customer.LastName = profileDto.LastName;
                customer.PhoneNumber = profileDto.PhoneNumber;

                var updatedCustomer = await _customerRepository.UpdateAsync(customer);

                var updatedProfileDto = new CustomerProfileDto
                {
                    Id = updatedCustomer.Id,
                    Email = updatedCustomer.Email,
                    FirstName = updatedCustomer.FirstName,
                    LastName = updatedCustomer.LastName,
                    FullName = updatedCustomer.FullName,
                    PhoneNumber = updatedCustomer.PhoneNumber,
                    IsEmailVerified = updatedCustomer.IsEmailVerified,
                    CreatedAt = updatedCustomer.CreatedAt,
                    LastLoginAt = updatedCustomer.LastLoginAt
                };

                _logger.LogInformation("Successfully updated profile for customer: {CustomerId}", customerId);

                return new ApiResponseDto<CustomerProfileDto>
                {
                    Success = true,
                    Message = "Profile updated successfully.",
                    Data = updatedProfileDto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for customer: {CustomerId}", customerId);
                return new ApiResponseDto<CustomerProfileDto>
                {
                    Success = false,
                    Message = "An error occurred while updating profile.",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponseDto<bool>> DeactivateAccountAsync(string customerId)
        {
            try
            {
                _logger.LogInformation("Deactivating account for customer: {CustomerId}", customerId);

                var customer = await _customerRepository.GetByIdAsync(customerId);
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for deactivation: {CustomerId}", customerId);
                    return new ApiResponseDto<bool>
                    {
                        Success = false,
                        Message = "Customer not found.",
                        Errors = new List<string> { "Customer does not exist." }
                    };
                }

                customer.IsActive = false;
                await _customerRepository.UpdateAsync(customer);

                _logger.LogInformation("Successfully deactivated account for customer: {CustomerId}", customerId);

                return new ApiResponseDto<bool>
                {
                    Success = true,
                    Message = "Account deactivated successfully.",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating account for customer: {CustomerId}", customerId);
                return new ApiResponseDto<bool>
                {
                    Success = false,
                    Message = "An error occurred while deactivating account.",
                    Errors = new List<string> { ex.Message }
                };
            }
        }
    }
}
