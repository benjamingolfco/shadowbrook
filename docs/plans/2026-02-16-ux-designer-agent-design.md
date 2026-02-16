# UX Designer Agent Design

## Summary

Add a UX Designer agent to the automated pipeline that produces interaction specs for stories involving UI changes. It runs in parallel with the Architect after story approval, so the frontend developer gets both a technical plan and a UX spec before implementation.

## Agent Identity

- **Name:** `ux-designer`
- **Label:** `agent/ux-designer`
- **Icon:** 🎯
- **Model:** sonnet
- **Skills:** agent-pipeline

### Expertise

- Interaction design — user flows, page states, progressive disclosure
- Mobile-first responsive design patterns
- Accessibility (WCAG, keyboard navigation, screen reader considerations)
- Component selection from shadcn/ui (knows what's available and when to use what)
- Error prevention, empty states, loading states, edge case UX
- Golf industry UX context (tee sheets, booking flows, waitlist patterns)

### Constraints

- Does NOT write code
- Does NOT review PRs
- Does NOT plan architecture or define data models
- Does NOT write user stories or acceptance criteria

## Pipeline Integration

### When it runs

After the owner approves the story (Story Review → approved), the PM reads the story and determines whether it involves UI changes. If yes, the PM dispatches `agent/ux-designer` AND `agent/architect` simultaneously. If backend-only, only the Architect is dispatched.

### Parallel execution and sync gate

Both agents run independently. The PM tracks both handbacks. When both have handed back, the PM advances to Architecture Review and tags the owner. The owner reviews both the technical plan and the UX spec in one pass.

If only the Architect was dispatched (backend-only story), the existing flow is unchanged.

### When it doesn't run

The PM skips the UX agent for stories that don't touch UI — pure backend work, infrastructure tasks, CI/CD changes, etc.

## Output Format

The UX agent posts a Work Output comment (pattern #3 from agent-pipeline) with this structure:

```markdown
### 🎯 UX Designer — Interaction Spec for #N

## User Flow
[Step-by-step description of what the user does and sees]

## Page/Component Breakdown
[What's on the page, how it's laid out, which shadcn/ui components to use]

## States
- **Loading:** [what the user sees while data loads]
- **Empty:** [what the user sees when there's no data]
- **Error:** [what the user sees when something fails]
- **Success:** [confirmation behavior after an action]

## Responsive Behavior
[How the layout adapts from mobile to desktop]

## Accessibility
[Keyboard navigation, focus management, screen reader notes]
```

Sections are omitted when not applicable.

## Workflow Changes

### 1. Agent dispatch workflow (`claude-agents.yml`)

Change the concurrency group to allow different agents to run in parallel on the same issue:

```yaml
concurrency:
  group: agent-${{ github.event.issue.number || github.event.pull_request.number }}-${{ github.event.label.name }}
  cancel-in-progress: false
```

- Adding the label name to the group allows UX + Architect to run simultaneously on the same issue
- `cancel-in-progress: false` protects running agents from being killed mid-work; duplicate triggers queue and the latest pending run is kept

### 2. PM agent routing logic (`product-manager.md`)

Three updates:

**Story Review → approved routing:**
- PM reads the approved story
- If it involves UI: add `agent/architect` AND `agent/ux-designer` labels
- If backend-only: add `agent/architect` only
- Set status to Needs Architecture in both cases

**Parallel handback tracking:**
- When an agent hands back, PM checks: are both agents done?
- If one is done but the other isn't: update PM status comment, wait
- If both are done (or only architect was dispatched): advance to Architecture Review

**Owner review scope:**
- Architecture Review gate covers both the technical plan and the UX spec
- PM's "Action Required" comment links to both deliverables

### 3. Pipeline protocol updates (`agent-pipeline/SKILL.md`)

- Add `agent/ux-designer` to the agent labels table
- Add 🎯 to the role icons table
- Document the parallel dispatch pattern in the routing logic

### 4. CLAUDE.md updates

- Add `agent/ux-designer` to the issue labels table
- Add UX Designer to the agent pipeline description

## New Files

- `.claude/agents/ux-designer.md` — agent definition following the same pattern as other agents

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Text-based interaction specs, no wireframes | Agent runs in CI (text-only). shadcn/ui provides the visual language — UX agent focuses on behavior, not visuals. |
| Parallel with Architect, not serial | Faster pipeline. Conflicts caught at the owner review gate. |
| PM infers UI involvement from story | No extra labels to manage. PM already reads the story for routing. |
| Specs only, no UX review gate | Keeps pipeline lean. Code reviewer checks implementation against the spec. |
| cancel-in-progress: false for agent dispatch | Protects running agents from being killed. Duplicate triggers queue harmlessly. |
