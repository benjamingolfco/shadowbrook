# Operator Screen Pattern & Walkup Waitlist UI Redesign

**Date:** 2026-03-06
**Status:** Approved

## Goal

Establish a reusable operator screen pattern — action bar at the top, dialog-based flows — and apply it to the walkup waitlist page as the first implementation.

## The Pattern

Every operator page follows this layout:

```
┌─────────────────────────────────────────┐
│  Page Title          [Action] [Action]  │  ← header bar with context-aware actions
├─────────────────────────────────────────┤
│                                         │
│  Data / Content                         │  ← read-only views, tables, status info
│                                         │
└─────────────────────────────────────────┘
```

- **Header bar**: Page title on the left, action buttons on the right. Actions change based on page state (context-aware — only show actions that apply).
- **Data area**: Read-only content below — tables, cards, status displays.
- **Action flows**: Clicking an action button opens a dialog. Dialog handles the form/confirmation. On success, dialog closes and page data refreshes via query invalidation.

Extracted as a reusable `PageHeader` component.

## Walkup Waitlist — States & Actions

| State | Title Area | Actions (right side) |
|-------|-----------|---------------------|
| Inactive (no waitlist today) | "Walk-Up Waitlist" | `Open Waitlist` |
| Active (waitlist open) | "Walk-Up Waitlist" + short code display + copy button | `Add Tee Time Request`, `Close Waitlist` |
| Closed (waitlist closed today) | "Walk-Up Waitlist" + "Closed" badge | (no actions) |

Data area (below header):
- **Active**: Golfer queue table + tee time requests table
- **Inactive**: Empty state message
- **Closed**: Read-only queue history

## Dialog Flows

### Open Waitlist Dialog
- Heading: "Open Walk-Up Waitlist"
- Body: Confirmation text explaining what happens
- Actions: Cancel | Open Waitlist
- On success: dialog closes, page refreshes to Active state

### Add Tee Time Request Dialog
- Heading: "Add Tee Time Request"
- Body: 2-field form — tee time (time input) + golfers needed (number 1-4)
- Actions: Cancel | Add Request
- Validation: existing Zod schema (teeTime required, golfersNeeded 1-4)
- On success: dialog closes, requests table refreshes

### Close Waitlist Dialog
- Heading: "Close Walk-Up Waitlist"
- Body: Warning text that golfers can no longer join
- Actions: Cancel | Close Waitlist (destructive variant)
- On success: dialog closes, page refreshes to Closed state

All dialogs are single-screen — no multi-step wizards.

## Component Structure

```
PageHeader (new, reusable)
├── title slot (left)
├── subtitle/badge slot (left, optional)
└── actions slot (right)

WalkUpWaitlist (refactored page)
├── PageHeader
│   ├── title: "Walk-Up Waitlist"
│   ├── subtitle: short code + copy button (when active)
│   └── actions: context-aware buttons
├── OpenWaitlistDialog (new)
├── AddTeeTimeRequestDialog (extracted from inline form)
├── CloseWaitlistDialog (extracted from inline AlertDialog)
├── GolferQueueTable (existing inline markup, unchanged)
└── TeeTimeRequestsTable (existing inline markup, minus form)
```

## What Changes

- **New**: `PageHeader` component (reusable across operator pages)
- **New**: `OpenWaitlistDialog` component
- **Extracted**: `AddTeeTimeRequestDialog` (form moves from inline to dialog)
- **Extracted**: `CloseWaitlistDialog` (AlertDialog becomes own component)
- **Simplified**: `WalkUpWaitlist.tsx` becomes a thin orchestrator

## What Doesn't Change

- No new hooks — existing `useWalkUpWaitlistToday`, `useOpenWalkUpWaitlist`, `useCloseWalkUpWaitlist`, `useCreateWaitlistRequest` stay as-is
- No API changes
- No new routes
- No changes to data fetching or mutation logic

## Design Principles Applied

- **Zero Training Required**: Actions are visible buttons, not hidden in menus. Context-aware display means operators only see what's relevant.
- **Progressive Disclosure**: Simple action bar → focused dialog flow → back to main view.
- **Reusable Pattern**: `PageHeader` establishes the template for all operator screens going forward.
