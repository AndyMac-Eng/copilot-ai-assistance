using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using CustomerService.Models.DTOs;
using CustomerService.Services.Interfaces;

namespace CustomerService.Functions
{
    /// <summary>
    /// Azure Functions for customer account management operations
    /// </summary>
    public class CustomerFunctions
    {
        private readonly ICustomerService _customerService;
        private readonly IAuthenticationService _authenticationService;
        private readonly ILogger<CustomerFunctions> _logger;

        public CustomerFunctions(
            ICustomerService customerService,
            IAuthenticationService authenticationService,
            ILogger<CustomerFunctions> logger)
        {
            _customerService = customerService;
            _authenticationService = authenticationService;
            _logger = logger;
        }

        [Function("RegisterCustomer")]
        public async Task<HttpResponseData> RegisterCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/register")] HttpRequestData req)
        {
            _logger.LogInformation("Processing customer registration request");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var registrationDto = JsonSerializer.Deserialize<CustomerRegistrationDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (registrationDto == null)
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Invalid request body",
                        Errors = new List<string> { "Request body cannot be empty" }
                    });
                    return badRequestResponse;
                }

                var result = await _customerService.RegisterAsync(registrationDto);

                var response = req.CreateResponse(result.Success ? HttpStatusCode.Created : HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(result);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing customer registration");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "An internal error occurred",
                    Errors = new List<string> { ex.Message }
                });
                return errorResponse;
            }
        }

        [Function("LoginCustomer")]
        public async Task<HttpResponseData> LoginCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/login")] HttpRequestData req)
        {
            _logger.LogInformation("Processing customer login request");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var loginDto = JsonSerializer.Deserialize<CustomerLoginDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (loginDto == null)
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Invalid request body",
                        Errors = new List<string> { "Request body cannot be empty" }
                    });
                    return badRequestResponse;
                }

                var result = await _customerService.LoginAsync(loginDto);

                var response = req.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
                await response.WriteAsJsonAsync(result);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing customer login");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "An internal error occurred",
                    Errors = new List<string> { ex.Message }
                });
                return errorResponse;
            }
        }

        [Function("LogoutCustomer")]
        public async Task<HttpResponseData> LogoutCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/logout")] HttpRequestData req)
        {
            _logger.LogInformation("Processing customer logout request");

            try
            {
                var customerId = await ExtractCustomerIdFromToken(req);
                if (string.IsNullOrEmpty(customerId))
                {
                    var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorizedResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Unauthorized",
                        Errors = new List<string> { "Invalid or missing authorization token" }
                    });
                    return unauthorizedResponse;
                }

                var result = await _customerService.LogoutAsync(customerId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing customer logout");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "An internal error occurred",
                    Errors = new List<string> { ex.Message }
                });
                return errorResponse;
            }
        }

        [Function("GetCustomerProfile")]
        public async Task<HttpResponseData> GetCustomerProfile(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/profile")] HttpRequestData req)
        {
            _logger.LogInformation("Processing get customer profile request");

            try
            {
                var customerId = await ExtractCustomerIdFromToken(req);
                if (string.IsNullOrEmpty(customerId))
                {
                    var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorizedResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Unauthorized",
                        Errors = new List<string> { "Invalid or missing authorization token" }
                    });
                    return unauthorizedResponse;
                }

                var result = await _customerService.GetProfileAsync(customerId);

                var response = req.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.NotFound);
                await response.WriteAsJsonAsync(result);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing get customer profile");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "An internal error occurred",
                    Errors = new List<string> { ex.Message }
                });
                return errorResponse;
            }
        }

        [Function("UpdateCustomerProfile")]
        public async Task<HttpResponseData> UpdateCustomerProfile(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "customers/profile")] HttpRequestData req)
        {
            _logger.LogInformation("Processing update customer profile request");

            try
            {
                var customerId = await ExtractCustomerIdFromToken(req);
                if (string.IsNullOrEmpty(customerId))
                {
                    var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorizedResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Unauthorized",
                        Errors = new List<string> { "Invalid or missing authorization token" }
                    });
                    return unauthorizedResponse;
                }

                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var profileDto = JsonSerializer.Deserialize<CustomerProfileDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (profileDto == null)
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Invalid request body",
                        Errors = new List<string> { "Request body cannot be empty" }
                    });
                    return badRequestResponse;
                }

                var result = await _customerService.UpdateProfileAsync(customerId, profileDto);

                var response = req.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(result);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing update customer profile");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "An internal error occurred",
                    Errors = new List<string> { ex.Message }
                });
                return errorResponse;
            }
        }

        [Function("DeactivateCustomerAccount")]
        public async Task<HttpResponseData> DeactivateCustomerAccount(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customers/account")] HttpRequestData req)
        {
            _logger.LogInformation("Processing deactivate customer account request");

            try
            {
                var customerId = await ExtractCustomerIdFromToken(req);
                if (string.IsNullOrEmpty(customerId))
                {
                    var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await unauthorizedResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                    {
                        Success = false,
                        Message = "Unauthorized",
                        Errors = new List<string> { "Invalid or missing authorization token" }
                    });
                    return unauthorizedResponse;
                }

                var result = await _customerService.DeactivateAccountAsync(customerId);

                var response = req.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(result);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deactivate customer account");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new ApiResponseDto<object>
                {
                    Success = false,
                    Message = "An internal error occurred",
                    Errors = new List<string> { ex.Message }
                });
                return errorResponse;
            }
        }

        private async Task<string?> ExtractCustomerIdFromToken(HttpRequestData req)
        {
            try
            {
                var authHeader = req.Headers.GetValues("Authorization").FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return null;
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                return await _authenticationService.GetCustomerIdFromTokenAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting customer ID from token");
                return null;
            }
        }
    }
}
