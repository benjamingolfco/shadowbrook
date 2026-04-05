# Production Checklist

Checklist for standing up a new environment (staging, production). Derived from the test environment subscription transfer.

## Azure Subscription & Tenant

- [ ] Subscription exists in the correct Entra tenant (Benjamin Golf Co)
- [ ] Admin user has Owner role on subscription (portal-only, not used in CLI)
- [ ] Separate non-prod user for Azure CLI / AI sessions — Contributor on non-prod resource groups only, no prod access
- [ ] Separate prod deploy credential (service principal or user) — Contributor scoped to `shadowbrook-prod-rg` + AcrPush on shared ACR, used only by manually-triggered workflows
- [ ] Resource providers registered (Microsoft.App, Microsoft.Sql, Microsoft.Web, etc.)

## Infrastructure (Bicep)

- [ ] If transferring subscription: delete old managed identity first (`az identity delete`), then redeploy
- [ ] Shared resources deployed (`deploy.sh shared`) — ACR
- [ ] Environment resources deployed (`deploy.sh {env}`) — SQL, Container App, SWA, App Insights, Log Analytics
- [ ] Managed identity created in correct tenant
- [ ] ACR pull role assigned to managed identity
- [ ] Container App configured with managed identity client ID

## Database

- [ ] Azure SQL Server created with Entra-only authentication
- [ ] Managed identity set as SQL Server admin
- [ ] Firewall allows Azure services
- [ ] EF Core migrations applied (on first app startup)

## Authentication

- [ ] App registrations created in correct tenant (API + SPA)
- [ ] API exposes `access_as_user` scope
- [ ] SPA has delegated permission to API scope (admin consent granted)
- [ ] SPA redirect URIs configured (production domain)
- [ ] `appsettings.{Environment}.json` has correct TenantId, ClientId, Audience
- [ ] Frontend env vars point to correct authority and client ID
- [ ] `email` optional claim configured on API app registration (for guest/external users)

## Graph API (User Invitations)

- [ ] Run `grant-graph-permissions.sh {env}` — grants `User.Invite.All` to managed identity (must run after Bicep deploy creates new identity)
- [ ] `App__FrontendUrl` set correctly (invitation redirect URL)
- [ ] Verify invitation creates user in correct tenant

## GitHub Actions CI/CD

- [ ] Run `setup-github-oidc.sh` — creates app registration, service principal, federated credentials, RBAC, and GitHub secrets
- [ ] Update SWA deployment token: `az staticwebapp secrets list ... | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN`
- [ ] Deploy workflows tested end-to-end

## DNS & Custom Domain

- [ ] Custom domain added to Static Web App (frontend)
- [ ] Custom domain added to Container App (API) if needed
- [ ] SSL/TLS certificates provisioned (auto-managed by Azure)

## Observability

- [ ] Application Insights connected and receiving telemetry
- [ ] Log Analytics workspace configured with appropriate retention
- [ ] Daily cap set on Log Analytics to control costs
- [ ] Health endpoint (`/health`) accessible and returning 200

## SMS (Notifications)

- [ ] Twilio (or SMS provider) credentials configured
- [ ] `ITextMessageService` implementation registered (not NoOp)

## Pre-Launch Verification

- [ ] App starts and passes health check
- [ ] Auth flow works end-to-end (sign in, token validation, API call)
- [ ] SQL connection works via managed identity
- [ ] Graph invitation creates user in correct tenant
- [ ] SMS notifications deliver
- [ ] CORS configured for production domain
- [ ] No dev/test secrets in production config
