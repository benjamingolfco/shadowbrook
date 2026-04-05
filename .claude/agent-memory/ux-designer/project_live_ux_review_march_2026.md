---
name: Live UX Review — March 2026
description: Candid UX review of the live preview (purple-field-0a3932a0f preview URL), covering all operator and admin pages as of late March 2026
type: project
---

Conducted a full live-preview UX review via Playwright screenshots on 2026-03-29.

**Why:** First comprehensive review of the redesigned frontend — Post & Track waitlist layout had just shipped. Needed to assess the full operator flow, visual polish, and hierarchy of the waitlist page in particular.

**How to apply:** Use this as the baseline for future design iterations. Key problems documented here inform the next round of improvements.

## Pages Reviewed
- Admin: Tenants, Courses, SMS Log (Dev)
- Operator: Org Select, Course Select/Portfolio, Tee Sheet, Walk-Up Waitlist (open state), Settings (Tee Time Settings)
- Mobile: Settings, Waitlist (390px viewport)

## Critical Findings
1. Short code in waitlist header rendered as spaced digits ("0 7 8 5") — visually legible but layout tension between code, badge, and copy icon
2. "1 waiting / View queue" counter lives top-right of the waitlist header, opposite the title — queue count is the most operationally important number and should be more prominent
3. "Print sign" appears as an underline-on-hover ghost link immediately below the title — easily missed
4. Course Portfolio shows 56 E2E test courses for the E2E Test Golf Group — real test data pollution in the shared preview environment (not a design issue per se, but affects design review clarity)
5. Tee Sheet shows only a date picker and an error state ("Tee time settings not configured") — page is nearly empty; no empty state guidance
6. Settings page shows only 3 fields — looks sparse but is complete; asterisks (*) on every label but no legend explaining they're required
7. Org Select page: centered in the viewport with massive whitespace above and below, table with a single "Organization Name" column header, no context about what to do
8. "Register Course" appears in the sidebar nav — feels like a settings action, not a primary nav destination
9. Admin Courses page: all E2E test courses listed with dashes (—) in Tenant, Location, and Contact columns — data quality issue in preview makes this look broken
10. Mobile waitlist: sidebar collapses correctly, content is readable, but the "Print sign" ghost link disappears off-screen

## What Works Well
- Warm stone background (#F7F5F0 equivalent) is immediately distinctive, not generic SaaS
- Forest green primary button on white card is punchy and clear
- Fraunces serif headings land — "Walk-Up Waitlist" reads with authority
- Post Tee Time form: time input + segmented slot picker + submit button in one horizontal row is fast and purposeful
- Slot selector (1/2/3/4 segmented button group) is a smart pattern — golf operators always think in groups
- Sidebar hierarchy: org name at top, course switcher below, nav links below that — correct mental model
- "Change Organization" ghost button is in the right place (below course switcher, above nav)
- Auto-focus on the time input after posting is a pro-shop detail that works
- QueueDrawer collapse/expand pattern is right — queue is secondary info
- "Add golfer manually · Close waitlist for today" footer row correctly demotes these destructive/infrequent actions
- Error states have Retry buttons (good)

## Problems Worth Fixing
- Queue count ("1 waiting") is undersized and top-right — should be near the title or in a badge on the page heading
- "Print sign" link is visually orphaned below the title; it belongs somewhere more findable (maybe near the short code)
- Tee Sheet empty state gives no guidance — "Tee time settings not configured" is a raw error message, not a helpful next step
- Settings page asterisks have no legend — remove them or add one
- Org Select page has too much whitespace and no instructional copy explaining this is a one-time setup step
- "Register Course" in the nav is misplaced — it's an admin/setup action, not a daily-use nav item

## Fix Verification — 2026-03-29

Five targeted fixes were deployed and verified via Playwright on the same preview URL.

**Fix 1 (queue count prominence): NEEDS_WORK.** Count "0 waiting" is now on a secondary row below the title — correct placement. But the text is small/muted (same size as "Print sign" beside it). The spec asked for text-lg and more prominent. What shipped reads at roughly the same weight as the subtitle, not as a key operational number. It needs larger text and ideally bold or a stronger color to read at a glance.

**Fix 2 (Print sign inline): PASS.** "Print sign" is now on the same secondary row as the queue count, separated by a · dot. No longer a separate orphaned line. The row reads "0 waiting · Print sign" which is cohesive. Discoverable without being intrusive.

**Fix 3 ("Posted!" feedback): PASS.** Verified by interaction. Button transitions: "Post Tee Time" → "Posting..." (during API call, ~900ms) → "Posted!" (~450ms) → resets to "Post Tee Time". The "Posting..." intermediate state is a bonus — the operator sees the API in progress. "Posted!" is clearly legible at full button width. Form resets correctly. New tee time appears in Today's Openings. Solid execution.

**Fix 4 (Tee Sheet CTA): PASS.** Bordered card replaces the raw error. Reads: "Configure your tee times to get started / Set your tee time interval, first tee time, and last tee time in Settings. / [Go to Settings]". Card is compact (left-aligned, not centered), bordered, and routes correctly to /operator/settings. Helpful, not alarming.

**Fix 5 (Settings no asterisks): PASS.** Labels read "Tee Time Interval", "First Tee Time", "Last Tee Time" — no asterisks. Clean.

### Remaining Issue After This Round
The queue count on the waitlist header still needs work — it is not visually prominent enough to qualify as "bigger and near the title" as intended. The structural placement is correct but the visual weight is wrong. Recommend: `text-lg font-semibold` (or at minimum `font-medium`) for the count number itself, and a darker/primary color rather than muted-foreground.
