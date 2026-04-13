# Rate Schedules Design Spec

**Issue:** #401 — Configure rate schedules
**Date:** 2026-04-13

## Overview

Course operators need to define pricing rules — a default per-player price and optional rate schedules with day-of-week + time-band granularity. Prices are resolved at draft time, propagated on changes, and locked in at booking time. The design sets up for dynamic pricing by decoupling live interval prices from booked prices.

## Domain Model

### CoursePricingSettings (new aggregate root)

Lives in `Teeforce.Domain/CoursePricingAggregate/`. One per course.

| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | V7 |
| CourseId | Guid | Unique — one per course |
| DefaultPrice | decimal? | Fallback when no schedule matches |
| MinPrice | decimal? | Floor for all prices (guard rail for dynamic pricing) |
| MaxPrice | decimal? | Ceiling for all prices |
| RateSchedules | List\<RateSchedule\> | Owned entities |
| CreatedAt | DateTimeOffset | |

### RateSchedule (owned entity inside CoursePricingSettings)

| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | V7 |
| Name | string | Required — e.g., "Weekend Morning" |
| DaysOfWeek | DayOfWeek[] | At least one required |
| StartTime | TimeOnly | Inclusive |
| EndTime | TimeOnly | Exclusive |
| Price | decimal | Must be > 0, within min/max bounds when set |

### Invariants (enforced by aggregate root)

- **No conflicting schedules:** Two schedules with the same specificity (same day count and same time band width) cannot overlap on any day+time combination. Checked on add and update.
- **Price bounds:** When MinPrice/MaxPrice are set, all schedule prices and DefaultPrice must fall within bounds.
- **Specificity resolution order:** Single-day schedules beat multi-day schedules. Within the same day granularity, narrower time band (EndTime - StartTime) wins.

### Conflict Detection Algorithm

Two schedules conflict when:
1. They share at least one day of week
2. Their time bands overlap (A.StartTime < B.EndTime AND B.StartTime < A.EndTime)
3. They have the same specificity — same number of days AND same time band duration

If conditions 1+2 are met but specificity differs, no conflict — the more specific one wins at resolution time.

### Price Resolution Algorithm

Given a `DayOfWeek` and `TimeOnly`:
1. Find all schedules where the day is in DaysOfWeek AND StartTime <= time < EndTime
2. Sort matches: fewer days first (single-day before multi-day), then narrower time band ascending
3. Return first match's Price
4. If no match, return DefaultPrice
5. If no DefaultPrice, return null (no price configured)

This is a method on the aggregate: `ResolvePrice(DayOfWeek day, TimeOnly time) → decimal?`

## Migration from Course.FlatRatePrice

`Course.FlatRatePrice` moves to `CoursePricingSettings.DefaultPrice`. A data migration:
1. Creates a `CoursePricingSettings` row for each existing course
2. Copies `Course.FlatRatePrice` → `CoursePricingSettings.DefaultPrice`
3. Drops `Course.FlatRatePrice` column

## Price Stamping on TeeSheetInterval

TeeSheetInterval gains two properties:

| Property | Type | Notes |
|----------|------|-------|
| Price | decimal? | Resolved price at draft/reprice time |
| RateScheduleId | Guid? | Which schedule produced it (null = default rate) |

### Draft-time stamping

When `TeeSheet.Draft()` is called, the caller also provides resolved pricing. For each interval, the resolved price and source schedule ID are stamped. This follows the same pattern as capacity being stamped from ScheduleSettings.

`TeeSheet.Draft()` signature gains a pricing parameter — either a price map or a resolver function that maps (DayOfWeek, TimeOnly) → (decimal?, Guid?).

### Repricing published sheets

`TeeSheet.ApplyPricing(...)` — new method:
- Accepts resolved prices (map of TimeOnly → price + scheduleId)
- Updates Price and RateScheduleId on each interval
- Works on both Draft and Published status
- No restriction on intervals with existing bookings — the claim already captured the booking-time price

### Propagation on pricing changes

When `CoursePricingSettings` is modified, it raises `PricingSettingsChanged`:

```
PricingSettingsChanged
├── CourseId (Guid)
```

A handler:
1. Loads all future tee sheets for the course (draft + published, date >= today)
2. Resolves prices using the updated CoursePricingSettings
3. Calls `TeeSheet.ApplyPricing()` on each

This means operator pricing changes take effect immediately on all future tee sheets.

## Booking-Time Price Lock-in

TeeTimeClaim gains:

| Property | Type | Notes |
|----------|------|-------|
| Price | decimal? | Price per player at booking time — immutable |

At booking time:
1. `TeeTime.Claim()` already receives the `TeeSheetInterval` — it reads `interval.Price`
2. Stamps it on the `TeeTimeClaim`
3. The claim's Price never changes, even if the interval is repriced later

A null Price on the interval means "no price configured" — booking is still allowed. The claim captures null, and downstream consumers (Booking aggregate) treat it as unpriced/free. This avoids blocking bookings when pricing hasn't been set up yet.

The `TeeTimeClaimed` domain event also carries the Price for downstream consumers (e.g., the Booking aggregate).

## API Endpoints

All under `Features/Pricing/`:

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/courses/{courseId}/pricing` | Returns default price, min/max, and all rate schedules |
| PUT | `/courses/{courseId}/pricing/default` | Update default price |
| PUT | `/courses/{courseId}/pricing/bounds` | Update min/max price |
| POST | `/courses/{courseId}/pricing/schedules` | Create a rate schedule |
| PUT | `/courses/{courseId}/pricing/schedules/{scheduleId}` | Update a rate schedule |
| DELETE | `/courses/{courseId}/pricing/schedules/{scheduleId}` | Remove a rate schedule |

Existing `PUT /courses/{courseId}/pricing` (which sets FlatRatePrice) is replaced by the new routes.

## Dynamic Pricing Readiness

This architecture prepares for dynamic pricing:

- **RateScheduleId on interval** — provenance: you know the base price source
- **MinPrice/MaxPrice on aggregate** — guard rails that dynamic pricing cannot breach
- **ApplyPricing() on TeeSheet** — the same mechanism dynamic pricing will use, just with a different trigger
- **Price mutable on intervals, immutable on claims** — live prices can change freely; booked prices are locked

Future dynamic pricing flow:
1. Dynamic pricing engine calculates adjusted prices based on demand, weather, time-to-tee-off, etc.
2. Calls `TeeSheet.ApplyPricing()` with adjusted prices (clamped to min/max)
3. Existing bookings unaffected — their price is on the claim

## Scope

**In scope:**
- CoursePricingSettings aggregate with CRUD
- Rate schedule conflict detection and specificity resolution
- Price stamping on TeeSheetInterval at draft time
- Price propagation on pricing changes (PricingSettingsChanged handler)
- Price lock-in on TeeTimeClaim at booking time
- Migration of Course.FlatRatePrice
- API endpoints for pricing management
- Domain and unit tests

**Out of scope:**
- Frontend UI (separate issue)
- Dynamic pricing engine
- Date ranges / seasonal schedules
- Price history / audit log
