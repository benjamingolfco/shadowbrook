---
name: public_route_pattern
description: Pattern for unauthenticated public routes in the SPA and backend -- used for walk-up flows (/join, /book/walkup, /w)
type: project
---

Public (unauthenticated) routes exist at /join/*, /book/walkup/*, and /w/* in router.tsx. These are lazy-loaded features placed outside AuthGuard/RoleGuard wrappers. Backend public endpoints (e.g., /walkup/verify, /walkup/join, /walkup/status/{shortCode}) use IgnoreQueryFilters() to bypass tenant isolation since no X-Tenant-Id header is sent.

**Why:** Walk-up golfers scanning QR codes or entering codes are not authenticated users. They need public access to join waitlists.

**How to apply:** When adding new public-facing features, follow this pattern -- no auth wrapping in router, IgnoreQueryFilters in backend queries, no tenant header dependency.
