---
name: Walk-Up Waitlist Design Direction
description: Final "Post & Track" design direction for the Walk-Up Waitlist operator page — complete spec written 2026-03-29
type: project
---

Three directions were explored in 2026-03-29. A complete "Post & Track" spec was then produced that synthesizes the best elements.

**Spec location:** `docs/superpowers/plans/2026-03-29-waitlist-ux-spec.md`

## Final Design: "Post & Track"

Core decisions:
- **Post Tee Time is the primary action** — inline form at the top of the page in a Card, no dialog. Time + segmented slot selector (1/2/3/4 toggle buttons) + Post button on one horizontal row.
- **Openings list is the main content** — flat list with dividers, sorted chronologically. No tabs.
- **Queue is a count pill in the header** — not a table. Inline expansion on demand via "View queue" link.
- **QR code behind a "Print sign" link** — not a persistent card in main content.
- **Infrequent actions in a quiet Zone 4** — "Add golfer manually · Close waitlist for today" as text links.

Page zones (active state):
1. Header bar: title + Open badge + short code + copy icon + queue pill + View queue link
2. Post Tee Time card: inline form
3. Openings list: flat rows (time, status badge, fill indicator, golfer names, cancel link)
4. Infrequent actions: text links
5. Queue drawer: inline expansion below Zone 1

Key structural changes from the current implementation:
- `max-w-2xl` (672px) → `max-w-[860px]`
- Remove Tabs component
- Remove QrCodePanel from main content
- Remove OpenWaitlistDialog (button fires mutation directly)
- Replace AddTeeTimeOpeningDialog with inline form
- Replace Select for slots with segmented button group
- Move Add Golfer + Close Waitlist out of PageHeader actions
- Auto-focus time input on active page load

**Original directions for reference:**
- Direction 1 — Ops Dashboard: two-column, queue left, openings right
- Direction 2 — Status Board: single column, sticky status banner, 48px short code
- Direction 3 — Task-Forward: dynamic Next Action card that changes by operational state

## Known Problems in Current Design (diagnosed during exploration)

1. Tabs hide the relationship between queue and openings — operator cannot see both simultaneously
2. QR code panel (240px) dominates every active-state page load, not just the first
3. Three equal-weight buttons in header: Add Golfer, Add Tee Time Opening, Close Waitlist — Close Waitlist should be far less prominent
4. "Open Waitlist" triggers a confirmation dialog for a non-destructive, easily-reversed action
5. Short code is buried in header subtitle — hard to read aloud to a golfer at the counter
6. Openings table has five columns (Filled, Pending, Status, Golfers, Actions) — most are archive data during the morning rush
7. Inactive state is nearly empty with no explanation of what the waitlist does

## Cross-Cutting Recommendations (apply regardless of direction)

1. Remove the "Open Waitlist" / "Start Waitlist" confirmation dialog — not destructive, no safety benefit, adds friction at startup
2. Move Close Waitlist out of the header action group to the bottom of the page with muted/ghost styling
3. Enlarge short code to minimum 32px (Direction 3) or 48px (Direction 2), tap-to-copy as single interaction, eliminate separate "Copy Code" button
