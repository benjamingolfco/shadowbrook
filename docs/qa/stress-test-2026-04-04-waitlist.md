# Stress Test Report — waitlist — 2026-04-04

**Duration:** 12 minutes  
**Environment:** test (https://white-stone-00610060f.1.azurestaticapps.net)  
**Agents:** 1 operator + 3 golfers + 1 observer  
**Hint:** none  
**Exit reason:** Circuit breaker — memory exceeded 1,000 MB  
**Context:** First stress test after replacing OTEL SDK with Serilog App Insights sink (PR #355)

---

## Memory Timeline

| Time (UTC) | Memory (MB) | Notes |
|------------|-------------|-------|
| 15:00 | 162 | Post-deploy baseline (new revision) |
| 15:05 | 175 | Pre-stress-test idle |
| 15:06 | 471 | Traffic starting |
| 15:07 | 606 | |
| 15:08 | 763 | |
| 15:09 | 897 | |
| 15:10 | 1,083 | **Circuit breaker threshold** |
| 15:11 | 1,123 | |
| 15:12 | 1,154 | |
| 15:13 | 1,195 | Peak |
| 15:14 | 1,187 | Slight recovery |
| 15:15–15:17 | ~1,190 | Flat — no recovery |

**Idle baseline:** 162 MB (down from ~280 MB with OTEL — sink is lighter)  
**Peak under load:** 1,195 MB  
**Growth:** ~1,020 MB during stress test, no recovery after traffic stopped

## Telemetry Validation (Serilog Sink)

| Check | Result |
|-------|--------|
| Traces flowing to App Insights | Yes (240+ traces) |
| Exceptions table populated | Yes (6 exceptions) |
| customDimensions (OrganizationId, SourceContext, etc.) | Yes — working correctly |
| Requests table (expected empty after OTEL removal) | Confirmed empty |
| Dependencies table (expected empty) | Confirmed empty |

**Conclusion:** Serilog App Insights sink is working correctly as a replacement for the OTEL SDK.

## Operator Actions

**Login:** success  
**Actions completed:** 18

| Time | Action | Result | Notes |
|------|--------|--------|-------|
| 15:05:06 | Navigate to operator portal | success | Waitlist open, code 8695, 7 golfers waiting |
| 15:05:19 | Post tee time 15:30 / 4 slots | success | |
| 15:07:03 | Post tee time 16:00 / 2 slots | failed | Time input validation bug |
| 15:08:30 | Post tee time 16:00 / 3 slots (retry) | success | |
| 15:09:50 | Post tee time 16:30 / 4 slots | success | |
| 15:10:07 | Cancel 3:00 PM opening | success | |
| 15:10:25 | Repost 15:00 / 2 slots | success | |
| 15:10:55 | Add golfer manually (group 2) | success | |
| 15:11:30 | Add golfer manually (group 1) | success | |
| 15:12:00 | Post tee time 17:00 / 3 slots | success | |
| 15:12:15 | Cancel 3:30 PM opening | success | |
| 15:12:40 | Post tee time 17:30 / 4 slots | success | |
| 15:13:00 | Post duplicate 15:30 / 1 slot | success | Accepted as separate opening |
| 15:14:05 | Post tee time 18:30 / 4 slots | success | |
| 15:15:05 | Post tee time 17:00 / 2 slots | success | |
| 15:15:30 | Post tee time 19:00 / 1 slot | success | |
| 15:15:55 | Post tee time 19:30 / 3 slots | success | |
| 15:16:40 | Post tee time 20:00 / 2 slots | success | |
| 15:17:08 | Final state | — | 13 openings, 0/30 filled, 11 golfers waiting |

## Golfer Results

| Golfer | Phone | Joined Waitlist | Received Offer | Accepted | Notes |
|--------|-------|-----------------|----------------|----------|-------|
| 1 | +15555550010 | Already on (prior run) | Old offer (2:10 AM) | Failed (409 — expired) | Correctly rejected expired offer |
| 2 | +15555550020 | Yes (prior run) | No new offers | N/A | Polled SMS repeatedly, no offers dispatched |
| 3 | +15555550030 | Yes (prior run) | No new offers | N/A | Polled SMS repeatedly, no offers dispatched |

## Contention Events

- **15:07:25:** 409 Conflict on offer accept — golfer tried to claim expired 2:10 AM opening. System correctly rejected.
- **15:06:19, 15:06:57:** GolferAlreadyOnWaitlistException (2x) — duplicate join attempts for +15555550010. Domain correctly enforced uniqueness.
- **15:16:20:** 409 Conflict on duplicate tee time post — UI showed "An opening already exists for this time." Handled gracefully.

## Observer Findings

### Errors
- `UnknownSagaException` (4 occurrences) — Wolverine saga timing: follow-up message arrived before saga state persisted. Retried successfully.
- `GolferAlreadyOnWaitlistException` (2 occurrences) — Expected under concurrent stress testing.

### Warnings
- None observed.

### Concurrency Events
- Saga timing issues (UnknownSagaException) resolved by Wolverine retry — not a data integrity issue.
- Duplicate join correctly rejected at domain level.

### Flow Gaps
- **No offers dispatched to new golfers:** 13 openings posted with 11 golfers waiting, but 0 slots filled. The offer matching/dispatch system did not send offers during the test session. This may indicate the matching service requires manual trigger, or there's a timing/configuration issue with the offer policy.
- **Memory does not recover:** Working set grew from 175 MB to 1,195 MB and stayed flat after traffic stopped. This is NOT caused by OTEL (removed in this build). The root cause is elsewhere — likely Wolverine message processing, Server GC retention, or the Serilog App Insights TelemetryClient buffer.

---

## Summary

The Serilog App Insights sink migration (PR #355) is working correctly — structured logs flow to App Insights with full customDimensions, and the idle baseline dropped from ~280 MB to ~162 MB. However, memory still grows to ~1,200 MB under real Wolverine workloads (saga processing, domain events, message handling) and does not recover. The OTEL SDK accounted for only the idle overhead; the load-driven memory growth is a separate issue rooted in .NET Server GC behavior or Wolverine's message processing pipeline. No offers were dispatched to golfers despite openings being posted, which limited the stress test's ability to exercise the full waitlist flow.

### Issues Found

- **Memory growth under load persists without OTEL** — working set grows ~130 MB/min under moderate traffic and never recovers. Root cause needs local diagnostics with `dotnet-counters` under real Wolverine workload.
- **No offers dispatched** — 13 openings posted, 11 golfers waiting, 0 offers sent. Offer matching/dispatch may require investigation.
- **Time input validation bug** — Playwright `.fill()` on `<input type="time">` fails after first use without page reload. RHF/Zod validation state not resetting properly after form submission.
