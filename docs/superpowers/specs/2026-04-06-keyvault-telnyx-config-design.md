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
- The operator deploying the infra needs `Key Vault Secrets Officer` role (or equivalent) to set secrets manually

**Naming:** KV names are globally unique across Azure. `kv-teeforce-test` (17 chars) is within the 3–24 character limit. If the name is taken, fall back to `kv-teeforce-{environment}-{suffix}` with a short unique identifier.

### Container App Key Vault References

Container Apps support native Key Vault secret references. Instead of passing secret values through Bicep parameters, the Container App declares a secret source as a Key Vault URI and the platform resolves it using the managed identity.

In `container-app.bicep`:

- New parameter: `keyVaultName` (passed as an output from the Key Vault module, not hardcoded — this creates an implicit Bicep dependency)
- New secrets in the Container App secrets array using the Key Vault reference syntax:

```bicep
{
  name: 'telnyx-api-key'
  keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/telnyx-api-key'
  identity: userAssignedIdentityId
}
{
  name: 'telnyx-from-number'
  keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/telnyx-from-number'
  identity: userAssignedIdentityId
}
```

The `identity` field must be the resource ID of the user-assigned identity (not the client ID). This is the same `userAssignedIdentityId` parameter already passed to the module.

- New environment variables:
  - `Telnyx__ApiKey` → `secretRef: 'telnyx-api-key'`
  - `Telnyx__FromNumber` → `secretRef: 'telnyx-from-number'`

### Orchestration in main.bicep

- Add Key Vault module deployment (depends on `managedIdentity` via `managedIdentity.outputs.principalId` parameter — implicit dependency)
- Pass `keyVault.outputs.name` to the `containerApp` module (creates implicit dependency — Key Vault deploys before Container App)
- Update the deployment order comment at the top of `main.bicep`
- Dependency chain: managedIdentity → keyVault → containerApp

### Secret Management — First-Time Setup

**Container Apps validates Key Vault references at deploy time.** If the secrets don't exist in Key Vault, the Container App deployment fails. This means secrets must be seeded before the first infra deploy that includes KV references.

**First-time environment setup (one-time per environment):**

1. Deploy infra without the KV secret references first (or deploy just the Key Vault module)
2. Seed placeholder secrets:

```bash
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-api-key --value placeholder
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-from-number --value placeholder
```

3. Deploy full infra (Container App can now resolve the KV references)
4. Update secrets with real values:

```bash
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-api-key --value <API_KEY>
az keyvault secret set --vault-name kv-teeforce-test --name telnyx-from-number --value <FROM_NUMBER>
```

After initial setup, subsequent infra deploys work without manual intervention — the secrets persist in Key Vault.

No GitHub secrets, no deployment workflow changes, no Bicep parameters for secret values. The Key Vault is the single source of truth.

### What Doesn't Change

- Existing SQL connection string and App Insights connection string env vars stay as direct Bicep secrets
- Deploy workflows (`deploy-infra.yml`, `deploy-api.yml`) unchanged
- `TelnyxOptions`, `TelnyxSmsSender`, and `Program.cs` DI wiring unchanged
- Parameter files unchanged (no new parameters needed)

## Testing Strategy

- Deploy infra to test environment (following the first-time setup steps above)
- Set real Telnyx secrets in Key Vault
- Verify Container App starts and resolves secrets (check logs for Telnyx configuration errors)
- Send a test SMS via the app to confirm end-to-end flow

## Future Use

When adding new external service secrets (email provider, payment processor), the pattern is:
1. Seed the secret in Key Vault (placeholder for first deploy, real value after)
2. Add a Key Vault reference entry in the Container App secrets array (with `keyVaultUrl` + `identity`)
3. Add the corresponding environment variable
