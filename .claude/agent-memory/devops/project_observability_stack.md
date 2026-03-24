---
name: Observability stack Bicep modules
description: Log Analytics workspace and Application Insights added to infra; wired to Container Apps Environment and Container App
type: project
---

Two new Bicep modules were added to the observability stack and wired into the main orchestration template.

**Why:** The platform needed structured telemetry — container logs routed to a central sink and App Insights for distributed tracing/metrics in the .NET API.

**How to apply:** When touching any infra module that needs telemetry context, these resources now exist in every environment deployment:

- `infra/bicep/modules/log-analytics.bicep` — `shadowbrook-logs-{env}` workspace, 30-day retention, PerGB2018 SKU. Outputs: `id`, `customerId`, `sharedKey`, `name`.
- `infra/bicep/modules/app-insights.bicep` — `shadowbrook-insights-{env}`, workspace-based (linked to Log Analytics). Outputs: `connectionString`, `instrumentationKey`, `name`.
- `infra/bicep/modules/container-app-env.bicep` — now requires `logAnalyticsWorkspaceCustomerId` and `@secure() logAnalyticsSharedKey`; sets `appLogsConfiguration` on the managed environment.
- `infra/bicep/modules/container-app.bicep` — now requires `@secure() appInsightsConnectionString`; stored as secret `app-insights-connection-string`, surfaced as env var `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- `infra/bicep/main.bicep` — `logAnalytics` and `appInsights` modules placed in the "independent resources" section (parallel with database/SWA/identity). `containerAppEnv` depends on `logAnalytics` outputs (implicit dependency). New outputs: `appInsightsName`, `logAnalyticsName`.

**Linter note:** `az bicep build` emits `outputs-should-not-contain-secrets` on `log-analytics.bicep` because `listKeys()` appears in an output. This is a known false-positive for this cross-module wiring pattern — the shared key flows directly into a `@secure()` param and is never exposed in deployment outputs.
