---
name: how-tos:operator-add-golfer-manually
description: Use when you need to manually add a walk-up golfer to the waitlist on their behalf
---

# Operator: Add Golfer Manually to Waitlist

## Prerequisites
- **Required data:** Waitlist must be open for today
- **Required role/page:** Operator; on the Walk-Up Waitlist page at /operator
- **Depends on:** `how-tos:operator-open-waitlist`

## Steps
1. With the waitlist open, click **Add golfer manually** in the sub-header
2. A modal dialog "Add Golfer to Waitlist" appears with fields: First Name, Last Name, Phone Number, Group Size
3. Fill **First Name** and **Last Name**
4. Fill **Phone Number** (10 digits, e.g. `5551234567`)
5. Optionally change **Group Size** using the combobox (default is 1; options 1-4)
6. Click **Add Golfer**
7. Verify: The dialog closes, the "N waiting" counter in the sub-header increments by 1, and an SMS confirmation is sent (visible in Admin > SMS Log)

## Notes
- Group Size is a combobox (shadcn Select), not a text input — use click + select option in Playwright
- The SMS confirmation text: "You're on the waitlist at {Course Name}. Keep your phone handy – we'll text you when a spot opens up!"

