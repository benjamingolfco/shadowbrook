# Rate Schedules Frontend Design Spec

**Issue:** #401 — Configure rate schedules (frontend)
**Date:** 2026-04-14
**Depends on:** `2026-04-13-rate-schedules-design.md` (backend, already implemented)

## Overview

Operator-facing UI for managing course pricing: default price, min/max bounds, and rate schedules with day-of-week + time-band granularity. Lives as a dedicated "Pricing" page under a new "Settings" nav group in the operator sidebar.

## Navigation Changes

The sidebar currently has flat nav items: Dashboard, Schedule, Settings. This changes to a grouped layout:

```
Dashboard
Schedule
Settings          ← section header (non-clickable label)
  Tee Times       ← existing settings page, renamed
  Pricing         ← new page
```

"Settings" becomes a section header label (matching the existing "Course" label pattern above Dashboard). "Tee Times" and "Pricing" are always-visible nav items underneath — no collapsing.

### Route

`/course/{courseId}/manage/pricing`

The existing settings page at `/course/{courseId}/manage/settings` remains unchanged but gets the "Tee Times" label in the nav.

## Page Structure

The Pricing page has three cards stacked vertically. The page progressively reveals content based on configuration state.

### State 1: Fresh (no pricing configured)

- **Default Price card** — starts in edit mode (no value to view yet). Once saved, switches to view mode.
- **Rate Schedules section** — dashed border CTA: "Want different prices for different times? Set Up Price Bounds." Clicking shows the Price Bounds card in edit mode.

### State 2: Bounds configured, no schedules

- **Default Price card** — view/edit toggle with saved value
- **Price Bounds card** — view/edit toggle showing min/max
- **Rate Schedules section** — empty state: "No rate schedules yet. The default price applies to all tee times." with "+ Add Schedule" button

### State 3: Active (schedules exist)

- **Default Price card** — view/edit toggle
- **Price Bounds card** — view/edit toggle
- **Rate Schedules section** — list of schedules with "+ Add Schedule" button

## View/Edit Toggle Pattern

Default Price and Price Bounds cards use a shared interaction pattern:

**View mode** (default):
- Values displayed as large read-only text
- Small "Edit" button (with pencil icon) top-right of the card
- Subtle card border (standard `border-soft`)

**Edit mode** (after clicking Edit):
- Card border changes to primary color (visual cue for active editing)
- Values become editable input fields with primary-colored focus borders
- "Save" and "Cancel" buttons appear at the bottom of the card
- Edit button disappears
- Cancel reverts to view mode without saving
- Save calls the API, then returns to view mode with updated values

**Blocking**: While a card is in edit mode, other interactive elements on the page should feel secondary. No hard modal blocking needed — the border highlight and button placement make it clear where focus should be.

## Default Price Card

**View mode**: Shows "$XX.00" in large text.

**Edit mode**: Single dollar input field. Validates: required, must be a number > 0, must be within min/max bounds (if set). Calls `PUT /courses/{courseId}/pricing/default`.

## Price Bounds Card

Only visible after initial setup (or during setup flow from the CTA).

**View mode**: Shows "Minimum: $XX.00" and "Maximum: $XX.00" side by side.

**Edit mode**: Two dollar input fields (min and max). Validates: both required, both > 0, min < max. Calls `PUT /courses/{courseId}/pricing/bounds`.

**On save — invalid schedule handling**: When bounds change, the backend marks any rate schedules with prices outside the new bounds as invalid (see Invalid Schedule State below). The UI refreshes the schedule list after bounds save to reflect any newly invalid schedules.

## Rate Schedules Section

### Schedule List

Each schedule row displays:
- **Name** (left, bold) — e.g., "Weekend Morning"
- **Day pills** — compact abbreviation badges (Mon, Tue, etc.)
- **Time band** — "6:00 AM – 11:00 AM"
- **Price** (right, large) — "$65.00"
- **`⋯` menu** (far right) — dropdown with Edit and Delete actions

### Invalid Schedule State

When a schedule's price falls outside the current min/max bounds (after a bounds update), the backend marks it as invalid with a reason.

**Visual treatment**:
- Light warning background on the row
- "Invalid" badge (warning-colored pill) next to the schedule name
- Warning-colored reason text below the time band — e.g., "Price $90.00 exceeds maximum of $85.00"
- Price displayed with strikethrough in warning color

**Behavior**:
- Invalid schedules are **skipped** during price resolution — the default price applies instead
- Operator fixes by clicking `⋯` → Edit, adjusting the price to be within bounds, and saving
- On save, the schedule becomes active again (invalid state clears)

### Empty State

"No rate schedules yet. The default price applies to all tee times." Centered text in the card.

## Rate Schedule Dialog

