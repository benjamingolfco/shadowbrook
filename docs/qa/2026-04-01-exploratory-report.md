## Exploratory QA Report

**Environment:** http://localhost:3000 (API: http://localhost:5221)
**Date:** 2026-04-01
**Overall:** FAIL — blocking issues found

---

### Setup Notes

The local environment required manual setup before testing could begin:

1. The `LocalQaFixes` migration (20260402000710) was broken — its `Up()` method tries to `DROP COLUMN [DisplayName]` which doesn't exist when starting from a fresh database (the squashed `InitialCreate` migration already reflects the final schema without that column). This caused the API to crash at startup on a fresh DB. Fixed by making `LocalQaFixes` a no-op migration.

2. The Docker container used by `make dev` does not have `Auth__UseDevAuth=true`, so the dev auth bearer token (`dev-admin-oid`) returns 500. The API must be run natively with `Auth__UseDevAuth=true` for local QA.

3. Fresh databases have no seeded users. Dev users must be inserted directly via SQL before the frontend can authenticate. See `.claude/skills/how-tos/start-local-dev.md`.

---

### Pages Visited

- `http://localhost:3000/` — OK (redirects to /admin)
- `http://localhost:3000/admin` — OK (Analytics Dashboard)
- `http://localhost:3000/admin/organizations` — OK
- `http://localhost:3000/admin/organizations/new` — OK
- `http://localhost:3000/admin/organizations/:id` — OK
- `http://localhost:3000/admin/courses` — ISSUES (Organization column blank)
- `http://localhost:3000/admin/courses/new` — OK
- `http://localhost:3000/admin/courses/:id` — ISSUES (Organization shows "—")
- `http://localhost:3000/admin/users` — ISSUES (Display Name column blank)
- `http://localhost:3000/admin/users/new` — ISSUES (form/backend mismatch)
- `http://localhost:3000/admin/users/:id` — OK (detail works when navigated directly)
- `http://localhost:3000/admin/feature-flags` — OK
- `http://localhost:3000/admin/dev/sms` — OK (SMS Log)
- `http://localhost:3000/operator` — OK (WalkUpWaitlist or CoursePortfolio based on flag)
- `http://localhost:3000/operator/tee-sheet` — OK (when course selected and feature on)
- `http://localhost:3000/operator/waitlist` — OK
- `http://localhost:3000/operator/settings` — OK

---

### Blocking Issues

1. **LocalQaFixes migration breaks fresh DB startup**
   - **Page:** API startup (not a UI page)
   - **Flow:** Starting the API against a fresh database
   - **Expected:** API migrates schema and starts successfully
   - **Actual:** `ALTER TABLE DROP COLUMN [DisplayName]` fails because `DisplayName` was never added by the squashed `InitialCreate` migration
   - **Console errors:** `Unhandled exception. Microsoft.Data.SqlClient.SqlException: ALTER TABLE DROP COLUMN failed because column 'DisplayName' does not exist in table 'AppUsers'`
   - **Screenshot:** N/A (startup crash)
   - **Fix applied:** Made `LocalQaFixes` a no-op migration (the squashed `InitialCreate` already reflects the target schema)

2. **Create User form has fields that don't exist in the backend API**
   - **Page:** `http://localhost:3000/admin/users/new`
   - **Flow:** Admin tries to create a new user
   - **Expected:** Form submits successfully, user created with all provided data
   - **Actual:** Form requires "Identity ID" and "Display Name" fields. The backend `POST /auth/users` only accepts `email`, `role`, `organizationId` — it has no `identityId` or `displayName` parameters. The form submits (201 response) but creates users with `firstName=null`, `lastName=null`, and the identity ID is silently ignored. Users are created without names and are Inactive.
   - **Console errors:** None
   - **Screenshot:** `/docs/qa/screenshots/2026-04-01/050-admin-user-created-result.png`

3. **E2ESeedData.EnsureAsync fails with FK constraint (Tenants vs Organizations)**
   - **Page:** API startup
   - **Flow:** E2E seed data initialization on startup
   - **Expected:** Seed data creates test courses
   - **Actual:** `The INSERT statement conflicted with the FOREIGN KEY constraint "FK_Courses_Organizations_OrganizationId"` — `E2ESeedData` creates a `Tenant` (stored in `Tenants` table) but `Courses.OrganizationId` references the `Organizations` table, not `Tenants`. These are separate tables.
   - **Console errors:** EF Core FK violation
   - **Screenshot:** N/A (API log error)

---

### Minor Issues

1. **"Display Name" column blank for all users in Users list**
   - **Page:** `http://localhost:3000/admin/users`
   - **What:** The "Display Name" column is always empty. The frontend `UserListItem` interface expects `displayName` but the backend `UserListResponse` returns `firstName` and `lastName`. The mapping is missing.
   - **Screenshot:** `/docs/qa/screenshots/2026-04-01/006-admin-users-list.png`

