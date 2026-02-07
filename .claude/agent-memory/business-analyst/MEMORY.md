# Business Analyst Memory

## Issues Created

### Session: 2026-02-04 - Waitlist Gaps
**Parent Epic:** Issue #3 (Waitlist & SMS Notifications)

Created two user stories to fill gaps in waitlist functionality:

1. **Issue #27**: As a golfer, I can remove myself from a waitlist
   - Labels: `golfers love`, `course operators love`, `v1`
   - Covers the gap where golfers need to cancel waitlist entries before being offered a slot
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/27

2. **Issue #28**: As a course operator, I can see waitlist demand for time slots
   - Labels: `course operators love`, `v1`
   - Covers the missing operator-side value proposition (visibility into demand)
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/28

Both linked as sub-issues of #3 and added to project #1.

### Session: 2026-02-05 - Free Cancellation Policy Stories
**Parent Feature:** Issue #36 (Free Cancellation Policy - 30 Minutes Before)

Created three user stories to complete the cancellation policy feature:

1. **Issue #47**: As a golfer, I can cancel a booking without penalty within the free cancellation window
   - Labels: `golfers love`, `v1`
   - Covers the happy path of cancelling within the free window
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/47

2. **Issue #48**: As a golfer, I see a warning when cancelling after the free cancellation cutoff
   - Labels: `golfers love`, `v1`
   - Covers the late cancellation warning and fee disclosure flow
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/48

3. **Issue #49**: As a course operator, I can configure the cancellation policy for my course
   - Labels: `course operators love`, `v1`
   - Covers operator-side configuration of cutoff time and late fees
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/49

All linked as sub-issues of #36 and added to project #1. Combined with existing issue #44 (see cutoff time), this feature is now complete.

### Session: 2026-02-05 - Auto-Interval Suggestions (v2 Feature)
**Parent Feature:** Issue #54 (Auto-interval suggestions)

Created three user stories for v2 operator-side analytics feature:

1. **Issue #56**: As a course operator, I can view interval suggestions based on pace data
   - Labels: `course operators love`, `v2`
   - Covers viewing data-driven recommendations, understanding rationale, and applying suggestions
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/56

2. **Issue #60**: As a course operator, I can see confidence levels for interval suggestions
   - Labels: `course operators love`, `v2`
   - Covers confidence indicators, understanding reliability factors, and prioritizing recommendations
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/60

3. **Issue #63**: As a course operator, I can dismiss interval suggestions that don't fit my needs
   - Labels: `course operators love`, `v2`
   - Covers dismissing, viewing dismissed items, and restoring suggestions
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/63

All linked as sub-issues of #54 and added to project #1. This is an operator-only analytics tool (golfers don't see or interact with it), so only `course operators love` label applies.

### Session: 2026-02-05 - Skill-based Group Matching (v2 Feature)
**Parent Feature:** Issue #50 (Skill-based group matching)

Created four user stories for v2 two-sided matching feature:

1. **Issue #53**: As a golfer, I can opt into skill-based group matching
   - Labels: `golfers love`, `v2`
   - Covers opt-in during booking, saving preferences, and updating default preference
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/53

2. **Issue #55**: As a golfer, I can see suggested matched groups when booking
   - Labels: `golfers love`, `v2`
   - Covers viewing matched groups, match quality indicators, joining, and handling no-matches
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/55

3. **Issue #61**: As a golfer, I can see my group's skill match quality after booking
   - Labels: `golfers love`, `v2`
   - Covers viewing match details, seeing matches in booking list, and group change notifications
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/61

4. **Issue #68**: As a course operator, I can see pace-of-play analytics for matched groups
   - Labels: `course operators love`, `v2`
   - Covers pace analytics, adoption metrics, group performance comparison, and exporting
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/68

All linked as sub-issues of #50 and added to project #1. This is a "both golfers AND course operators love" feature from the roadmap - uses v1 handicap data (#41) to improve pace of play and golfer satisfaction through skill-based matching.

### Session: 2026-02-05 - GHIN Handicap Sync (v2 Feature)
**Parent Feature:** Issue #66 (GHIN Handicap Sync)

Created three user stories for v2 golfer-side GHIN integration feature:

1. **Issue #73**: As a golfer, I can link my GHIN number to my profile
   - Labels: `golfers love`, `v2`
   - Covers linking GHIN account, handling invalid numbers, and viewing linked status
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/73

2. **Issue #76**: As a golfer, I see my auto-synced GHIN handicap across the platform
   - Labels: `golfers love`, `v2`
   - Covers viewing synced handicap, automatic updates, and manual refresh trigger
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/76

3. **Issue #78**: As a golfer, I can disconnect my GHIN account
   - Labels: `golfers love`, `v2`
   - Covers disconnecting, data retention, and reconnecting later
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/78

All linked as sub-issues of #66 and added to project #1. This is a golfer-only v2 enhancement that builds on v1 manual handicap entry (#41) by allowing automatic sync from USGA's GHIN system. Courses benefit indirectly from more accurate handicap data.

