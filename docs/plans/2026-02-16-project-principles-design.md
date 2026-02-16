# Project Principles

## 1. Zero Training Required

Both golfers and course operators should be productive immediately — no onboarding sessions, no manuals, no "let me show you how this works."

**What this means in practice:**
- Progressive disclosure — show the essential action first, advanced options on demand
- Familiar patterns over clever ones — if a golfer has ever booked a restaurant on their phone, booking a tee time should feel just as obvious
- Operator tools mirror how they already think (tee sheet = visual grid of time slots, not a form)
- Error prevention over error messages — don't let users get into bad states in the first place

## 2. Event-Driven Backend

The backend communicates through domain events, not direct service coupling. This is the foundation for resiliency, async processing, and future scalability.

**What this means in practice:**
- Key actions (booking created, tee time cancelled, waitlist spot opened) publish domain events
- Downstream concerns (SMS notifications, waitlist processing, analytics) subscribe to events rather than being called synchronously
- If a downstream system is slow or down, the core booking flow still completes
- New capabilities can be added by subscribing to existing events without modifying the code that produces them

## 3. SMS is the Communication Channel

Meet golfers where they are — in their text messages. The web app is for browsing and booking; SMS is how the system talks back to golfers.

**What this means in practice:**
- v1: Web for actions (browse, book, manage profile), SMS for system-to-golfer communication (confirmations, waitlist updates, cancellation notices)
- Over time, SMS expands from one-way notifications to two-way conversational booking (v2)
- Every golfer-facing notification should work as a text message first — if it doesn't fit in an SMS, simplify it
- 90%+ open rates vs 20% for email validates this channel choice

## 4. Multi-Tenant from Day One

Every course shares infrastructure but gets its own isolated world. Tenant boundaries are a first-class architectural concern, not something bolted on later.

**What this means in practice:**
- Every API endpoint, query, and data access path is scoped to a course (tenant)
- Per-course configuration for tee time intervals, pricing, cancellation policies, waitlist rules — courses are different and the system respects that
- No data leakage between tenants — a bug in one course's setup never affects another
- Infrastructure scales horizontally across tenants without per-course deployment

## 5. Configuration Without Opinions

Course operators know their course better than we do. The system gives them full control over how they run their operation, with sensible defaults to start from.

**What this means in practice:**
- Ship with reasonable defaults (e.g., 10-minute intervals, 30-minute cancellation window) so operators can get started immediately
- Every operational parameter is configurable — intervals, pricing, group sizes, waitlist rules, no-show policies
- No hard-coded business rules that assume all courses work the same way
- As the platform matures and usage data accumulates, we may introduce gentle suggestions ("courses like yours see fewer pace issues at 8-minute intervals") — but never force them
