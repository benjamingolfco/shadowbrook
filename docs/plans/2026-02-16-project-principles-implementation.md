# Project Principles Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add the five project principles to CLAUDE.md and README.md so they guide all development decisions.

**Architecture:** Documentation-only change. Add a concise "Project Principles" section to both files — full detail in CLAUDE.md (since it's the agent/developer reference), summary in README.md (since it's the public-facing intro).

**Tech Stack:** Markdown

---

### Task 1: Add Project Principles to CLAUDE.md

**Files:**
- Modify: `.claude/CLAUDE.md` (insert after line 1, before "## Build & Run")

**Step 1: Add the principles section**

Insert the following after the `# Shadowbrook — Tee Time Booking Platform` heading and before `## Build & Run`:

```markdown
## Project Principles

### 1. Zero Training Required
Both golfers and course operators should be productive immediately — no onboarding sessions, no manuals. Progressive disclosure, familiar patterns, error prevention over error messages. Operator tools mirror how they already think (tee sheet = visual grid, not a form).

### 2. Event-Driven Backend
The backend communicates through domain events, not direct service coupling. Key actions publish events; downstream concerns (SMS, waitlist processing, analytics) subscribe. If a downstream system is slow or down, the core flow still completes.

### 3. SMS is the Communication Channel
Web for actions (browse, book, manage profile), SMS for system-to-golfer communication (confirmations, waitlist updates, cancellation notices). Over time, SMS expands from one-way notifications to two-way conversational booking.

### 4. Multi-Tenant from Day One
Every course shares infrastructure but gets its own isolated world. Every API endpoint, query, and data access path is scoped to a course. Per-course configuration for intervals, pricing, policies, and rules. No data leakage between tenants.

### 5. Configuration Without Opinions
Course operators know their course best. Ship with sensible defaults, but every operational parameter is configurable. No hard-coded business rules. As usage data accumulates, we may introduce gentle suggestions — but never force them.
```

**Step 2: Verify formatting**

Visually confirm the section reads well and the heading hierarchy is consistent with the rest of the file.

**Step 3: Commit**

```bash
git add .claude/CLAUDE.md
git commit -m "docs: add project principles to CLAUDE.md"
```

---

### Task 2: Add Project Principles to README.md

**Files:**
- Modify: `README.md` (insert after the opening description, before "## Live Environment")

**Step 1: Add a brief principles section**

Insert the following after the repo description line and before `## Live Environment (Dev)`:

```markdown
## Principles

1. **Zero Training Required** — both golfers and operators are productive immediately
2. **Event-Driven Backend** — domain events, not service coupling, for resiliency and scalability
3. **SMS is the Communication Channel** — web for actions, SMS for golfer communication
4. **Multi-Tenant from Day One** — shared infrastructure, isolated course data and configuration
5. **Configuration Without Opinions** — sensible defaults, full operator control
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add project principles summary to README"
```
