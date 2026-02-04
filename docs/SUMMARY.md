# Tee Time Booking Platform â€” Product Summary

## Concept

A tee time booking platform that improves fill rates for golf courses and makes finding and booking tee times effortless for golfers. One account, many courses.

---

## Golfer Experience

### Browsing & Discovery

- **Default view is tee-time-first, not course-first.** You open the app and see available tee times across all nearby and favorited courses.
- Favorites surface to the top, everything else sorted by distance.
- Tee times at the same time across different courses are grouped together, so you can pick a time first and a course second.
- You search by a **general time window** ("around 8am") rather than exact slots. The system shows everything in a reasonable range around your target time, sorted by proximity to it.
- **Course-first browsing is also supported.** You can tap into a specific course and see its availability directly for when you know where you want to play.
- Filtering by date, time range, number of players, price, distance radius, and number of holes.
- List view by default with a toggle to map view for exploring unfamiliar areas or when traveling.

### Booking

- Supports 1â€“4 players.
- Solo golfers can be shown options like "play sooner with others, or later solo."
- Basic golfer info (name) shared with the course for check-in purposes.
- Payment happens at the course for now (no upfront payment in v1).

### Waitlist & Notifications

The killer feature. Solves the same problem upfront payment tries to solve (late cancellations leaving empty slots) but in a way that's better for everyone.

- Golfers can set a **waitlist alert** for a time window at one or more courses.
- When a cancellation happens, the **first person on the waitlist** gets a text:
  > "8:07am Saturday at Braemar just opened up. Reply Y to confirm. 2 others waiting â€” you have 15 minutes."
- No reply or decline within 15 minutes rolls to the next person on the waitlist.
- If nobody on the waitlist claims it, the slot goes back to open availability.
- **First come first served** based on when the golfer joined the waitlist.

### SMS Notifications (via Twilio)

- Booking confirmations
- Cancellation alerts
- Waitlist offers

---

## Course Operator Experience

A web dashboard for course staff to manage their tee times and understand their business.

- **Tee sheet view** â€” the day's tee times with booking status.
- **Tee time settings** â€” intervals, hours of operation, capacity.
- **Pricing management** â€” flat rate pricing for now.
- **Analytics dashboard** â€” fill rates, cancellation recovery rates, usage trends, efficiency metrics.

---

## Tech Stack

- **Backend:** .NET
- **Frontend:** React (single responsive web app for both golfers on mobile and operators on desktop)

---

## Parked for Later

- Native mobile app (React Native)
- AI conversational booking via SMS
- Dynamic pricing
- Course profiles (photos, scorecard, reviews, amenities)
- Starter scanner check-in (skip the clubhouse)
- Upfront payment / cancellation fees
- Advanced check-in flow (confirmation codes, app-based check-in)
- Which sms provider.