---
name: claude-code-mastery
description: Deep-dive into Claude Code documentation to master configuration, skills, subagents, hooks, and best practices
disable-model-invocation: true
allowed-tools: WebFetch, WebSearch, Read, Write, Glob, Grep
---

# Claude Code Mastery

Research Claude Code documentation to become an expert on the topic claude code setup.

## Documentation Sources

Fetch the documentation index first, then dive into relevant pages:

1. **Index**: https://code.claude.com/docs/llms.txt
2. **Core docs**: https://code.claude.com/docs/en/{page}

Key pages by topic:
- **Setup**: features-overview, memory, settings, best-practices
- **Skills**: skills (creation, frontmatter, patterns)
- **Subagents**: sub-agents (configuration, prompts, tools)
- **Hooks**: hooks, hooks-guide (events, scripts)
- **MCP**: mcp (servers, tools, configuration)
- **Plugins**: plugins, plugins-reference, plugin-marketplaces
- **Prompt Engineering**: https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-4-best-practices

## Research Process

1. **Fetch the docs index** to understand available pages
2. **Fetch 2-3 most relevant pages** for the topic
3. **Cross-reference** with Claude 4.x prompt engineering best practices
4. **Synthesize** into actionable knowledge

## Output Format

After researching, provide:

### Understanding
Explain the concept thoroughly—how it works, when to use it, how it interacts with other features.

### Best Practices
What the docs recommend, including anti-patterns to avoid.

### Practical Application
How this applies to the current project. Reference existing configuration in `.claude/` where relevant.

### Example Configuration
Provide a concrete, ready-to-use example if applicable.

---

**Remember**: The docs emphasize conciseness. "For each line, ask: Would removing this cause Claude to make mistakes? If not, cut it."
