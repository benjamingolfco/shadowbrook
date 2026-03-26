---
name: Opening Cancel Pattern Decision
description: Design decision for #262 -- Cancel() throws on non-Open (operator action) vs Expire() silently no-ops (system process). Establishes precedent for operator-initiated vs system-initiated state transitions.
type: project
---

For #262, decided that `Cancel()` should throw `OpeningNotAvailableException` when the opening is not Open, unlike `Expire()` which silently no-ops.

**Why:** Cancel is an explicit operator action triggered from the UI. If the opening is already filled/expired/cancelled, the operator should know immediately (stale UI or race condition). System-initiated transitions like expiration can afford to be idempotent because there is no human waiting for feedback.

**How to apply:** When adding new operator-initiated domain methods, prefer throwing on invalid state. When adding system/automated transitions, prefer idempotent silent returns. This distinction should guide future aggregate method designs.
