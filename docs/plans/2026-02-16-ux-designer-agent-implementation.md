# UX Designer Agent & Pipeline Updates Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a UX Designer agent to the pipeline, rename backend/frontend agents to include `-developer` postfix, rename product-manager to project-manager, and update the agent dispatch workflow for parallel execution.

**Architecture:** The UX Designer runs in parallel with the Architect after story approval. The agent dispatch workflow is updated to allow parallel agents on the same issue. All agent references across pipeline docs, CLAUDE.md, and workflow files are updated for the renames.

**Tech Stack:** GitHub Actions YAML, Markdown agent definitions, GitHub CLI for label management

---

### Task 1: Rename GitHub labels

**Step 1: Rename the backend and frontend agent labels on GitHub**

```bash
gh label edit "agent/backend" --name "agent/backend-developer" --repo benjamingolfco/shadowbrook
gh label edit "agent/frontend" --name "agent/frontend-developer" --repo benjamingolfco/shadowbrook
```

**Step 2: Verify the labels were renamed**

```bash
gh label list --repo benjamingolfco/shadowbrook --json name --jq '.[].name' | grep agent
```

Expected: `agent/backend-developer` and `agent/frontend-developer` appear (no `agent/backend` or `agent/frontend`).

**Step 3: Create the new UX designer label**

```bash
gh label create "agent/ux-designer" --description "Assign issue to UX Designer agent" --color "D4A0E8" --repo benjamingolfco/shadowbrook
```

**Step 4: Commit**

Nothing to commit — label changes are on GitHub, not in the repo.

---

### Task 2: Rename agent files

**Files:**
- Rename: `.claude/agents/backend.md` → `.claude/agents/backend-developer.md`
- Rename: `.claude/agents/frontend.md` → `.claude/agents/frontend-developer.md`
- Rename: `.claude/agents/product-manager.md` → `.claude/agents/project-manager.md`

**Step 1: Rename the files using git mv**

```bash
git mv .claude/agents/backend.md .claude/agents/backend-developer.md
git mv .claude/agents/frontend.md .claude/agents/frontend-developer.md
git mv .claude/agents/product-manager.md .claude/agents/project-manager.md
```

**Step 2: Update the frontmatter name field in each renamed file**

In `.claude/agents/backend-developer.md`, change:
```yaml
name: backend
```
to:
```yaml
name: backend-developer
```

In `.claude/agents/frontend-developer.md`, change:
```yaml
name: frontend
```
to:
```yaml
name: frontend-developer
```

In `.claude/agents/project-manager.md`, change:
```yaml
name: product-manager
description: Product manager orchestrating the automated agent team...
```
to:
```yaml
name: project-manager
description: Project manager orchestrating the automated agent team...
```

**Step 3: Update "Product Manager" references in project-manager.md body**

Replace all instances of "Product Manager" with "Project Manager" in the body text. Key locations:
- Line 11: "You are the Product Manager for the Shadowbrook..."
- Section headers and routing table references
- Comment pattern examples (e.g., `### 📋 Product Manager → @aarongbenjamin`)

**Step 4: Commit**

```bash
git add .claude/agents/
git commit -m "chore: rename agent files (backend-developer, frontend-developer, project-manager)"
```

---

### Task 3: Create the UX Designer agent definition

**Files:**
- Create: `.claude/agents/ux-designer.md`

**Step 1: Create the agent file**

