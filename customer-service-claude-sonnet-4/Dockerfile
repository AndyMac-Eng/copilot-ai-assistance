# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

# Set the working directory
WORKDIR /home/site/wwwroot

# Copy the published application
COPY . .

# Set environment variables for Azure Functions
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

# Expose the port that the application will run on
EXPOSE 80

# The base image already contains the Azure Functions runtime
# so we just need to ensure our application starts correctly
