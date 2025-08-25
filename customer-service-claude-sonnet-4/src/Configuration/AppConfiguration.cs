namespace CustomerService.Configuration
{
    /// <summary>
    /// Configuration settings for Cosmos DB connection
    /// </summary>
    public class CosmosDbConfiguration
    {
        public const string SectionName = "CosmosDb";

        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "CustomerService";
        public string ContainerName { get; set; } = "Customers";
        public string PartitionKeyPath { get; set; } = "/partitionKey";
    }

    /// <summary>
    /// Configuration settings for JWT authentication
    /// </summary>
    public class JwtConfiguration
    {
        public const string SectionName = "Jwt";

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; } = 60;
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }

    /// <summary>
    /// Configuration settings for Azure AD B2C integration
    /// </summary>
    public class AzureAdB2CConfiguration
    {
        public const string SectionName = "AzureAdB2C";

        public string Instance { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string SignUpSignInPolicyId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration settings for Application Insights
    /// </summary>
    public class ApplicationInsightsConfiguration
    {
        public const string SectionName = "ApplicationInsights";

        public string ConnectionString { get; set; } = string.Empty;
        public string InstrumentationKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration settings for Key Vault
    /// </summary>
    public class KeyVaultConfiguration
    {
        public const string SectionName = "KeyVault";

        public string VaultUri { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
