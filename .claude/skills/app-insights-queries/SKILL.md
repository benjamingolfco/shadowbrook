---
name: app-insights-queries
description: Canned KQL queries for Azure Application Insights. Use when debugging errors, investigating performance, or monitoring the system via az CLI.
---

# App Insights KQL Reference

Canned KQL queries for Azure Application Insights. Use these when debugging errors, investigating performance, or monitoring the system during stress tests or production incidents.

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

## Queries

### 1. Exceptions

Unhandled exceptions grouped by type and operation. Use to identify error hotspots.

```kql
exceptions
| where timestamp > ago(1h)
| summarize count() by type, operation_Name
| order by count_ desc
```

### 2. Failed Requests

4xx and 5xx responses by operation and status code. Use to identify broken endpoints.

```kql
requests
| where timestamp > ago(1h)
| where resultCode >= 400
| summarize count() by operation_Name, resultCode
| order by count_ desc
```

### 3. Dependency Failures

Failed outbound calls to SQL, external services, or downstream APIs. Use to find infrastructure problems.

```kql
dependencies
| where timestamp > ago(1h)
| where success == false
| summarize count() by type, target, name
| order by count_ desc
```

### 4. Slow Requests

Response time percentiles (p50, p95, p99) by operation. Use to identify latency outliers.

```kql
requests
| where timestamp > ago(1h)
| summarize
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99),
    count_ = count()
  by operation_Name
| order by p95 desc
```

### 5. Traces by Severity

Warnings and errors from Serilog/OpenTelemetry. Severity levels: 0=Verbose, 1=Information, 2=Warning, 3=Error, 4=Critical.

```kql
traces
| where timestamp > ago(1h)
| where severityLevel >= 2
| project timestamp, severityLevel, message, operation_Name, cloud_RoleInstance
| order by timestamp desc
```

### 6. Traces by Tenant

Filtered by OrganizationId custom dimension, added by the `OrganizationIdEnricher`. Replace `{organizationId}` with the tenant's organization ID.

```kql
traces
| where timestamp > ago(1h)
| where customDimensions["OrganizationId"] == "{organizationId}"
| project timestamp, severityLevel, message, operation_Name
| order by timestamp desc
```

### 7. Concurrency

Optimistic concurrency exceptions (`DbUpdateConcurrencyException`) and 409 Conflict responses. Use to detect contention during load.

```kql
union
(
    exceptions
    | where timestamp > ago(1h)
    | where type contains "DbUpdateConcurrencyException"
    | project timestamp, kind = "exception", detail = outerMessage, operation_Name
),
(
    requests
    | where timestamp > ago(1h)
    | where resultCode == 409
    | project timestamp, kind = "409_conflict", detail = name, operation_Name
)
| order by timestamp desc
```

### 8. Request Volume

Requests per minute for load monitoring. Use during stress tests to track throughput over time.

```kql
requests
| where timestamp > ago(1h)
| summarize count() by bin(timestamp, 1m)
| order by timestamp asc
```

### 9. End-to-End Flow

Correlated traces for a single operation ID — requests, dependencies, traces, and exceptions in one view. Replace `{operationId}` with the target operation ID from a specific request.

```kql
union
(
    requests
    | where operation_Id == "{operationId}"
    | project timestamp, kind = "request", name, resultCode = tostring(resultCode), duration, operation_Id
),
(
    dependencies
    | where operation_Id == "{operationId}"
    | project timestamp, kind = "dependency", name, resultCode = tostring(resultCode), duration, operation_Id
),
(
    traces
    | where operation_Id == "{operationId}"
    | project timestamp, kind = "trace", name = message, resultCode = tostring(severityLevel), duration = 0.0, operation_Id
),
(
    exceptions
    | where operation_Id == "{operationId}"
    | project timestamp, kind = "exception", name = outerMessage, resultCode = type, duration = 0.0, operation_Id
)
| order by timestamp asc
```

### 10. Error Spike Detection

Server errors (5xx) per minute. Use for circuit breaker logic or alerting on error rate spikes during stress tests.

```kql
requests
| where timestamp > ago(1h)
| where resultCode >= 500
| summarize error_count = count() by bin(timestamp, 1m)
| order by timestamp asc
```
