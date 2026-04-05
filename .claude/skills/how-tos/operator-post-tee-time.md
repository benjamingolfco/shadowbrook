---
name: how-tos:operator-post-tee-time
description: Use when you need to post a tee time opening on the walk-up waitlist
---

# Operator: Post Tee Time Opening

## Prerequisites
- **Required data:** Waitlist must be open for today
- **Required role/page:** Operator; on the Walk-Up Waitlist page at /operator
- **Depends on:** `how-tos:operator-open-waitlist`

## Steps
1. With the waitlist open, locate the **Post Tee Time** form
2. Set **Time** using the time input (type="time" format: HH:MM e.g. `09:00`)
3. Click the **Slots** button for the number of open slots (1, 2, 3, or 4)
4. Click **Post Tee Time**
5. Verify: The button briefly shows "Posted!" and the opening appears in the **Today's Openings** section below as "Open · N / M slots filled · Waiting for golfers..."

## Notes
- **The tee time must be within 30 minutes of the current time** for waiting golfers to receive an SMS offer. Openings posted for times further out will not trigger the waitlist offer flow. When stress testing, always calculate tee times relative to "now" (e.g., if it's 10:52 PM UTC, post openings for 10:55 PM, 11:05 PM, 11:15 PM — not 08:00 AM or 09:00 AM).
- The time input is `type="time"` — use 24-hour format in automation (`09:00` not `09:00 AM`)
- Posting the same time twice will create two separate openings at that time
- Each opening can be cancelled via the "Cancel" link in the opening row