A modal dialog used for both creating and editing rate schedules.

**Title**: "Add Rate Schedule" (create) / "Edit Rate Schedule" (edit)

### Fields

| Field | Type | Validation |
|-------|------|------------|
| Name | Text input | Required. Placeholder: "e.g., Weekend Morning, Twilight" |
| Days of Week | Day pill toggles (Mon–Sun) | At least one required |
| Start Time | Select/dropdown | Required. Options aligned to course tee time intervals |
| End Time | Select/dropdown | Required. Must be after start time |
| Price Per Player | Dollar input | Required, > 0, within min/max bounds |

**Price hint**: Below the price input, show "Must be between $XX.00 and $XX.00" using the current bounds.

**Day pill toggles**: Row of 7 compact buttons (Mon, Tue, Wed, Thu, Fri, Sat, Sun). Click to toggle on/off. Selected pills use primary background + white text. Unselected pills use subtle border + muted text.

**Error handling**: Server-side conflict errors (overlapping schedule with same specificity) display as an alert banner at the top of the dialog content.

**Actions**: "Cancel" (secondary) and "Save Schedule" (primary), right-aligned at the dialog bottom.

**Edit mode**: Pre-fills all fields with current values. Delete is **not** in this dialog — it's in the `⋯` menu on the list row.

### Delete Confirmation

When the operator clicks `⋯` → Delete, show a confirmation dialog (AlertDialog): "Delete [schedule name]? This rate schedule will be removed and the default price will apply to its time slots." with Cancel and Delete (destructive) buttons.

## Backend Changes Required

### Min/Max bounds required for rate schedules

The backend currently allows rate schedules without bounds. This changes: `MinPrice` and `MaxPrice` on `CoursePricingSettings` become required before any `RateSchedule` can be added. The aggregate should reject `AddRateSchedule` when bounds are not set.

### Invalid schedule state on RateSchedule

`RateSchedule` gains:
- `InvalidReason` (`string?`) — null when valid, set when price is outside bounds (e.g., "Price $90.00 exceeds maximum of $85.00")

When bounds are updated via `UpdateBounds()`, the aggregate iterates all schedules and sets/clears `InvalidReason` based on the new bounds.

`ResolvePrice()` skips schedules where `InvalidReason` is not null.

The GET pricing endpoint returns `invalidReason` on each schedule in the response.

## API Integration

| Action | Endpoint | Method |
|--------|----------|--------|
| Load pricing | `/courses/{courseId}/pricing` | GET |
| Update default price | `/courses/{courseId}/pricing/default` | PUT |
| Update bounds | `/courses/{courseId}/pricing/bounds` | PUT |
| Create schedule | `/courses/{courseId}/pricing/schedules` | POST |
| Update schedule | `/courses/{courseId}/pricing/schedules/{id}` | PUT |
| Delete schedule | `/courses/{courseId}/pricing/schedules/{id}` | DELETE |

### Query Keys

Use existing `queryKeys.courses.pricing(courseId)` for all pricing data. Invalidate after any mutation.

### Types

Update `Pricing` interface in `types/course.ts`:

```typescript
interface Pricing {
  defaultPrice: number | null;
  minPrice: number | null;
  maxPrice: number | null;
  rateSchedules: RateSchedule[];
}

interface RateSchedule {
  id: string;
  name: string;
  daysOfWeek: number[];    // 0=Sunday, 1=Monday, etc.
  startTime: string;        // "HH:mm"
  endTime: string;          // "HH:mm"
  price: number;
  invalidReason: string | null;
}
```

## File Structure

```
src/web/src/features/course/manage/
  pages/
    Pricing.tsx              # Main pricing page
    Settings.tsx             # Existing, unchanged (renamed to "Tee Times" in nav only)
  components/
    DefaultPriceCard.tsx     # View/edit toggle for default price
    PriceBoundsCard.tsx      # View/edit toggle for min/max bounds
    RateScheduleList.tsx     # Schedule list with empty/populated states
    RateScheduleDialog.tsx   # Create/edit dialog
    DayPills.tsx             # Day-of-week toggle component
  hooks/
    usePricing.ts            # GET query + all mutations
```

## Scope

**In scope:**
- Navigation restructure (Settings → section header with Tee Times + Pricing)
- Pricing page with progressive disclosure (3 states)
- View/edit toggle pattern for Default Price and Price Bounds
- Rate schedule CRUD via dialog
- Invalid schedule visual state
- Backend: require bounds for schedules, add InvalidReason to RateSchedule
- Delete confirmation dialog

**Out of scope:**
- Golfer-facing price display
- Dynamic pricing controls
- Price history / audit trail
- Bulk schedule operations
- Schedule reordering or priority display