2. **Organization column shows "—" for courses in Courses list**
   - **Page:** `http://localhost:3000/admin/courses`
   - **What:** The Organization column shows "—" for all courses. The course API response doesn't include the organization name directly. The frontend appears to not be mapping or fetching the organization name.
   - **Screenshot:** `/docs/qa/screenshots/2026-04-01/072-admin-course-registered-result.png`

3. **Course Detail shows "—" for Organization field**
   - **Page:** `http://localhost:3000/admin/courses/:id`
   - **What:** The "Organization" field in the Course Information card shows "—" even though the course belongs to Benjamin Golf Co. The API response for individual courses returns `tenant.organizationName` not `organization.name`.
   - **Screenshot:** `/docs/qa/screenshots/2026-04-01/083-admin-course-detail-direct.png`

4. **Permissions duplicated in `auth/me` response**
   - **Page:** N/A (API)
   - **What:** `GET /auth/me` returns permissions array with each permission listed twice: `["app:access","users:manage","app:access","users:manage"]`. Both `DevAuthHandler` and `AppUserEnrichmentMiddleware` add permission claims independently.
   - **Screenshot:** N/A (curl output)

5. **SMS Log nav link goes to Dashboard at small viewport**
   - **Page:** `http://localhost:3000/admin`
   - **What:** In one test run, clicking "SMS Log" in the nav navigated to the Dashboard. The SMS Log is at `/admin/dev/sms` and only shows in dev mode (`VITE_SHOW_DEV_TOOLS=true`). The nav link appears correct at 1280px viewport but the routing redirect (`path="*"` → `/admin`) may be interfering.
   - **Screenshot:** `/docs/qa/screenshots/2026-04-01/033-admin-sms-log.png`

6. **User Detail "Display Name" card title is blank**
   - **Page:** `http://localhost:3000/admin/users/:id`
   - **What:** The User Detail card title uses `user.displayName` which is always `undefined` since the backend returns `firstName`/`lastName`. The card header shows nothing where the user's name should be.
   - **Screenshot:** `/docs/qa/screenshots/2026-04-01/081-admin-user-detail-direct.png`

7. **Org Detail Users table "Name" column blank**
   - **Page:** `http://localhost:3000/admin/organizations/:id`
   - **What:** The "Name" column in the Users sub-table on the Org Detail page is blank for all users (same `displayName` issue).
   - **Screenshot:** `/docs/qa/screenshots/2026-04-01/082-admin-org-detail-direct.png`

---

### Happy Paths Verified

- Root redirects to /admin (for Admin role) — PASS
- Admin dashboard loads with stats — PASS
- Admin: Organizations list loads — PASS
- Admin: Create Organization — PASS (`/docs/qa/screenshots/2026-04-01/023-admin-org-created-result.png`)
- Admin: Organization Detail loads — PASS
- Admin: Courses list loads — PASS
- Admin: Register Course (with combobox org selection) — PASS (`/docs/qa/screenshots/2026-04-01/072-admin-course-registered-result.png`)
- Admin: Course Detail loads — PASS
- Admin: Users list loads — PASS
- Admin: Create User (form submits, API accepts, redirects to list) — PASS with caveats (names are blank, user is Inactive)
- Admin: User Detail loads — PASS (when navigating directly to `/admin/users/:id`)
- Admin: Feature Flags page loads — PASS
- Admin: Toggle feature flag — PASS
- Admin: SMS Log (Dev SMS Viewer) loads and shows messages — PASS
- Operator: Walk-Up Waitlist page loads — PASS
- Operator: Open Waitlist for Today — PASS (`/docs/qa/screenshots/2026-04-01/031-operator-waitlist-opened.png`)
- Operator: Post Tee Time (9:00 AM, 1 slot) — PASS (`/docs/qa/screenshots/2026-04-01/058-operator-tee-time-posted.png`)
- Operator: Add Golfer Manually — PASS, SMS sent (`/docs/qa/screenshots/2026-04-01/067-operator-golfer-added.png`)
- Operator: Close Waitlist confirmation dialog — PASS (`/docs/qa/screenshots/2026-04-01/069-operator-waitlist-closed.png`)
- Operator: Course Portfolio (multi-course) loads when `full-operator-app` enabled — PASS (`/docs/qa/screenshots/2026-04-01/084-operator-full-app.png`)
- SMS confirmation to golfer on join — PASS (confirmed via SMS Log)

---

### How-Tos Created/Updated

- `.claude/skills/how-tos/start-local-dev.md` — created: full local env setup including dev user seeding
- `.claude/skills/how-tos/operator-open-waitlist.md` — created
- `.claude/skills/how-tos/operator-post-tee-time.md` — created (time input format: 24-hour HH:MM)
- `.claude/skills/how-tos/operator-add-golfer-manually.md` — created (Group Size is combobox)
- `.claude/skills/how-tos/admin-create-organization.md` — created
- `.claude/skills/how-tos/admin-register-course.md` — created (Organization uses shadcn combobox)
- `.claude/skills/how-tos/admin-toggle-feature-flag.md` — created

