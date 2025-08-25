Repository implementations for customer storage. Currently includes `CosmosCustomerRepository`.

Security: Passwords are hashed using PBKDF2 (Rfc2898DeriveBytes) with 100k iterations and 32-byte salt and hash. Consider delegating auth to Azure AD B2C for production and using managed identities and Key Vault for secrets.
