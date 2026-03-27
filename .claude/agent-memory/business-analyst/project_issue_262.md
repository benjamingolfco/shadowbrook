---
name: Issue #262 — Cancel a tee time opening
description: User story for operator cancelling a walk-up waitlist opening; two open questions flagged for Aaron
type: project
---

Issue #262 created 2026-03-26. Operator can cancel an Open tee time opening via a confirmation dialog. Pending offers for that opening are rejected as a side effect.

**Why:** Operators currently have no way to remove an opening created in error or that is no longer needed. The TeeTimeOpening aggregate needed a new Cancelled status.

**How to apply:** Two open questions were flagged and need Aaron's answer before implementation begins:
1. Should cancelled openings remain visible in the list, or hidden by default? Filter option needed?
2. What SMS message (if any) goes to golfers whose pending offers are withdrawn due to operator cancellation?

Status: Ready | Priority: P1 | Size: M | Sprint 5 | Sub-issue of #3
