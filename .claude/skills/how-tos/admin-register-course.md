---
name: how-tos:admin-register-course
description: Use when you need to register a new course in the platform as admin
---

# Admin: Register Course

## Prerequisites
- **Required data:** At least one organization must exist
- **Required role/page:** Must be logged in as Admin; navigate to /admin/courses

## Steps
1. Navigate to `http://localhost:3000/admin/courses`
2. Click **Register Course** button (top right)
3. Navigate to `/admin/courses/new`
4. Select an **Organization** from the combobox (required, click to open dropdown)
5. Fill **Course Name** (required)
6. Optionally edit **Timezone** (auto-populated from browser locale)
7. Optionally fill Street Address, City, State, Zip Code, Contact Email, Contact Phone
8. Click **Register Course**
9. Verify: Redirected to `/admin/courses` list with the new course appearing

## Notes
- The Organization field uses a shadcn combobox (not a native `<select>`) — in Playwright use `getByRole('combobox')` + click + wait for `[role="option"]` + click option
- Timezone is pre-filled but can be edited as free text
- Course is linked to organization; operator users of that org can then manage it

