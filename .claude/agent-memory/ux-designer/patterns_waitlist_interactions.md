---
name: Walk-Up Waitlist Interaction Patterns
description: Established UX decisions and component patterns for the Walk-Up Waitlist feature
type: project
---

## Confirmation Dialog Policy

- **Keep confirmation dialogs for:** Remove Golfer, Cancel Opening, Close Waitlist — these are destructive or operationally significant
- **Remove confirmation dialog for:** Open Waitlist — non-destructive, easily reversed, dialog adds friction with no safety benefit
- **Reopen Waitlist:** keep the confirmation — reopening after an intentional close is meaningful

## QR Code Panel

- QR code is removed from the main content area entirely
- Access via "Print sign" text link in the page header (Zone 1), which opens a Sheet/Dialog containing the QrCodePanel component
- It is a first-day setup action — not ambient content on every page load
- QrCodePanel component itself is unchanged; only its placement changes

## Short Code Display

- Short code sits inline in the page header next to the status badge (always visible when open)
- Displayed as spaced monospace characters (e.g., "A B 4 7 X"), text-lg, font-mono, font-bold, tracking-widest
- Copy action: icon button (Lucide `Copy`, 16px) immediately to the right — icon changes to `Check` for 2000ms on copy, no toast
- "Print sign" text link below the short code opens the QR modal

## Close Waitlist Placement

- Close Waitlist lives in an infrequent actions zone at the bottom of the active page (Zone 4)
- Rendered as a text link — text-sm text-muted-foreground, turns text-destructive on hover
- Must NOT be in the PageHeader actions array alongside working actions
- End-of-day action, not a moment-to-moment action

## Action Button/Label Conventions

- "Add Tee Time Opening" → "Post Tee Time" (operator vocabulary)
- "Add Golfer" is clear as-is
- "Add Golfer to Waitlist" in the inactive card → move to infrequent Zone 4 in active state
- "Open Waitlist" is acceptable on the inactive state card (one-time-per-day action)

## Post Tee Time Is Inline, Not a Dialog

- The primary posting action uses an inline form inside a Card in Zone 2
- No dialog, no confirmation — operator types time, selects slots, presses Post
- Slots use a segmented button group (4 adjacent toggle buttons for 1/2/3/4), NOT a Select dropdown
- Time field defaults to next 10-minute interval from current course time
- Time input receives auto-focus on page load so keyboard-only use is possible
- Focus returns to time input after successful submission to enable rapid sequential posting

## Tab Pattern Eliminated

- Tabs are inappropriate for queue + openings on this page
- Queue is a collapsible pill in the header (count + "View queue" toggle)
- Openings list is the full main content
- Both datasets are always available simultaneously

## Openings List Layout

- Flat list with dividers (not individual cards per row) — faster to scan
- Row anatomy (L→R): time (bold, 80px fixed) → status badge (80px fixed) → fill indicator text → golfer names (flexible) → cancel action (text link, Open only)
- Visual state by status:
  - Open: normal background
  - Filled: left border 3px solid green
  - Expired: opacity-60
  - Cancelled: opacity-50
- Cancelling mutation: row → opacity-40, spinner replaces cancel link

## Queue Display

- Default: collapsed — only a count pill in Zone 1 header: "**N** waiting"
- Count 0: full pill in text-muted-foreground
- Count 1+: number in font-bold text-foreground, "waiting" in text-muted-foreground
- "View queue" text link expands an inline section (not a drawer) below Zone 1
- Expanded queue: ordered list rows (position, name, group size, joined time, Remove link)

## Error Handling

- All mutation errors appear inline near the triggering action — no toasts
- Specific locations: form validation below the invalid field; API errors below the form row; row-level errors inline after the affected row
- Page-level load errors use an error card (not just a text paragraph)

## Inactive State

- Single large CTA card centered in the content area — no UI scaffold before waitlist starts
- Card has icon + headline + description + one prominent "Open Waitlist for Today" button
- 409 error (already open): "Waitlist is already open — try refreshing the page."

## Closed State

- Post Tee Time form hidden — can't post after closing
- Openings list is read-only historical record
- Short code hidden (no reason to share)
- Reopen button: outline variant, small, top-right of header — available but not prominent

## Add/Remove Feedback

- New opening posted: row slides in at correct list position (opacity-0 + translate-y offset → normal, 200ms), brief green-50 background fade over 800ms
- Opening transitions to Filled: left border accent animates in (300ms transition-colors)
- Remove/cancel pending: row fades to opacity-40, cancel link replaced by spinner
- Inline error on failure, row returns to full opacity

## Page Width

- Content area max-width: 860px (not the current max-w-2xl / 672px)
- The wider constraint is required to accommodate the horizontal inline form and multi-column openings list
