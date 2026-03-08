# Walk-Up Join UI QA Test Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Manually test the walk-up join UI flow end-to-end through a real browser

**Architecture:** Start API + web dev servers, use Playwright MCP to navigate the UI, create test data via API calls, and verify each phase of the 3-step join flow (code entry → join form → confirmation)

**Tech Stack:** Playwright MCP browser, .NET API (localhost:5221), Vite dev server (localhost:3000), SQL Server (Docker)

---

### Task 1: Start infrastructure and seed test data

Start Docker DB, API, and web dev server. Create a tenant, course, and open waitlist via API calls.

**Steps:**
1. Start SQL Server: `docker compose up db -d`
2. Start API: `dotnet run --project src/backend/Shadowbrook.Api` (background)
3. Start web: `pnpm --dir src/web dev` (background)
4. Wait for API health check: `curl http://localhost:5221/health`
5. Create tenant via API:
   ```
   POST http://localhost:5221/tenants
   { "organizationName": "QA Test Golf", "contactName": "QA Tester", "contactEmail": "qa@test.com", "contactPhone": "555-000-0000" }
   ```
6. Create course via API (with X-Tenant-Id header):
   ```
   POST http://localhost:5221/courses
   { "name": "QA Test Course" }
   ```
7. Open walk-up waitlist:
   ```
   POST http://localhost:5221/courses/{courseId}/walkup-waitlist/open
   ```
8. Record the short code from the response

---

### Task 2: Test Phase 1 — Code Entry (happy path)

Navigate to `/join` and enter the valid short code.

**Verify:**
- Page shows "Shadowbrook" header
- Code input is visible with placeholder/label
- Entering 4 digits auto-submits (no button click needed)
- After valid code, transitions to the join form
- Course name is displayed on the join form

---

### Task 3: Test Phase 1 — Code Entry (invalid code)

Enter an invalid 4-digit code.

**Verify:**
- Error message appears: "Code not found. Check the code posted at the course and try again."
- User can clear and re-enter a code
- No crash or blank screen

---

### Task 4: Test Phase 2 — Join Form (happy path)

Fill in first name, last name, and phone number, then submit.

**Verify:**
- Form has fields for first name, last name, phone
- Submit button is present
- After submission, transitions to confirmation screen
- Confirmation shows golfer name, position (#1), and course name

---

### Task 5: Test Phase 2 — Join Form (validation errors)

Submit the form with empty fields.

**Verify:**
- Validation messages appear for required fields
- Form does not submit
- No network request is made

---

### Task 6: Test Phase 3 — Confirmation screen

After a successful join, verify the confirmation screen.

**Verify:**
- Green checkmark icon visible
- Shows "You're on the list, {FirstName}!"
- Shows "#1 in line at QA Test Course"
- Shows "Keep your phone handy" message

---

### Task 7: Test duplicate join (409 handling)

Re-enter the same code and same phone number on the same waitlist.

**Verify:**
- Does NOT show an error — treats 409 as success
- Shows confirmation screen with correct position
- Position is still #1 (same golfer)

---

### Task 8: Test numeric-only input filtering

Try entering letters and special characters in the code field.

**Verify:**
- Only digits are accepted
- Letters and special chars are stripped
- Input max length is 4

---
