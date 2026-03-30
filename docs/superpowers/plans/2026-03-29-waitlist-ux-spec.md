# Walk-Up Waitlist — Operator UX Specification
**"Post & Track" Design Direction**
Date: 2026-03-29

---

## Overview

The Walk-Up Waitlist is the primary (and in Phase 1, only) Shadowbrook feature for course operators. The page has one job: let the operator post tee time openings fast and see at a glance whether they filled. Everything else is secondary.

**The operator's mental model:**

> "I have a gap at 10:40. Let me post it. Done. Someone will text me if there's a problem."

The page should match that mental model exactly: a fast posting action at the top, a list of what was posted below it, and a quiet indicator of the queue count off to the side. The system handles matching via SMS — the operator does not need to watch the page.

---

## 1. Page States

### State A: Inactive (No Waitlist Today)

The waitlist has not been opened for today. This is the first thing the operator sees each morning.

**What the operator sees:**

The page is centered and minimal. There is a single large call-to-action card in the center of the content area (not a header button — a prominent card that fills the middle of the page and communicates that nothing is running yet).

- Page title "Walk-Up Waitlist" in the top-left header area, standard weight
- Below the header: a card with roughly 300–400px height, centered horizontally in the content area
- Inside the card:
  - A golf-flag or clock icon (24px, muted foreground color) centered at the top of the card
  - Large bold text: "Waitlist Not Open Today" (text-xl, font-semibold)
  - Below in muted text (text-sm, text-muted-foreground): "Open the waitlist to let walk-up golfers join the queue. You'll post tee time openings throughout the day."
  - A single large primary button: "Open Waitlist for Today" — full width within the card, height 44px minimum (not small, not outline — this IS the action)
- No tabs, no tables, no queue section visible

**Rationale:** The operator should not wonder what to do. There is exactly one thing to do and one place to click.

---

### State B: Active — Empty Queue, No Openings Posted

The waitlist was just opened. No golfers have joined yet and no openings have been posted.

**What the operator sees:**

The page shifts to the full active layout (described in Section 2). In this brand-new state:

- The status badge shows "Open" in green, top-left below the page title
- The short code is displayed inline next to the status badge
- The inline Post Tee Time form is visible and ready (it is the visual anchor of the page)
- The openings list area shows an empty state message: "No openings posted yet. Post a tee time above when you have a gap to fill." — muted text, no icon needed
- The queue pill shows: "0 waiting" in muted text — small, top-right of the content section, not a table

**Rationale:** The page is ready to work. The operator's eye goes immediately to the Post Tee Time form. They see the queue is empty and know no one is waiting yet.

---

### State C: Active — Golfers in Queue, No Openings

Golfers have joined the waitlist but no openings have been posted today.

**What the operator sees:**

Same as State B, except:

- The queue pill changes to a count with more visual presence: "**14 waiting**" — the number is bold, still in the top-right of the content section. The text color shifts from muted to foreground when the count is above zero.
- The openings list area still shows the empty state message
- The Post Tee Time form remains the visual anchor

**Rationale:** The operator glances at "14 waiting" and knows there's demand. The form is still the primary action. The queue count alone is sufficient — they don't need names.

---

### State D: Active — Openings Posted (Various Fill States)

This is the dominant working state throughout the day.

**What the operator sees:**

- Status badge "Open" (green) with short code to its right
- Queue pill showing current count, positioned top-right of content
- The Post Tee Time inline form, prominent at the top
- Below the form: the Openings list, which is the main content
- Each opening in the list shows its time, slot status, and who claimed it (if filled)
- Infrequent actions accessible but visually quiet (described in Section 2)

Opening rows have three visual treatments based on status:

**Open** (system is actively trying to fill it):
- Row has normal background
- Time is bold (text-base, font-semibold)
- Slot status shown as "2 of 4 filled" in muted text
- Golfer names appear as they claim slots — initially a dash or empty
- A "Cancel" link (text-sm, text-destructive) appears at the end of the row — not a button, a text link, to de-emphasize it