### Session: 2026-02-05 - Post-Round Pace Feedback (v2 Feature)
**Parent Feature:** Issue #52 (Post-Round Pace Feedback via SMS)

Created three user stories for v2 feedback loop feature:

1. **Issue #75**: As a golfer, I receive an SMS asking about pace after my round
   - Labels: `golfers love`, `course operators love`, `v2`
   - Covers receiving feedback request, submitting feedback, and invalid response handling
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/75

2. **Issue #77**: As a course operator, I view pace feedback trends and insights
   - Labels: `golfers love`, `course operators love`, `v2`
   - Covers viewing overall score, filtering by time slot/date range, and drilling into responses
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/77

3. **Issue #79**: As a course operator, I configure pace feedback settings
   - Labels: `golfers love`, `course operators love`, `v2`
   - Covers enabling/disabling feature, setting timing delay, and frequency controls
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/79

All linked as sub-issues of #52 and added to project #1. This is a "both golfers AND course operators love" feature - simple post-round SMS feedback loop where golfers feel heard and courses get actionable data.

### Session: 2026-02-05 - Window-based Booking (v3 Feature)
**Parent Feature:** Issue #80 (Window-based booking)

Created six user stories for v3 fundamental paradigm shift feature:

1. **Issue #81**: As a golfer, I can book a time window instead of a specific tee time
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers viewing windows, selecting/booking, confirmation, and viewing assigned time
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/81

2. **Issue #83**: As a golfer, I understand how window-based assignment works
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers explanation, assignment timing, factors considered, and opting out
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/83

3. **Issue #84**: As a golfer, I can see my assigned tee time and update my plans
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers viewing assignment, SMS notification, canceling, and requesting reassignment
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/84

4. **Issue #88**: As a course operator, I can configure time windows for booking
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers enabling windows, defining windows, mixed booking mode, and assignment timing config
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/88

5. **Issue #93**: As a course operator, I can assign exact tee times within booked windows
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers viewing window bookings, manual assignment, automatic assignment, and reviewing suggestions
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/93

6. **Issue #97**: As a course operator, I can see throughput analytics for window-based booking
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers throughput comparison, gap analysis, group matching quality, and export/reporting
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/97

All linked as sub-issues of #80 and added to project #1. This is a "both golfers AND course operators love" feature from the roadmap - golfers trade exact-time control for better-matched groups and smoother pace; courses gain optimization flexibility to improve throughput and reduce gaps. Fundamental shift from traditional booking model.

### Session: 2026-02-05 - Throughput-per-window Analytics (v3 Feature)
**Parent Feature:** Issue #85 (Throughput-per-window analytics)

Created three user stories for v3 operator-only analytics feature:

1. **Issue #90**: As a course operator, I can view throughput data for each time window
   - Labels: `course operators love`, `v3`
   - Covers viewing actual vs expected group counts, utilization percentages, filtering, and drilling into details
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/90

2. **Issue #95**: As a course operator, I can compare actual pace vs expected pace per window
   - Labels: `course operators love`, `v3`
   - Covers pace variance indicators, identifying slow windows, and trend analysis over time
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/95

3. **Issue #103**: As a course operator, I can export throughput data for external analysis
   - Labels: `course operators love`, `v3`
   - Covers CSV export with custom fields, report sharing, and scheduled email delivery
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/103

All linked as sub-issues of #85 and added to project #1. This is a v3 "course operators only" feature — gives operators actionable capacity data to tune window sizes and intervals based on real throughput, not theoretical estimates. Golfers don't see or interact with this.

### Session: 2026-02-05 - Real-time Tee Time Assignment via SMS (v3 Feature)
**Parent Feature:** Issue #86 (Real-time tee time assignment via SMS)

Created four user stories for v3 future feature:

1. **Issue #89**: As a golfer, I can receive my exact tee time via SMS
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers receiving assignment notification, viewing in app, and understanding timing
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/89

2. **Issue #94**: As a golfer, I can receive updates if my tee time changes
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers change notifications, viewing updated assignments, and understanding why changes happen
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/94

3. **Issue #98**: As a course operator, I can configure tee time assignment notifications
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers setting timing rules, configuring automatic vs manual, and viewing active configuration
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/98

4. **Issue #100**: As a course operator, I can trigger tee time assignments manually
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers reviewing pending assignments, sending notifications, and handling errors
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/100

All linked as sub-issues of #86 and added to project #1. This is a "both golfers AND course operators love" v3 feature that closes the loop on window-based booking - golfers get flexibility but still know exactly when to arrive. Builds on window-based booking and day-of group optimization.

### Session: 2026-02-05 - Starter Dispatch Tools (v3 Feature)
**Parent Feature:** Issue #91 (Starter dispatch tools)

Created four user stories for v3 starter optimization feature:

1. **Issue #96**: As a starter, I can view real-time upcoming groups and check-in status
   - Labels: `course operators love`, `v3`
   - Covers live view of next 2-3 hours, check-in indicators, group details, and late arrival alerts
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/96

