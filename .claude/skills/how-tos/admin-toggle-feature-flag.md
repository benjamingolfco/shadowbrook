---
name: how-tos:admin-toggle-feature-flag
description: Use when you need to enable or disable a feature flag for an organization
---

# Admin: Toggle Feature Flag

## Prerequisites
- **Required data:** At least one organization must exist
- **Required role/page:** Must be logged in as Admin; navigate to /admin/feature-flags

## Steps
1. Navigate to `http://localhost:3000/admin/feature-flags`
2. The page shows a table with organizations as rows and flags as columns: `sms-notifications`, `dynamic-pricing`, `full-operator-app`
3. Click the toggle (switch button) for the desired flag/organization intersection
4. Verify: The toggle state changes (checked = enabled, unchecked = disabled) immediately

## Notes
- Feature flags have a 5-minute stale time in the frontend cache — after toggling, the operator may need to hard-refresh to see the change
- `full-operator-app` enables the full operator interface (Tee Sheet, Settings); when off, only Walk-Up Waitlist is shown
- In Playwright: toggles are `button[role="switch"]` with `data-state="checked"` or `data-state="unchecked"`
- Layout order: Row N has switches at indices `(N-1)*3`, `(N-1)*3+1`, `(N-1)*3+2` for sms-notifications, dynamic-pricing, full-operator-app respectively

