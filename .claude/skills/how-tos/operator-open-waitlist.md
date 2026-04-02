---
name: how-tos:operator-open-waitlist
description: Use when you need to open the walk-up waitlist for a course as an operator
---

# Operator: Open Walk-Up Waitlist

## Prerequisites
- **Required data:** At least one course exists and is assigned to the operator's organization
- **Required role/page:** Must be logged in as Operator; navigate to /operator
- **Feature flag:** Works regardless of `full-operator-app` flag — waitlist is always accessible

## Steps
1. Navigate to `http://localhost:3000/operator`
2. If multiple courses: click **Manage** on the target course from the Course Portfolio page
3. The Walk-Up Waitlist page shows an empty state with an **Open Waitlist for Today** button
4. Click **Open Waitlist for Today**
5. Verify: The page refreshes showing the waitlist header with "Open" badge, a 4-digit short code, and a "Post Tee Time" form

## Notes
- The short code (e.g., "0939") is displayed prominently — golfers use this to join the waitlist via SMS
- Once open, the header shows: `N waiting · Print sign · Add golfer manually · Close waitlist for today`
- There is no confirmation dialog — the waitlist opens immediately

