---
name: Task 2 - Channel Sender Interfaces and NoOpEmailSender
description: ISmsSender and IEmailSender interfaces created; NoOpEmailSender implementation for dev/test
type: project
---

**Task 2 completed:** Created three files in `src/backend/Teeforce.Api/Infrastructure/Services/`:

1. **ISmsSender.cs** — Interface with `Task Send(string toPhoneNumber, string message, CancellationToken ct = default)`
2. **IEmailSender.cs** — Interface with `Task Send(string toEmail, string subject, string body, CancellationToken ct = default)`
3. **NoOpEmailSender.cs** — Implementation of IEmailSender that logs a warning instead of sending (used in dev/test)

**Why:** These are channel-specific sender abstractions. ISmsSender will have Telnyx (prod) and DatabaseSmsSender (dev) implementations. IEmailSender gets a no-op for now pending email provider selection. NotificationService (Task 3) will inject these to route notifications by channel.

**Build status:** API project compiles successfully. Test projects have NuGet cache permission issues in sandbox (read-only file system), but those are infrastructure/environment issues, not code issues.

**Files changed:**
- Created ISmsSender.cs
- Created IEmailSender.cs  
- Created NoOpEmailSender.cs

**Commit:** `4082aa2` — feat: add ISmsSender, IEmailSender interfaces and NoOpEmailSender
