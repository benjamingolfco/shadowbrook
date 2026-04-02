---
name: how-tos:admin-create-organization
description: Use when you need to create a new organization (golf course operator group) as admin
---

# Admin: Create Organization

## Prerequisites
- **Required role/page:** Must be logged in as Admin; navigate to /admin/organizations

## Steps
1. Navigate to `http://localhost:3000/admin/organizations`
2. Click **Create Organization** button (top right)
3. Navigate to `/admin/organizations/new`
4. Fill **Organization Name** (the only required field)
5. Click **Create Organization**
6. Verify: Redirected to `/admin/organizations` list with the new org appearing in the table

## Notes
- The create form only has one field: "Organization Name" — no contact info required at creation
- Contact details are managed through the org detail page
- The new org appears immediately in the list with 0 courses and 0 users