**Filled** (all slots claimed):
- Row has a subtle success tint (e.g., green-50 background or a left border accent in green)
- Time remains bold
- Slot status shows "Filled" badge (green, small)
- Golfer names listed, truncated if needed
- No cancel action

**Expired** (offer window passed, not fully claimed):
- Row has muted/faded treatment (opacity-60 or muted background)
- Time in muted foreground
- Shows "Expired" badge (gray, small)
- Shows however many slots were claimed ("2 of 4 claimed")
- No actions

**Cancelled** (operator cancelled it):
- Row has muted/faded treatment
- "Cancelled" badge (red-outline variant, small)
- All text muted
- No actions

**End-of-day summary (at bottom of list):**
When there are multiple openings, show a single-line summary below the table: "Today: 6 openings — 18 slots offered, 14 filled, 4 expired" — small, muted foreground.

---

### State E: Closed

The waitlist has been closed for the day.

**What the operator sees:**

- Page title "Walk-Up Waitlist" with a "Closed" badge (gray/secondary) inline to its right
- A single quiet action button in the top-right: "Reopen" (outline variant, small) — not prominent
- The openings list is fully visible as a read-only historical record — same visual format as State D but no actions anywhere
- Queue count is shown as a historical fact: "X golfers were on the queue" in muted text
- The Post Tee Time form is hidden (the waitlist is closed; posting is not possible)
- The short code is not displayed (no reason to share it)

**Rationale:** Closed is a wind-down state. The operator is reviewing today's results, not acting. Read-only record is appropriate. Reopen is available but not prominent — it's the exception.

---

## 2. Layout Specification (Active State)

The active state layout runs top-to-bottom in a single column with a maximum content width of 860px, left-aligned within the operator's main content area (sidebar excluded). The full content area is used — the current `max-w-2xl` (672px) constraint is too narrow for this layout.

---

### Zone 1: Page Header Bar

**Full width of content area. Height: approximately 56px.**

Left side:
- Page title "Walk-Up Waitlist" — text-2xl, font-semibold
- Immediately to the right of the title (same line, vertically centered): "Open" badge (success/green variant, small)
- To the right of the badge: short code in monospace — displayed as spaced characters (e.g., "A B 4 7 X") — text-lg, font-mono, font-bold, tracking-widest
- To the right of the short code: a "Copy" icon button (icon only, 16px copy icon, outline variant, small) — clicking it copies the short code to clipboard, and the button text changes to a checkmark icon for 2 seconds

Right side (top-right of header bar):
- Queue pill: "**N** waiting" — the number is font-bold text-foreground, the word "waiting" is text-muted-foreground text-sm. Both are on one line. When count is 0, the entire pill is in text-muted-foreground.
- Below the queue pill (or directly below if the pill wraps): a text link "View queue" in text-xs text-muted-foreground — clicking it expands the queue drawer (described in Section 3)

**Spacing:** 24px below the header bar before Zone 2.

---

### Zone 2: Post Tee Time — The Primary Action

**This zone is the visual anchor of the page. It must draw the eye.**

It is rendered as a Card (shadcn Card component) with a slightly elevated appearance — a subtle shadow or a border with higher contrast than the default card border. The card occupies the full content width.

Inside the card:

**Card header row (inside card, not CardHeader component):**
- Left: "Post Tee Time" — text-base, font-semibold — this is the section label
- Right: nothing (no actions here)

**Inline form — all fields on one row:**

The form renders as a single horizontal row of controls, not a stacked form. Left to right:

1. Time input — label "Time" (text-xs, text-muted-foreground, above the field), `<Input type="time">` — width approximately 140px. The field opens pre-populated with the current time rounded up to the nearest 10-minute interval (e.g., if it is 10:33, default to 10:40).

2. Slots selector — label "Slots" (text-xs, text-muted-foreground, above the field), a segmented button group showing 1, 2, 3, 4 — NOT a Select dropdown. The four options are rendered as four adjacent toggle-style buttons (like a button group / radio group). Default selection: 1. Width: approximately 160px total for the group.

