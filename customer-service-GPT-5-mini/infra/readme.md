This infra folder contains Bicep templates for deploying resources used by the Customer microservice. These templates are declarative and do not perform any deployment when generated.

Important: They assume you will wire up managed identities, Key Vault references, and configure API Management with a custom domain and certificate in your deployment pipeline.

Next steps for deployment:
- Replace placeholder names and client IDs
- Add Key Vault integration for secrets
- Add ACR build/push steps or GitHub container registry integration
