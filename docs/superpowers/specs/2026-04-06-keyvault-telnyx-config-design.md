# Key Vault + Telnyx Configuration Design

## Problem

The notification service branch introduces `TelnyxSmsSender` with `TelnyxOptions` (API key, from number), but the deployed test environment has no way to receive these secrets. The current infra pattern passes secrets directly through Bicep parameters, which works for non-sensitive values (managed identity connection strings, App Insights identifiers) but isn't appropriate for real external API keys.

## Goals

1. Introduce Azure Key Vault for storing external service secrets
2. Configure the test environment Container App to read Telnyx credentials from Key Vault
3. Establish a pattern for future external service secrets (email provider, payment, etc.)

## Non-Goals

- Migrating existing Container App secrets (SQL connection string, App Insights connection string) to Key Vault — these use managed identity auth or are non-sensitive identifiers
- Key rotation automation — manual rotation is fine at this stage
- Network restrictions on Key Vault — not needed at this scale

## Design

### Key Vault Bicep Module

New module: `infra/bicep/modules/key-vault.bicep`

- Resource name: `kv-teeforce-{environment}`
- RBAC authorization (not access policies) — aligns with Azure best practices
- Soft delete enabled (Azure default, cannot opt out)
- Grants the existing Container App managed identity the `Key Vault Secrets User` role so the Container App can read secrets at runtime

### Container App Key Vault References

Container Apps support native Key Vault secret references. Instead of passing secret values through Bicep parameters, the Container App declares a secret source as a Key Vault URI and the platform resolves it using the managed identity.

In `container-app.bicep`:

- New parameter: `keyVaultName` (the Key Vault resource name)
- New secrets in the Container App secrets array:
  - `telnyx-api-key` → Key Vault reference to `https://kv-teeforce-{env}.vault.azure.net/secrets/telnyx-api-key`
  - `telnyx-from-number` → Key Vault reference to `https://kv-teeforce-{env}.vault.azure.net/secrets/telnyx-from-number`
- New environment variables:
  - `Telnyx__ApiKey` → `secretRef: 'telnyx-api-key'`
  - `Telnyx__FromNumber` → `secretRef: 'telnyx-from-number'`

The managed identity already assigned to the Container App is the same one granted Key Vault access, so no additional identity configuration is needed.

### Orchestration in main.bicep

- Add Key Vault module deployment (depends on `managedIdentity`)
- Pass Key Vault name to the `containerApp` module
- Key Vault deploys before the Container App (dependency chain: managedIdentity → keyVault → containerApp)

### Secret Management

Secrets are set manually after infrastructure deploys:

```bash
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-api-key --value <API_KEY>
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-from-number --value <FROM_NUMBER>
```

No GitHub secrets, no deployment workflow changes, no Bicep parameters for secret values. The Key Vault is the single source of truth.

### What Doesn't Change

- Existing SQL connection string and App Insights connection string env vars stay as direct Bicep secrets
- Deploy workflows (`deploy-infra.yml`, `deploy-api.yml`) unchanged
- `TelnyxOptions`, `TelnyxSmsSender`, and `Program.cs` DI wiring unchanged
- Parameter files unchanged (no new parameters needed)

## Testing Strategy

- Deploy infra to test environment
- Set Telnyx secrets in Key Vault manually
- Verify Container App starts and resolves secrets (check logs for Telnyx configuration errors)
- Send a test SMS via the app to confirm end-to-end flow

## Future Use

When adding new external service secrets (email provider, payment processor), the pattern is:
1. Add the secret to Key Vault manually
2. Add a Key Vault reference in the Container App secrets array
3. Add the corresponding environment variable
