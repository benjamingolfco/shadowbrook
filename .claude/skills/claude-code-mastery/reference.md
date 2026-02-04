# Claude Code Documentation Quick Reference

## Documentation URLs

| Topic | URL |
|-------|-----|
| **Index** | https://code.claude.com/docs/llms.txt |
| **Features Overview** | https://code.claude.com/docs/en/features-overview |
| **Best Practices** | https://code.claude.com/docs/en/best-practices |
| **Memory (CLAUDE.md)** | https://code.claude.com/docs/en/memory |
| **Skills** | https://code.claude.com/docs/en/skills |
| **Subagents** | https://code.claude.com/docs/en/sub-agents |
| **Hooks Guide** | https://code.claude.com/docs/en/hooks-guide |
| **Hooks Reference** | https://code.claude.com/docs/en/hooks |
| **MCP** | https://code.claude.com/docs/en/mcp |
| **Plugins** | https://code.claude.com/docs/en/plugins |
| **Plugins Reference** | https://code.claude.com/docs/en/plugins-reference |
| **Settings** | https://code.claude.com/docs/en/settings |
| **Claude 4.x Prompting** | https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-4-best-practices |

## Extension Hierarchy

```
Feature          | Loads When        | Context Cost  | Best For
-----------------|-------------------|---------------|---------------------------
CLAUDE.md        | Session start     | Every request | Always-on rules, commands
Skills           | On demand         | When invoked  | Reference docs, workflows
Subagents        | When spawned      | Isolated      | Parallel work, isolation
MCP              | Session start     | Every request | External services
Hooks            | On trigger        | Zero          | Deterministic automation
```

## Layering Rules

- **CLAUDE.md**: Additive (all levels contribute)
- **Skills**: Override by name (managed > user > project)
- **Subagents**: Override by name (CLI > project > user > plugin)
- **MCP**: Override by name (local > project > user)
- **Hooks**: All merge and fire

## Subagent Frontmatter

```yaml
---
name: agent-name              # Required: lowercase, hyphens
description: "When to use"    # Required: helps lead agent delegate
tools: Read, Grep, Glob       # Optional: restricts available tools
disallowedTools: Write, Edit  # Optional: denylist
model: sonnet|opus|haiku      # Optional: defaults to inherit
permissionMode: default|acceptEdits|dontAsk|bypassPermissions|plan
skills: [skill1, skill2]      # Optional: preloaded into context
hooks:                        # Optional: scoped to this agent
  PreToolUse: [...]
  PostToolUse: [...]
---
```

## Skill Frontmatter

```yaml
---
name: skill-name                    # Optional: defaults to directory name
description: "What and when"        # Recommended: helps Claude decide
disable-model-invocation: true      # User-only (no auto-trigger)
user-invocable: false               # Claude-only (hidden from menu)
allowed-tools: Read, Grep           # Tool restrictions when active
context: fork                       # Run in isolated subagent
agent: Explore                      # Which agent for fork context
---
```

## Hook Events

| Event | Matcher | When |
|-------|---------|------|
| PreToolUse | Tool name | Before tool executes |
| PostToolUse | Tool name | After tool executes |
| Stop | (none) | Session/agent ends |
| SubagentStart | Agent name | Subagent begins |
| SubagentStop | (none) | Any subagent completes |

## Key Best Practices

1. **Keep prompts short** - Claude 4.x follows simple guidance well
2. **Tell what TO do, not what NOT to do** - positive framing works better
3. **Add "why" context** - helps Claude generalize correctly
4. **Prune ruthlessly** - "Would removing this cause mistakes? If not, cut it."
5. **Trust Claude's intelligence** - don't enumerate every edge case