2. **Issue #102**: As a starter, I can make last-minute group composition adjustments
   - Labels: `course operators love`, `v3`
   - Covers moving players between groups, merging partial groups, splitting large groups, and undo
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/102

3. **Issue #105**: As a starter, I can fill gaps with walk-up players
   - Labels: `course operators love`, `v3`
   - Covers adding walk-ups, quick registration, assigning to groups, and payment tracking
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/105

4. **Issue #106**: As a starter, I can adjust spacing between groups to optimize pace
   - Labels: `course operators love`, `v3`
   - Covers adding buffer time, compressing intervals, skipping slots, and spacing recommendations
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/106

All linked as sub-issues of #91 and added to project #1. This is a v3 "course operators only" feature — shifts starters from traffic cops to optimizers with real-time group assembly and day-of adjustments. Builds on window-based booking and day-of optimization. Golfers don't see or interact with this tool.

### Session: 2026-02-05 - Day-of Group Optimization (v3 Feature)
**Parent Feature:** Issue #82 (Day-of group optimization)

Created five user stories for v3 optimization feature:

1. **Issue #87**: As a golfer, I can view my day-of group assignment
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers viewing assigned group, understanding match rationale, receiving notifications, and handling incomplete groups
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/87

2. **Issue #92**: As a golfer, I can request to stay in my original booking group
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers opting out of optimization, viewing opt-out status, group-wide preferences, and changing preferences
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/92

3. **Issue #99**: As a course operator, I can view optimized groups for each window
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers viewing optimization results, understanding rationale, viewing unoptimized groups, and filtering/sorting
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/99

4. **Issue #101**: As a course operator, I can manually override optimized group assignments
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers moving golfers between groups, creating manual groups, undoing overrides, and notifying affected golfers
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/101

5. **Issue #104**: As a course operator, I can configure day-of optimization settings
   - Labels: `golfers love`, `course operators love`, `v3`
   - Covers enabling/disabling, setting priorities, configuring timing, and reviewing optimization history
   - URL: https://github.com/benjamingolfco/shadowbrook/issues/104

All linked as sub-issues of #82 and added to project #1. This is a v3 "both golfers AND course operators love" feature - on day-of-play, the system optimizes group composition within booked windows to maximize both golfer compatibility and course throughput.

## GitHub API Patterns

### Sub-Issue Linking
The REST API endpoint `POST /repos/{owner}/{repo}/issues/{parent}/sub_issues` doesn't work reliably. Use GraphQL instead:

```bash
gh api graphql -f query='
  mutation {
    addSubIssue(input: {
      issueId: "I_kwDORIyZ887oYQqG"
      subIssueId: "I_kwDORIyZ887ocdAz"
    }) {
      issue {
        number
      }
    }
  }'
```

Get node IDs first:
```bash
gh api graphql -f query='
  query {
    repository(owner: "benjamingolfco", name: "shadowbrook") {
      issue(number: 27) {
        id
      }
    }
  }'
```

## User Story Format (Shadowbrook Style)

**Current Standard:** Given/When/Then format (as of 2026-02-05)

See `/home/aaron/dev/orgs/benjamingolfco/shadowbrook/.claude/skills/writing-user-stories/SKILL.md` for full guidelines.

```markdown
## User Story
As a [role], I want [goal], so that [benefit].

## Details
Brief context (1-2 sentences).

## Acceptance Criteria

### Workflow Name
Given [precondition]
When [action]
Then [outcome]
And [additional outcome]

### Another Workflow
Given [precondition]
When [action]
Then [outcome]
```

Key principles:
- Use Given/When/Then for all acceptance criteria
- Group criteria by user workflow (3-7 per workflow)
- Focus on observable behavior, not implementation
- Keep criteria scoped to the story's user role only
- If criteria involve a different role, suggest a Related Stories section

Issues #17-#22, #27, and #28 updated to this format on 2026-02-05.

## Backlog Insights

### Waitlist Epic (#3) - Now Complete
Original sub-issues (reformatted on 2026-02-05):
- #17: Join waitlist
- #18: Receive SMS notification when slot opens
- #19: Receive waitlist offer when previous person declines (was "As the system")
- #20: Browse unclaimed waitlist slots (was "As the system")
- #21: Receive SMS booking confirmations
- #22: Receive SMS cancellation alerts

Gaps identified and filled:
- #27: Cancel waitlist entry (golfer self-service)
- #28: Operator visibility into demand (completes value prop)

The waitlist epic now covers:
1. Join flow (golfer)
2. Notification flow (golfer)
3. Offer rollover flow (golfer)
4. Unclaimed slot return (golfer)
5. Confirmation messages (golfer)
6. Cancellation alerts (golfer)
7. Cancel waitlist entry (golfer)
8. Visibility into demand (operator)

Key fix: Issues #19 and #20 originally used "As the system" which is not a real user role. Reframed from golfer perspective since they experience the behavior.
