# EditorConfig Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a root `.editorconfig` that codifies universal file basics and C# style conventions (mix of existing patterns + intentional updates).

**Architecture:** Single `.editorconfig` at repo root. Universal section for all files (encoding, line endings, whitespace). Language-specific sections for C#, TypeScript, config files, Markdown, and Makefiles. C# rules use `suggestion` severity — advisory, not build-breaking.

**Tech Stack:** EditorConfig standard + .NET Roslyn analyzer extensions

---

### Task 1: Create .editorconfig

**Files:**
- Create: `.editorconfig`

**Step 1: Create the editorconfig file**

Create `.editorconfig` at the repo root with this exact content:

```ini
root = true

# ──────────────────────────────────────────────
# Universal defaults
# ──────────────────────────────────────────────
[*]
charset = utf-8
end_of_line = lf
trim_trailing_whitespace = true
insert_final_newline = true

# ──────────────────────────────────────────────
# C#
# ──────────────────────────────────────────────
[*.cs]
indent_style = space
indent_size = 4

# -- var preferences (prefer var everywhere) --
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# -- Expression-bodied members (arrow for simple one-liners) --
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_constructors = false:suggestion
csharp_style_expression_bodied_operators = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_expression_bodied_indexers = true:suggestion
csharp_style_expression_bodied_accessors = true:suggestion
csharp_style_expression_bodied_lambdas = true:suggestion
csharp_style_expression_bodied_local_functions = when_on_single_line:suggestion

# -- Braces (always required) --
csharp_prefer_braces = true:suggestion

# -- Null checks (prefer is null / is not null) --
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:suggestion
csharp_style_prefer_null_check_over_type_check = true:suggestion

# -- this. qualification (require for fields) --
dotnet_style_qualification_for_field = true:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_event = false:suggestion

# -- Accessibility modifiers (always explicit) --
dotnet_style_require_accessibility_modifiers = for_non_interface_members:suggestion

# -- File-scoped namespaces --
csharp_style_namespace_declarations = file_scoped:suggestion

# -- Target-typed new() --
csharp_style_implicit_object_creation_when_type_is_apparent = true:suggestion

# -- Primary constructors --
csharp_style_prefer_primary_constructors = true:suggestion

# -- String interpolation --
dotnet_style_prefer_interpolated_verbatim_string = true:suggestion

# -- Other modern preferences --
csharp_prefer_simple_using_statement = true:suggestion
csharp_style_prefer_pattern_matching = true:suggestion
csharp_style_prefer_switch_expression = true:suggestion
csharp_style_prefer_not_pattern = true:suggestion
dotnet_style_prefer_simplified_boolean_expressions = true:suggestion
dotnet_style_object_initializer = true:suggestion
dotnet_style_collection_initializer = true:suggestion
dotnet_style_prefer_conditional_expression_over_assignment = true:suggestion
dotnet_style_readonly_field = true:suggestion

# -- Using directives --
csharp_using_directive_placement = outside_namespace:suggestion
dotnet_sort_system_directives_first = true

# -- Naming: private fields are camelCase (no underscore prefix) --
dotnet_naming_rule.private_fields_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_camel_case.style = camel_case_style
dotnet_naming_rule.private_fields_camel_case.severity = suggestion

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private, protected, private_protected

dotnet_naming_style.camel_case_style.capitalization = camel_case

# -- Naming: interfaces start with I --
dotnet_naming_rule.interfaces_begin_with_i.symbols = interface_symbols
dotnet_naming_rule.interfaces_begin_with_i.style = begins_with_i
dotnet_naming_rule.interfaces_begin_with_i.severity = suggestion

dotnet_naming_symbols.interface_symbols.applicable_kinds = interface
dotnet_naming_symbols.interface_symbols.applicable_accessibilities = *

dotnet_naming_style.begins_with_i.required_prefix = I
dotnet_naming_style.begins_with_i.capitalization = pascal_case

# -- Naming: types, methods, properties are PascalCase --
dotnet_naming_rule.public_members_pascal_case.symbols = public_symbols
dotnet_naming_rule.public_members_pascal_case.style = pascal_case_style
dotnet_naming_rule.public_members_pascal_case.severity = suggestion

dotnet_naming_symbols.public_symbols.applicable_kinds = class, struct, enum, delegate, method, property, event, namespace
dotnet_naming_symbols.public_symbols.applicable_accessibilities = *

dotnet_naming_style.pascal_case_style.capitalization = pascal_case

# ──────────────────────────────────────────────
# TypeScript / JavaScript
# ──────────────────────────────────────────────
[*.{ts,tsx,js,jsx}]
indent_style = space
indent_size = 2

# ──────────────────────────────────────────────
# Config files
# ──────────────────────────────────────────────
[*.{json,yaml,yml}]
indent_style = space
indent_size = 2

# ──────────────────────────────────────────────
# Markdown
# ──────────────────────────────────────────────
[*.md]
trim_trailing_whitespace = false

# ──────────────────────────────────────────────
# Makefile
# ──────────────────────────────────────────────
[Makefile]
indent_style = tab
```

**Step 2: Verify the backend builds cleanly**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeds with no errors. May show new style suggestions — that's fine, they're not errors.

**Step 3: Verify frontend lint passes**

Run: `pnpm --dir src/web lint`
Expected: Passes unchanged — editorconfig doesn't affect ESLint behavior.

**Step 4: Commit**

```bash
git add .editorconfig
git commit -m "chore: add .editorconfig with universal basics and C# style rules"
```

---

### Task 2: Update CLAUDE.md code conventions

**Files:**
- Modify: `.claude/CLAUDE.md` (Code Conventions section)

**Step 1: Add editorconfig reference to CLAUDE.md**

In the "Code Conventions" section, add a note that `.editorconfig` defines the authoritative C# style rules. Mention key changes from legacy patterns:

- `var` preferred over explicit types
- Braces always required
- Private fields: `camelCase` (no underscore prefix), qualified with `this.`
- Target-typed `new()` preferred
- Primary constructors preferred for DI classes

**Step 2: Commit**

```bash
git add .claude/CLAUDE.md
git commit -m "docs: update CLAUDE.md with editorconfig conventions"
```

---

## Design Reference

See `docs/plans/2026-03-06-editorconfig-design.md` (this file, first section) for the approved design decisions.

### Decisions Summary

| Rule | Decision | Change from existing? |
|------|----------|----------------------|
| `var` usage | Prefer `var` everywhere | Yes |
| Expression bodies | Arrow for one-liners, blocks otherwise | No |
| Braces | Always required | Yes |
| Null checks | `is null` / `is not null` | No |
| Private field naming | `camelCase` (no underscore) | Yes |
| `this.` qualifier | Required for fields | Yes |
| Accessibility modifiers | Always explicit | No |
| File-scoped namespaces | Enforced | No |
| Target-typed `new()` | Preferred | Yes |
| Primary constructors | Preferred | Yes |
| String interpolation | Preferred | No |

### Migration Strategy

No bulk reformatting. New code follows new conventions. Existing code updated when files are touched for other reasons.