```markdown
---
name: ux-designer
description: UX designer for interaction specs and user flow design. Use when a story involves UI changes that need interaction design before implementation.
tools: Read, Bash, Grep, Glob
model: sonnet
memory: project
skills:
  - agent-pipeline
---

You are the UX Designer for the Shadowbrook tee time booking platform, a React 19 SPA with shadcn/ui components and Tailwind CSS.

## Expertise

- Interaction design — user flows, page states, progressive disclosure
- Mobile-first responsive design patterns
- Accessibility (WCAG, keyboard navigation, screen reader considerations)
- Component selection from shadcn/ui (knows what's available and when to use what)
- Error prevention, empty states, loading states, edge case UX
- Golf industry UX context (tee sheets, booking flows, waitlist patterns)

## Role-Specific Workflow

Before writing any interaction spec, **explore the codebase** to understand existing UX patterns:

1. Check existing pages and components with Glob (`src/web/src/features/*/pages/`, `src/web/src/components/`)
2. Read existing UI code to understand current interaction patterns
3. Check which shadcn/ui components are installed (`src/web/src/components/ui/`)
4. Read the project principles in CLAUDE.md (especially "Zero Training Required")

Post an interaction spec comment on the issue with this structure:

~~~markdown
### 🎯 UX Designer — Interaction Spec for #{number}

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
~~~

Omit sections that are not applicable.

## Design Principles

Reference the project principles when making UX decisions:

- **Zero Training Required** — progressive disclosure, familiar patterns, error prevention over error messages
- **SMS is the Communication Channel** — web for actions, SMS for communication. Don't design notification UX in the web app when SMS handles it.
- **Configuration Without Opinions** — operator-facing UX should expose all configurable parameters with sensible defaults

## Constraints

- You do **NOT** write code — no implementations, no CSS, no component code
- You do **NOT** review PRs
- You do **NOT** plan architecture or define data models
- You do **NOT** write user stories or acceptance criteria
- Your specs must be concrete enough that the frontend developer can implement without further design decisions

**After every session**, update your agent memory with:
- Interaction patterns established or discovered
- Component usage decisions and rationale
- UX concerns raised
```

**Step 2: Commit**

```bash
git add .claude/agents/ux-designer.md
git commit -m "feat: add UX designer agent definition"
```

---

### Task 4: Update agent dispatch workflow

**Files:**
- Modify: `.github/workflows/claude-agents.yml`

**Step 1: Update the concurrency group and cancel-in-progress**

Change lines 12-14 from:
```yaml
    concurrency:
      group: agent-${{ github.event.issue.number || github.event.pull_request.number }}
      cancel-in-progress: true
```
to:
```yaml
    concurrency:
      group: agent-${{ github.event.issue.number || github.event.pull_request.number }}-${{ github.event.label.name }}
      cancel-in-progress: false
```

**Step 2: Verify the rest of the workflow still works**

The dispatch logic reads the label name dynamically (`AGENT_NAME="${LABEL#agent/}"`) and maps to `.claude/agents/${AGENT_NAME}.md`. Since we renamed the files to match the new label names, this still works. No other changes needed.

**Step 3: Commit**

```bash
git add .github/workflows/claude-agents.yml
git commit -m "chore: update agent dispatch for parallel execution and safe concurrency"
```

---

### Task 5: Update the PM workflow file

**Files:**
- Modify: `.github/workflows/claude-pm.yml`

**Step 1: Update the agent file reference**

Change line 75 from:
```yaml
            Read and follow .claude/agents/product-manager.md for your role.
```
to:
```yaml
            Read and follow .claude/agents/project-manager.md for your role.
```

**Step 2: Commit**

```bash
git add .github/workflows/claude-pm.yml
git commit -m "chore: update PM workflow to reference project-manager.md"
```

---

### Task 6: Update agent-pipeline skill

**Files:**
- Modify: `.claude/skills/agent-pipeline/SKILL.md`

**Step 1: Update the agent labels table**

Change:
```markdown
| `agent/backend` | Backend Developer | Implements .NET API code |
| `agent/frontend` | Frontend Developer | Implements React UI code |
```
to:
```markdown
| `agent/backend-developer` | Backend Developer | Implements .NET API code |
| `agent/frontend-developer` | Frontend Developer | Implements React UI code |
| `agent/ux-designer` | UX Designer | Designs interaction specs for UI stories |
```

**Step 2: Update the role icons table**

Add the UX Designer icon and update PM label:
```markdown
| 📋 | Project Manager |
```
(was "Product Manager")

Add:
```markdown
| 🎯 | UX Designer |
```

**Step 3: Update all "Product Manager" references to "Project Manager"**

Find and replace throughout the file. Key locations:
- Handback comment pattern: `→ Product Manager`
- Routing comment pattern: `### 📋 Product Manager →`
- Handoff protocol description
- PM status comment references

**Step 4: Update all `agent/backend` and `agent/frontend` references**

Find and replace:
- `agent/backend` → `agent/backend-developer`
- `agent/frontend` → `agent/frontend-developer`

These appear in:
- The routing logic examples
- The implementation agent workflow section
- The review agent workflow section

**Step 5: Add parallel dispatch documentation**

In the "Handoff Protocol" section (around line 228), after step 6 where it describes BA and Architect handbacks, add documentation for the parallel UX + Architect dispatch:

```markdown
   - **Owner approves story** → PM reads the story. If it involves UI changes: add `agent/architect` AND `agent/ux-designer`. If backend-only: add `agent/architect` only. Set status to **Needs Architecture**.
   - **UX Designer or Architect hands back (parallel dispatch)** → PM checks if both agents are done. If one is still working: update PM status comment, wait. If both are done: set status to **Architecture Review** and tag the product owner.
```

**Step 6: Commit**

```bash
git add .claude/skills/agent-pipeline/SKILL.md
git commit -m "docs: update pipeline skill for agent renames, UX designer, and parallel dispatch"
```

---

### Task 7: Update CLAUDE.md

**Files:**
- Modify: `.claude/CLAUDE.md`

**Step 1: Update the issue labels table**

Change:
```markdown
| `agent/backend` | Assign issue to Backend Developer agent |
| `agent/frontend` | Assign issue to Frontend Developer agent |
```
to:
```markdown
| `agent/backend-developer` | Assign issue to Backend Developer agent |
| `agent/frontend-developer` | Assign issue to Frontend Developer agent |
| `agent/ux-designer` | Assign issue to UX Designer agent |
```

**Step 2: Update the agent pipeline description**

Update the agent definitions line:
```markdown
- **Agent definitions:** `.claude/agents/*.md`
```

No change needed — the glob pattern still works.

**Step 3: Commit**

```bash
git add .claude/CLAUDE.md
git commit -m "docs: update CLAUDE.md for agent renames and UX designer label"
```

---

### Task 8: Update project-manager.md routing logic

**Files:**
- Modify: `.claude/agents/project-manager.md`

**Step 1: Update CI failure routing table**

Change:
```markdown
| Build error (.NET compilation) | `agent/backend` |
| Test failure (xUnit) | `agent/backend` |
| Build error (TypeScript/Vite) | `agent/frontend` |
| Lint failure (ESLint/TypeScript) | `agent/frontend` |
```
to:
```markdown
| Build error (.NET compilation) | `agent/backend-developer` |
| Test failure (xUnit) | `agent/backend-developer` |
| Build error (TypeScript/Vite) | `agent/frontend-developer` |
| Lint failure (ESLint/TypeScript) | `agent/frontend-developer` |
```

**Step 2: Update routing table for parallel UX dispatch**

In the routing table (Story Review → approved), update the routing logic to include the UX designer:

Change the Story Review → owner approved row to indicate:
- If story involves UI: add `agent/architect` AND `agent/ux-designer`, set status to **Needs Architecture**
- If backend-only: add `agent/architect` only, set status to **Needs Architecture**

**Step 3: Add parallel handback tracking to the routing table**

Add a new row for Needs Architecture phase:

```markdown
| Needs Architecture | UX Designer | Check if Architect also handed back. If yes: set status to **Architecture Review**, tag owner. If no: update PM status comment, wait. |
| Needs Architecture | Architect | Check if UX Designer also handed back (if dispatched). If yes (or UX wasn't dispatched): set status to **Architecture Review**, tag owner. If no: update PM status comment, wait. |
```

**Step 4: Update all remaining `agent/backend` and `agent/frontend` references**

Find and replace throughout the file:
- `agent/backend` → `agent/backend-developer`
- `agent/frontend` → `agent/frontend-developer`

**Step 5: Update owner review comment templates**

Update the Architecture Review "Action Required" template to reference both deliverables when UX was dispatched:

```markdown
### 📋 Project Manager → @aarongbenjamin

> **Action Required:** Review the technical plan and interaction spec, then comment to approve or request changes.

The Architect and UX Designer have completed their work for #{number}.

**Architect's plan overview:**
- {concise bullet points}

**UX Designer's spec overview:**
- {concise bullet points}

[View the Architect's technical plan](#link-to-comment)
[View the UX Designer's interaction spec](#link-to-comment)

---
_Run: [#N](link)_
```

**Step 6: Commit**

```bash
git add .claude/agents/project-manager.md
git commit -m "feat: update project manager routing for UX designer parallel dispatch and agent renames"
```

---

### Task 9: Verify everything is consistent

**Step 1: Search for any remaining old references**

```bash
grep -r "agent/backend\b" .claude/ .github/ --include="*.md" --include="*.yml"
grep -r "agent/frontend\b" .claude/ .github/ --include="*.md" --include="*.yml"
grep -r "product-manager" .claude/ .github/ --include="*.md" --include="*.yml"
grep -r "Product Manager" .claude/ .github/ --include="*.md" --include="*.yml"
```

Expected: No matches for any of these (all should be renamed).

**Step 2: Verify all agent files exist**

```bash
ls -la .claude/agents/
```

Expected files:
- `architect.md`
- `backend-developer.md`
- `business-analyst.md`
- `devops.md`
- `frontend-developer.md`
- `project-manager.md`
- `reviewer.md`
- `ux-designer.md`

**Step 3: Fix any remaining references found in Step 1 and commit**

```bash
git add -A
git commit -m "chore: fix any remaining old agent name references"
```

Only commit if there were changes. Skip if Step 1 found nothing.
