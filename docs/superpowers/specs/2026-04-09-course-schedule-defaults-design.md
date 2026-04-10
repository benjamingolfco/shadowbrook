# Course Schedule Defaults — Design Spec

**Issue:** #397 — Configure course schedule defaults
**Date:** 2026-04-09

## Summary

Extend the existing tee-time-settings endpoint and operator settings page to include `DefaultCapacity`, and relax the interval validator from a fixed 8/10/12 enum to any positive integer with a soft warning below 8.

## Scope

Four fields, one endpoint, one form: first tee time, last tee time, interval in minutes, default group capacity.

## What Already Exists

- **Domain**: `Course` aggregate has `TeeTimeIntervalMinutes`, `FirstTeeTime`, `LastTeeTime`, `DefaultCapacity` properties with `UpdateTeeTimeSettings()` and `UpdateDefaultCapacity()` methods. `ScheduleSettings` value object validates all four fields.
- **Database**: All four columns exist on the Course table. No migration needed.
- **API**: `GET/PUT /courses/{courseId}/tee-time-settings` endpoints exist but DTOs exclude `DefaultCapacity`. Validator restricts interval to 8, 10, or 12.
- **Frontend**: `TeeTimeSettings.tsx` page with form for interval (select), first/last tee time. Missing capacity field.

## Changes

### Backend

**DTOs** (`CourseEndpoints.cs`):
- Add `int DefaultCapacity` to `TeeTimeSettingsRequest` and `TeeTimeSettingsResponse`.

**Validator** (`TeeTimeSettingsRequestValidator`):
- Change interval rule from "must be 8, 10, or 12" to "must be greater than 0".
- Add `DefaultCapacity` rule: must be greater than 0.

**PUT handler**:
- After `course.UpdateTeeTimeSettings(...)`, call `course.UpdateDefaultCapacity(request.DefaultCapacity)`.

**GET handler**:
- Include `course.DefaultCapacity` in response.

### Frontend

**Type** (`course.ts`):
- Add `defaultCapacity: number` to `TeeTimeSettings` interface.

**Zod schema** (`TeeTimeSettings.tsx`):
- Add `defaultCapacity: z.number().int().min(1, "Must be at least 1")`.

**Form** (`TeeTimeSettings.tsx`):
- Change interval from `<Select>` (8/10/12) to a number input.
- Show a non-blocking warning (yellow alert) when interval < 8.
- Add a number input for "Default Group Size" with min 1.

**Hook** (`useTeeTimeSettings.ts`):
- No structural changes — the existing fetch/mutate handles the full DTO. Just ensure `defaultCapacity` flows through.

### Validation Layers

| Field | Frontend (Zod) | Backend (FluentValidation) | Domain |
|-------|---------------|---------------------------|--------|
| Interval | `int, min(1)`, warn < 8 | `> 0` | `> 0` (ScheduleSettings) |
| First tee | required | `< LastTeeTime` | `< LastTeeTime` |
| Last tee | required | `> FirstTeeTime` | `> FirstTeeTime` |
| Capacity | `int, min(1)` | `> 0` | `> 0` |

## Out of Scope

- Per-interval capacity overrides
- Per-day schedule variations
- Any new endpoints or pages
