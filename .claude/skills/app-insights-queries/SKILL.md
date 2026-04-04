---
name: app-insights-queries
description: Canned KQL queries for Azure Application Insights. Use when debugging errors, investigating performance, or monitoring the system via az CLI.
---

# App Insights KQL Reference

Canned KQL queries for Azure Application Insights. Use these when debugging errors, investigating performance, or monitoring the system during stress tests or production incidents.

## Logging Architecture

- **Deployed environments (Test, Production):** Serilog App Insights sink only — populates `traces` and `exceptions` tables with structured properties (including `OrganizationId` in `customDimensions`). No console output (no stdout). No OTEL SDK — `requests` and `dependencies` tables are **empty**.
- **Development:** Human-readable console output only (no App Insights sink unless connection string is set).
- **Bootstrap logger:** Writes to console in all environments for startup errors before the host builds.
- **Daily cap:** Log Analytics workspace has a 1 GB/day cap (raised from 0.1 GB after PR #349). Health probe logs and verbose `Microsoft.IdentityModel` logs are suppressed to stay within budget.

### Available Tables

| Table | Populated? | Source |
|-------|-----------|--------|
| `traces` | Yes | Serilog App Insights sink |
| `exceptions` | Yes | Serilog App Insights sink |
| `requests` | No | Would require OTEL SDK |
| `dependencies` | No | Would require OTEL SDK |

Queries below that reference `requests` or `dependencies` will return empty results. Use `traces` queries for request-level debugging (Serilog request logging writes status codes and durations as structured properties).

## Prerequisites

- Logged in to Azure CLI: `az login`
- App Insights resource name: replace `{appInsightsName}` with the actual resource (e.g., `shadowbrook-insights-test`)
- Resource group: replace `{resourceGroup}` with the actual resource group (e.g., `shadowbrook-test-rg`)
- Time range: replace `{timespan}` with an ISO 8601 duration (e.g., `PT1H` = last 1 hour, `PT30M` = last 30 minutes, `P1D` = last 1 day)

## Running Queries

Use `az monitor app-insights query` to execute any KQL query against Application Insights:

```bash
az monitor app-insights query \
  --app {appInsightsName} \
  --resource-group {resourceGroup} \
  --analytics-query '{KQL_QUERY}' \
  --timespan {timespan}
```

Results are returned as JSON with a `tables` array containing `columns` and `rows`.

### customDimensions Fields

Serilog request logging (`UseSerilogRequestLogging`) writes these fields to `customDimensions` on request completion traces:

| Field | Type | Example | Notes |
|-------|------|---------|-------|
| `RequestMethod` | string | `GET` | HTTP method |
| `RequestPath` | string | `/courses/{id}/waitlist` | Route path |
| `StatusCode` | string | `200` | HTTP status (string, cast to int for comparisons) |
| `Elapsed` | string | `45.123` | Duration in ms (string, cast to real for math) |
| `OrganizationId` | string | GUID | Tenant ID from `OrganizationIdEnricher` |
| `SourceContext` | string | `Serilog.AspNetCore.RequestLoggingMiddleware` | Logger source — filter on this for request traces |
| `EnvironmentName` | string | `Test` | Hosting environment |
| `MachineName` | string | container hostname | Pod/container name |
| `ConnectionId` | string | `0HNKIBA4MADT5` | Kestrel connection ID |
| `RequestId` | string | `0HNKIBA4MADT5:00000001` | Kestrel request ID |

Application log traces have `SourceContext` set to the logger's class name (e.g., `Microsoft.EntityFrameworkCore.Migrations`).

## Queries

### 1. Exceptions

Unhandled exceptions grouped by type. Use to identify error hotspots.

```kql
exceptions
| where timestamp > ago(1h)
| summarize count() by type, outerMessage
| order by count_ desc
```

### 2. Failed Requests

4xx and 5xx responses by path and status code. Excludes health probes.

```kql
traces
| where timestamp > ago(1h)
| where customDimensions["SourceContext"] == "Serilog.AspNetCore.RequestLoggingMiddleware"
| where customDimensions["RequestPath"] != "/health"
| extend StatusCode = toint(customDimensions["StatusCode"])
| where StatusCode >= 400
| summarize count() by tostring(customDimensions["RequestMethod"]), tostring(customDimensions["RequestPath"]), StatusCode
| order by count_ desc
```

### 3. Slow Requests

Response time percentiles (p50, p95, p99) by endpoint. Excludes health probes.

```kql
traces
| where timestamp > ago(1h)
| where customDimensions["SourceContext"] == "Serilog.AspNetCore.RequestLoggingMiddleware"
| where customDimensions["RequestPath"] != "/health"
| extend Elapsed = toreal(customDimensions["Elapsed"])
| summarize
    p50 = percentile(Elapsed, 50),
    p95 = percentile(Elapsed, 95),
    p99 = percentile(Elapsed, 99),
    count_ = count()
  by tostring(customDimensions["RequestMethod"]), tostring(customDimensions["RequestPath"])
| order by p95 desc
```

### 4. Warnings and Errors

Application log messages at Warning (2) or above. Excludes request completion logs. Severity levels: 0=Verbose, 1=Information, 2=Warning, 3=Error, 4=Critical.

```kql
traces
| where timestamp > ago(1h)
| where severityLevel >= 2
| where customDimensions["SourceContext"] != "Serilog.AspNetCore.RequestLoggingMiddleware"
| project timestamp, severityLevel, message, tostring(customDimensions["SourceContext"]), tostring(customDimensions["OrganizationId"])
| order by timestamp desc
```

### 5. Traces by Tenant

All activity for a specific tenant. Replace `{organizationId}` with the tenant's organization ID.

```kql
traces
| where timestamp > ago(1h)
| where customDimensions["OrganizationId"] == "{organizationId}"
| project timestamp, severityLevel, message, tostring(customDimensions["RequestPath"]), tostring(customDimensions["StatusCode"])
| order by timestamp desc
```

### 6. Request Volume

Requests per minute, excluding health probes. Use during stress tests to track throughput.

```kql
traces
| where timestamp > ago(1h)
| where customDimensions["SourceContext"] == "Serilog.AspNetCore.RequestLoggingMiddleware"
| where customDimensions["RequestPath"] != "/health"
| summarize count() by bin(timestamp, 1m)
| order by timestamp asc
```

### 7. Error Spike Detection

5xx responses per minute. Use for circuit breaker logic or alerting during stress tests.

```kql
traces
| where timestamp > ago(1h)
| where customDimensions["SourceContext"] == "Serilog.AspNetCore.RequestLoggingMiddleware"
| extend StatusCode = toint(customDimensions["StatusCode"])
| where StatusCode >= 500
| summarize error_count = count() by bin(timestamp, 1m)
| order by timestamp asc
```

### 8. Concurrency Events

Optimistic concurrency exceptions and 409 Conflict responses. Use to detect contention under load.

```kql
union
(
    exceptions
    | where timestamp > ago(1h)
    | where type contains "DbUpdateConcurrencyException"
    | project timestamp, kind = "exception", detail = outerMessage
),
(
    traces
    | where timestamp > ago(1h)
    | where customDimensions["SourceContext"] == "Serilog.AspNetCore.RequestLoggingMiddleware"
    | where customDimensions["StatusCode"] == "409"
    | project timestamp, kind = "409_conflict", detail = strcat(tostring(customDimensions["RequestMethod"]), " ", tostring(customDimensions["RequestPath"]))
)
| order by timestamp desc
```

### 9. Endpoint Breakdown

Request count and avg duration by endpoint. Use to understand traffic distribution.

```kql
traces
| where timestamp > ago(1h)
| where customDimensions["SourceContext"] == "Serilog.AspNetCore.RequestLoggingMiddleware"
| where customDimensions["RequestPath"] != "/health"
| extend Elapsed = toreal(customDimensions["Elapsed"])
| summarize
    count_ = count(),
    avg_ms = round(avg(Elapsed), 1),
    max_ms = round(max(Elapsed), 1)
  by tostring(customDimensions["RequestMethod"]), tostring(customDimensions["RequestPath"])
| order by count_ desc
```

### 10. Recent Exceptions with Stack Traces

Full exception details for debugging. Shows the most recent exceptions with type, message, and stack trace.

```kql
exceptions
| where timestamp > ago(1h)
| project timestamp, type, outerMessage, details
| order by timestamp desc
| take 20
```

### 11. Ingestion Budget

Data volume by table over the last 24 hours. Use to monitor daily cap usage (1 GB/day limit).

```kql
union withsource=TableName traces, exceptions
| where timestamp > ago(24h)
| summarize
    count_ = count(),
    size_MB = round(estimate_data_size(*) / 1048576.0, 2)
  by TableName
| order by size_MB desc
```
