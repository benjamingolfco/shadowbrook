---
name: Issue #263 — SMS notification when operator cancels confirmed booking
description: User story for golfer SMS when operator cancels a confirmed booking; blocked by #262, sub-issue of #29
type: project
---

Issue #263 created and fully configured on 2026-03-26.

**Why:** When an operator cancels a tee time opening (#262), golfers with confirmed bookings need an SMS so they know the booking is gone. Pending offers are silently rejected — no SMS.

**Scope decisions:**
- SMS must include course name and tee time date/time (Aaron's explicit requirement)
- Pending offers: silently rejected, no SMS (Aaron's explicit decision)
- No open questions — story was fully scoped upfront

**Configuration:**
- Labels: `golfers love`, `v1`
- Milestone: v1 — MVP
- Project: Status=Ready, Priority=P1, Size=S, Iteration=Sprint 5
- Sub-issue of #29 (Walk-up Waitlist with Course-Set Discounts)
- Blocked by #262 (Cancel a tee time opening)

**How to apply:** If follow-up work touches cancellation notifications, check this scope boundary — the story is intentionally narrow (confirmed bookings only, no message content customization, no multi-golfer edge cases).