3. Post button — "Post Tee Time" — primary variant, height matching the inputs — appears at the end of the row.

All three controls are vertically centered in the row. The row has 12px gap between items.

**No dialog.** The form submits inline. Pressing "Post Tee Time" (or pressing Enter while any field is focused) immediately fires the mutation. There is no confirmation step.

**After submission:**
- The time field clears (or advances to the next 10-minute interval — developer's choice, either is acceptable)
- The slots resets to 1
- The new opening appears at the top of the openings list below with a brief highlight animation (described in Section 7)
- The Post button shows "Posting..." disabled state for the duration of the in-flight request

**Validation:**
- Time field: required. If the submitted time is more than 5 minutes in the past (using course timezone, matching current backend validation), the time field gets a red border and an inline error "This time has already passed" appears directly below the time field — not a toast, not an alert above the form.
- If submission fails for any other reason, a small error message appears directly below the form row: "Couldn't post opening. Try again." in text-sm text-destructive.

**Card padding:** 20px all sides.

**Spacing:** 24px below this card before Zone 3.

---

### Zone 3: Openings List

**Full content width. This is the main content of the page.**

**Section header row:**
- Left: "Today's Openings" — text-sm, font-medium, text-muted-foreground (subdued — this is a label, not a title)
- Right: a compact summary when there are openings — "6 openings · 14/18 filled" in text-xs text-muted-foreground — updated in real time

**The list itself:**

Each opening is rendered as a single horizontal row inside the list. Rows are separated by a 1px divider line (not full Card borders for each row — a flat list with dividers is cleaner and faster to scan). There is no table header row.

**Row anatomy (left to right):**

- **Time** — text-base, font-semibold, text-foreground — approximately 80px wide, fixed. Example: "10:40 AM"
- **Status badge** — small Badge component — 80px wide, fixed. "Open" (green), "Filled" (green, slightly different shade or use `default` variant), "Expired" (secondary/gray), "Cancelled" (outline/red)
- **Slot fill indicator** — text-sm, text-muted-foreground — flexible width. For Open: "2 / 4 slots filled". For Filled: "4 / 4". For Expired: "2 / 4 claimed". For Cancelled: "—"
- **Golfer names** — text-sm, text-foreground — takes remaining width. If multiple golfers: comma-separated. If a golfer has a group size greater than 1, show as "J. Smith (×3)". If no one has claimed yet (Open with 0 filled): show "Waiting for golfers..." in text-muted-foreground italics. Truncate with ellipsis if the full list is too long.
- **Cancel action** — rightmost, only for Open status rows. Render as text-sm, text-destructive, underline on hover, no button border. Clicking opens the CancelOpeningDialog (keep the existing confirmation dialog — cancellation has real consequences for golfers who have been offered the slot).

**Row visual states:**
- Open: normal background
- Filled: left border 3px solid green (success color token), no background tint
- Expired: opacity-60, no border accent
- Cancelled: opacity-50, no border accent

**Empty state (no openings yet):**
Center-aligned within the Zone 3 area. Text only:
"No openings posted yet." — text-sm, text-muted-foreground
Below it: "When you have a gap to fill, use the form above." — text-xs, text-muted-foreground

**Spacing:** 32px below the openings list before Zone 4.

---

### Zone 4: Infrequent Actions

**Full content width. Visually quiet — this should not compete with Zones 2 or 3.**

A single row of text links (not buttons) separated by middots:

"Add golfer manually · Close waitlist for today"

Both items are text-sm text-muted-foreground. On hover: text-foreground, underline. "Close waitlist for today" is text-destructive on hover.

Clicking "Add golfer manually" opens the existing AddGolferDialog.
Clicking "Close waitlist for today" opens the existing CloseWaitlistDialog.

**No QR code panel in the main content area.** The QR code/print functionality is accessed via the short code section in Zone 1. A "Print sign" link is added to Zone 1 next to the Copy button — clicking it opens a minimal print dialog or directly triggers `window.print()` with the existing print-optimized QR view. The QR panel as a persistent card in the main content area is removed — it takes up too much space and is a setup-day action.

---

### Zone 5: Queue Drawer (Expanded, Opt-In)

**Not visible by default. Slides in from the right or expands inline below Zone 1 on click of "View queue".**

Recommended: **inline expansion** (not a side drawer) — a collapsible section that pushes content down. This avoids the complexity of a sheet component and keeps the operator oriented.

When expanded, a collapse link "Hide queue" replaces "View queue".

The expanded queue shows a simple list (not a full table). Each entry is one row:
- Position number (#1, #2, ...) — font-mono, text-muted-foreground, 24px wide
- Golfer name — text-sm, text-foreground
- Group size — if > 1, shown as "(×2)" in text-muted-foreground text-xs, inline after the name
- Joined time — text-xs, text-muted-foreground, right-aligned
- "Remove" text link — text-xs, text-destructive, rightmost — clicking opens RemoveGolferDialog

The queue drawer has a maximum height of 320px with internal scroll if the list is long. It shows at most the first 20 entries without scrolling.

---

## 3. Component Specifications

### 3.1 Inline Post Tee Time Form

**Content displayed:** Time (local, 24hr input rendered as 12hr AM/PM in the browser native picker per OS conventions), slots 1–4.

**Interactive elements:**
- Time input: native `<input type="time">` — auto-focused when the page first loads in active state, so the operator can literally just type the time and press Enter
- Slot group: 4-option segmented control. Keyboard: arrow keys navigate between options. Mouse: single click selects.
- Post button: primary variant. Disabled during pending mutation.

**States:**
- Default: time pre-filled with next round interval, slots = 1
- Submitting: button shows "Posting..." and is disabled; form fields remain interactive (operator can stage the next posting)
- Validation error: red border on invalid field, inline error text below that field only
- Submit error: error text below the entire form row
- Success: form resets silently (no toast, no celebration — the new row appearing in the list is the confirmation)

**Accessibility:** The label for the time input is "Tee time" (visible text-xs above). The label for the slot group is "Slots". Submit button has accessible text "Post tee time opening". All form fields are in a `<form>` element with `onSubmit` so Enter submits naturally.

---

### 3.2 Openings List Row

**Content displayed:** time, status, fill count, golfer names (as they arrive), cancel action for open rows.

**Interactive elements:** Cancel text link on Open rows only.

**States:**
- Open with 0 filled: golfer name column shows "Waiting..." in italics
- Open with partial fill: names of confirmed golfers shown, remaining shown as remaining slot count
- Filled: all names shown, left border accent
- Expired: row faded
- Cancelled: row faded
- Cancelling (mutation pending): row fades to opacity-40, cancel link disappears, a spinner icon (16px) appears in the actions column

**Real-time updates:** The list re-fetches via TanStack Query's `refetchInterval`. When a new opening is added to the list, the new row animates in from the top of the list (slide down + fade in, 200ms). When an opening transitions from Open to Filled, the left border accent animates in (transition-colors, 300ms).

---

### 3.3 Queue Pill (Header Zone 1)

**Content:** Integer count of current queue entries + "waiting" label.

**States:**
- 0 waiting: full pill in text-muted-foreground. "View queue" link hidden or in text-muted-foreground/50.
- 1+ waiting: count in font-bold text-foreground, "waiting" in text-muted-foreground. "View queue" link visible.

**No animation on count change** — the count updates silently with each re-fetch. Animating a counter in the pro shop environment would be distracting.

---

### 3.4 Short Code Display (Header Zone 1)

**Content:** The 5-character short code, spaced out in monospace.

**Interactive elements:** Copy icon button.

**States:**
- Default: copy icon (Lucide `Copy` icon, 16px)
- Copied: icon changes to `Check` (16px, text-green-600) for 2000ms then reverts. No toast.

**Print action:** A "Print sign" text link (text-xs, text-muted-foreground) appears below the short code. Clicking it opens a minimal sheet or modal containing only the QR code panel (the existing QrCodePanel component), with a "Print" button. This replaces the always-visible QrCodePanel card in the main content.

---

### 3.5 Status Badge

Uses the existing `Badge` component with existing variant mapping:
- Open → `success` variant
- Filled → `default` variant
- Expired → `secondary` variant
- Cancelled → `destructive` variant (outline style preferred to distinguish from action destructive)

No change to the badge component itself.

---

### 3.6 Inline Queue Expansion

**Content:** Ordered list of queue entries.

**Interactive elements:** "Hide queue" collapse link (replaces "View queue"), "Remove" text link per entry.

**States:**
- Collapsed: not rendered in DOM (or `hidden`, whichever the developer prefers — either is fine for accessibility)
- Expanding: slides down, 200ms ease-out
- Expanded with entries: scrollable list
- Expanded, entry being removed: that row fades to opacity-40 while mutation is pending
- Expanded, empty: shows "Queue is empty" in text-sm text-muted-foreground, centered

---

### 3.7 Page-Level Loading State

During initial data load (`todayQuery.isLoading`):

- Zone 1 header: show title "Walk-Up Waitlist", no status badge (render a Skeleton ~60px wide in its place)
- Zone 2 card: show full-width Skeleton with ~80px height (simulating the form row)
- Zone 3: show three Skeleton rows with varying widths (~16px height each, consistent with a list)
- No spinner in the center of the page — skeleton placeholders maintain layout stability

---

### 3.8 Page-Level Error State

When `todayQuery.isError`:

- Full content area shows an error card (not just a text string)
- Card content: "Couldn't load waitlist" (text-base, font-medium), below it the error message in text-sm text-muted-foreground, below that a "Retry" button (outline variant)
- The card is centered horizontally, standard card padding

---

## 4. Interaction Flows

### Flow 1: Starting the Waitlist for the Day

**Precondition:** Page is in State A (Inactive).

1. Operator sees the centered card with "Open Waitlist for Today" button.
2. Operator clicks the button. The button changes to "Opening..." (disabled).
3. The `openMutation` fires. On success, the page transitions to State B (Active, empty). There is no intermediate confirmation dialog — the button press is direct. The current `OpenWaitlistDialog` (which added a confirmation step) is removed from this flow.
4. The page animates: the inactive card fades out (150ms), the full active layout fades in (200ms). The time input in the Post Tee Time form receives auto-focus.
5. If the mutation returns a 409 (already open for today), the error displays inline below the button in the inactive card: "Waitlist is already open — try refreshing the page."
6. If the mutation fails for any other reason, the error displays inline below the button: "Couldn't open waitlist. Try again." The button re-enables.

**Rationale for removing the confirmation dialog:** "Open Waitlist for Today" is not a destructive action. The consequence of clicking it by mistake is trivially reversible (close and reopen). Dialogs for non-destructive actions add friction every single morning.

---

### Flow 2: Posting a Tee Time Opening (Primary Flow)

**Precondition:** Waitlist is Active. Operator has a gap to fill (e.g., 10:40 AM, 2 spots).

**Target: under 5 seconds from intention to posted.**

1. Operator looks at the Post Tee Time form at the top of the page. The time field shows the nearest upcoming 10-minute interval (e.g., "10:40 AM" if it is 10:36 AM).
2. If the default time is correct: operator clicks on the slot group (or tabs to it) and presses "2" — no, operator uses the slot group toggle buttons to select 2. One click.
3. Operator presses the "Post Tee Time" button. Or presses Enter if the button has focus.
4. Button shows "Posting..." The new opening appears in the list in under 1 second (optimistic or fast API).
5. The form resets — time advances to next interval, slots back to 1.

**If the default time is wrong:**
1. Operator clicks the time field (or it is already focused from page load).
2. Operator types the time — e.g., "10:40" using keyboard. Native time picker on tablet accepts direct keyboard input.
3. Operator clicks/tabs to slot selector, picks 2.
4. Operator presses Post or Enter.

**Total clicks for the happy path (correct default time, 1 slot):** 1 click (Post button). That is the floor.

**Total clicks for the standard case (need to set time, 2 slots):** 3 interactions (time field → adjust time, slot button → click 2, Post → click). Under 10 seconds.

---

### Flow 3: Viewing Opening Results

**No special flow — this is ambient information on the page.**

Openings are listed in chronological order in Zone 3. The operator glances at the list between customer interactions. They can see in 2 seconds:

- Which openings are still Open (normal row, "Waiting for golfers..." or partial names)
- Which filled (left green border accent, golfer names present)
- Which expired (faded row)

If they want to know exactly who is coming for a specific opening, they read the golfer names column on that row. No click required.

---

### Flow 4: Cancelling an Opening

**Precondition:** Opening is in Open status.

1. Operator finds the opening row in the list.
2. Operator clicks the "Cancel" text link at the end of the row.
3. The CancelOpeningDialog opens. This is an AlertDialog (keep existing). The dialog explains: "Cancel this tee time opening? Any pending offers will be withdrawn. This cannot be undone."
4. Operator clicks "Cancel Opening" (destructive button). Or dismisses with "Keep Opening" or Escape.
5. On confirm: dialog closes, the row fades to opacity-40 immediately (optimistic), status badge updates to "Cancelled". If the mutation fails, the row un-fades and an inline error appears below the row: "Couldn't cancel opening. Try again."

**Why keep the confirmation dialog here:** Cancellation sends withdrawal SMS messages to golfers who received offers. This is externally visible. Requiring confirmation is correct.

---

### Flow 5: Adding a Golfer Manually (Infrequent)

**Precondition:** Waitlist is Active. A golfer is standing at the counter and doesn't have a phone, or needs help joining.

1. Operator clicks "Add golfer manually" in Zone 4 (infrequent actions area).
2. The existing `AddGolferDialog` opens (Dialog with form).
3. Operator fills in first name, last name, phone, group size (four fields).
4. Operator clicks "Add Golfer".
5. On success: dialog closes, queue count in the pill increments by 1. No other visible change — the golfer is in the system.
6. On 409 (duplicate): dialog stays open, shows "This golfer is already on the waitlist." inline below the form.
7. On other error: inline error below form.

**No change to the existing AddGolferDialog component** — it is already correct for this infrequent flow.

---

### Flow 6: Removing a Golfer (Infrequent)

**Precondition:** Waitlist is Active. Operator needs to remove someone (no-show, changed mind).

1. Operator clicks "View queue" link in Zone 1 to expand the queue.
2. Queue drawer expands, showing the list of golfers.
3. Operator finds the golfer and clicks the "Remove" text link on their row.
4. The existing `RemoveGolferDialog` opens. "Remove [Name] from the waitlist? This cannot be undone."
5. Operator clicks "Remove" (destructive).
6. On success: dialog closes, the row fades out of the queue list, count decrements.
7. On error: dialog stays open, error shown inline in dialog.

---

### Flow 7: Sharing or Copying the Short Code

1. Operator looks at the short code in Zone 1 (always visible when waitlist is active).
2. Operator clicks the Copy icon button next to the short code.
3. Icon changes to a checkmark for 2 seconds. Short code is now in clipboard.
4. Operator pastes into wherever they need it (group text, email, etc.).

**Print sign flow:**
1. Operator clicks "Print sign" text link below the short code.
2. A Sheet or Dialog opens containing the QrCodePanel component (QR code + URL + date + Download/Print buttons).
3. Operator clicks "Print" to trigger the existing `window.print()` behavior.
4. Sheet/Dialog closes.

---

### Flow 8: Closing the Waitlist

**Precondition:** Waitlist is Active. End of day, or special close needed.

1. Operator clicks "Close waitlist for today" in Zone 4.
2. The existing `CloseWaitlistDialog` opens: "No new golfers will be able to join. Existing entries will be preserved."
3. Default focus is on "Keep Open" (safe default, matching existing dialog).
4. Operator clicks "Close Waitlist" (destructive).
5. On success: page transitions to State E (Closed). The Post Tee Time form disappears (no animation needed — it's closing time, not an action the operator needs to see animate). The status badge changes to "Closed" (gray).
6. On error: dialog closes, an error appears in Zone 4: "Couldn't close waitlist. [Retry link]"

---

### Flow 9: Reopening the Waitlist

**Precondition:** Waitlist is Closed (State E).

1. Operator sees the "Reopen" button (outline, small) in the top-right of the header.
2. Operator clicks it. The existing `ReopenWaitlistDialog` opens.
3. Operator confirms. Page transitions back to State D (Active with existing openings visible).
4. The Post Tee Time form reappears. The queue count is restored.

---

## 5. Mobile and Responsive Notes

Primary target: 1024px+ (tablet landscape or desktop). The following describes degradation only — not a separate mobile design.

**768px–1023px (tablet portrait):**
- The inline Post Tee Time form wraps: time field and slot group on one row, Post button on the next row at full width. Adequate.
- Zone 1 header: queue pill moves below the title/badge/short code row rather than floating right.
- Openings list: golfer names column truncates more aggressively (2-line max, ellipsis). All other columns remain visible.
- Zone 4 infrequent actions remain as a text row — no change.

**Below 768px (phone — edge case, not primary):**
- Zone 2 form becomes a vertical stack (time, slots, button each full-width). This matches the current dialog approach in feel.
- Openings list: golfer names column is removed from the row view. Tapping a row expands an inline detail area showing golfer names. This is acceptable degradation — phones are not the operator use case.
- Queue count pill remains in the header. "View queue" link works the same way.
- The overall layout still works — it is not broken, just less optimized.

**The current `max-w-2xl` (672px) constraint on the page wrapper is removed.** Content extends to `max-w-4xl` (896px) or `max-w-[860px]` to accommodate the horizontal form and multi-column list.

---

## 6. Error Handling

### Principle: Errors appear where the action happened.

No toasts for mutation errors. Toasts are for asynchronous background events (future — not relevant here). All operator-initiated mutation errors appear inline, near the action that caused them.

---

| Action | Error Location | Error Content |
|--------|---------------|---------------|
| Open waitlist (inactive page) | Inline below the "Open Waitlist for Today" button | "Couldn't open waitlist. Try again." (409: "Waitlist is already open — try refreshing.") |
| Post tee time (time in past) | Below the time field inside the form card | "This time has already passed." |
| Post tee time (other error) | Below the entire form row | "Couldn't post opening. Try again." |
| Cancel opening | Opening row fades back in. Inline below the row: "Couldn't cancel. Try again." (text-xs, text-destructive) | |
| Add golfer (dialog) | Inline inside the dialog, above the footer | Existing behavior, no change |
| Remove golfer (dialog) | Inline inside the dialog | Existing AlertDialog behavior, no change |
| Close waitlist | Inline in Zone 4, replacing the "Close waitlist" link | "Couldn't close waitlist. [Retry]" |
| Reopen waitlist | Inline below the "Reopen" button in the closed page header | "Couldn't reopen waitlist. Try again." |
| Page load error | Full error card replacing all content | "Couldn't load waitlist" + message + Retry button |

**Error persistence:** Inline form errors clear when the operator modifies the relevant field. API error messages (non-validation) remain until the operator retries or navigates away. There is no dismiss button for form errors — fixing the field is the dismiss action. Mutation error messages (below form, below row) disappear automatically when the operator next submits successfully.

**Closed active waitlist already open (409 on Open Waitlist):** This edge case likely means another browser session already opened it. The error message directs the operator to refresh.

---

## 7. Micro-interactions and Polish

These details distinguish a premium hospitality tool from a utilitarian admin panel. None of these are decorative — each serves a communication purpose.

---

### New Opening Appears in List

When a new opening is posted and the mutation succeeds, the new row slides down from the top of the list (the list is sorted chronologically, so the new row inserts at its correct time position — but it animates from opacity-0 + translate-y(-8px) to opacity-1 + translate-y(0)). Duration: 200ms, ease-out. The row briefly has a green-50 background that fades to transparent over 800ms — a "here's what just happened" indicator.

### Opening Changes to Filled

When TanStack Query's refetch returns a row that has changed from Open to Filled, the left border accent transitions from transparent to green over 300ms. The status badge cross-fades from "Open" (green) to "Filled" (default). No layout shift.

### Copy Short Code

The copy icon (`Copy`) cross-fades to a check icon (`Check`) with a 150ms opacity transition. No scale or bounce — this is a subtle confirmation, not a celebration.

### Queue Count Update

Count updates silently — no animation. The operator is not monitoring a scoreboard. The number should just be accurate when they glance at it.

### Post Button Loading State

The "Post Tee Time" button label changes to "Posting..." and the button is disabled. The form fields remain fully interactive so the operator can begin typing the next tee time immediately. When the response returns (typically < 500ms), the form resets and the button re-enables.

### Page Transition: Inactive → Active

When the waitlist is opened for the first time today, the page transitions from the single centered card to the full active layout. The centered card fades out (opacity-0, 150ms). The active layout fades in (opacity-0 → opacity-1, 200ms, with a very subtle translate-y(4px) → translate-y(0)). The time input receives focus automatically.

### Row Hover State

Opening list rows have a subtle hover background (`bg-muted/40`) on mouseover. The Cancel text link underlines on hover. These transitions use `transition-colors duration-100`.

### Opening Row Being Cancelled (Pending)

The row transitions to `opacity-40` immediately on confirm click, before the mutation resolves. The Cancel link is replaced by a 16px spinner (Lucide `Loader2` with `animate-spin`). This provides immediate feedback without waiting for the server.

### Skeleton Loading

The skeleton loading state (described in 3.7) should use the shadcn `Skeleton` component with `animate-pulse`. The skeleton shapes should match the approximate geometry of the actual content they represent — the form card skeleton is a wide rectangle, the list skeletons are three full-width thin rectangles.

### Focus Management

After posting a tee time (successful submit), focus returns to the time input field. This allows an operator who is rapidly posting multiple openings to never need to reach for the mouse — they can type a time, Tab to slots (if needed), Tab to Post, Enter, and repeat.

After the queue drawer expands, focus moves to the first golfer entry's row (or the "Hide queue" link). After it collapses, focus returns to the "View queue" link.

---

## Appendix: Current Implementation Gaps (for Developer Reference)

These are differences between the current implementation and this spec that require changes:

1. **Max-width:** Current `max-w-2xl` (672px) is too narrow. Increase to `max-w-[860px]` or `max-w-4xl`.

2. **Tabs removed:** The current Tabs (Golfer Queue / Tee Time Openings) are eliminated. Queue is a collapsible pill in the header. Openings are the main content.

3. **QrCodePanel card removed from main content.** QR access moves to a "Print sign" trigger in Zone 1. The QrCodePanel component itself is unchanged — just its placement changes.

4. **Post Tee Time is inline, not a dialog.** The `AddTeeTimeOpeningDialog` component can be repurposed as a fallback for mobile or retired. The inline form replaces it.

5. **Open Waitlist has no confirmation dialog.** The `OpenWaitlistDialog` (AlertDialog) is removed. The button fires the mutation directly.

6. **Slot group uses a segmented button group, not a Select.** 1/2/3/4 are rendered as four adjacent toggle buttons — no dropdown.

7. **Time input defaults to next 10-minute interval.** Current default is empty string. New default is computed from `getCourseNow(timeZoneId)` rounded up to the next 10-minute mark.

8. **Infrequent actions are text links, not header buttons.** Add Golfer and Close Waitlist are moved out of the PageHeader `actions` array.

9. **Active state auto-focuses the time input on mount.** Use `autoFocus` on the time input or `useEffect` + `ref.focus()`.

10. **Cancel opening error appears inline on the row**, not as a page-level paragraph before the tabs.
