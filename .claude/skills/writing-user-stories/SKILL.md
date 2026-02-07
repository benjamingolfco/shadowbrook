---
name: writing-user-stories
description: Guidelines for writing user stories focused on business value. Use when formatting issues or defining acceptance criteria.
user-invocable: false
---

# Writing User Stories

User stories describe **what** the user needs and **why**, not **how** it will be built.

## Be Concise

Keep user stories short and scannable:
- **Title**: 5-10 words max
- **Description**: 1-2 sentences
- **Acceptance criteria**: 3-7 items per workflow (if you have more, split the story)

Avoid walls of text. If you're writing paragraphs, you're overcomplicating it. A developer should grasp the story in 30 seconds.

## User Story Format

```
As a [user role]
I want [goal/desire]
So that [benefit/value]
```

**Good Example:**
> As a manufacturer admin, I want to upload product images, so that customers can see what they're customizing.

**Bad Example (too technical):**
> As a user, I want the FileUpload component to use Azure Blob Storage with SAS tokens, so that files are stored securely.

## Acceptance Criteria Format

Use Given/When/Then to describe **observable behavior**, not implementation:

```
Given [context/precondition]
When [action/trigger]
Then [expected outcome]
```

**Good Example:**
```
Given I am on the product edit page
When I upload an image file
Then the image appears in the product gallery
And a success message is shown
```

**Bad Example (too technical):**
```
Given the BlobStorageService is configured
When uploadFile() is called with a valid File object
Then the file is uploaded to the 'product-images' container
And the URL is stored in the product.images array
```

## Keep Criteria Focused on the Story's User Role

Acceptance criteria should describe what **the user in the story** experiences, not what other users experience as a side effect.

**The Problem:**
```
As a client admin
I want to disable products
So that customers cannot see unavailable products
```

With mixed acceptance criteria:
```
### Managing Product Status (✅ correct - admin perspective)
Given I'm viewing a product in the admin panel
When I toggle the enable/disable control
Then the product's status updates immediately

### Customer Experience (❌ wrong - different user role)
Given a product is disabled
When customers browse the catalog
Then the disabled product does not appear
```

**The Fix:**

Keep criteria for the story's user role. Suggest separate stories for other roles:

```
### Managing Product Status
Given I'm viewing a product in the admin panel
When I toggle the enable/disable control
Then the product's status updates to "Disabled"
And I see confirmation of the change

### Related Stories

These experiences involve different user roles and should be separate stories:

- As a customer, I want to only see available products, so that I don't
  try to customize unavailable items.

- As a customer, I want clear feedback when accessing an unavailable
  product, so that I understand why I can't customize it.
```

**Why This Matters:**
- **Testability**: Each story can be independently verified by its user role
- **Prioritization**: Admin features might ship before customer-facing changes
- **Clarity**: Developers know exactly whose perspective to test from
- **Scope control**: Prevents stories from ballooning with unrelated criteria

## What to Include

- **User goals** - What does the user want to accomplish?
- **Business value** - Why does this matter?
- **Observable outcomes** - What will the user see/experience?
- **Edge cases** - What happens in error scenarios?
- **User-facing constraints** - File size limits, allowed formats, etc.

## What to Exclude

- File paths and code locations
- Database schemas and field names
- API endpoint names and payloads
- Component names and architecture
- Implementation patterns (repositories, services, etc.)
- Technology choices (unless user-facing)

## Organizing Acceptance Criteria

Group by **user workflow**, not by technical domain:

**Good:**
```markdown
### Upload Flow
- [ ] User can select files from their device
- [ ] User sees upload progress
- [ ] User sees success confirmation

### Validation
- [ ] User sees error for files over 10MB
- [ ] User sees error for unsupported formats
```

**Bad:**
```markdown
### Backend
- [ ] Create FileUploadService with upload() method
- [ ] Add POST /api/files endpoint

### Frontend
- [ ] Add FileUpload component
- [ ] Connect to useFileUpload hook
```

## Suggesting Related Stories

When acceptance criteria would involve a different user role, add a "Related Stories" section at the end:

```markdown
### Related Stories

| User Role | Need | Suggested Story |
|-----------|------|-----------------|
| Customer | See only active products | "As a customer, I want to browse only available products..." |
| Admin | Audit changes | "As an admin, I want to see who disabled a product..." |
```

This keeps the current story focused while ensuring dependent functionality isn't forgotten.

## Questions to Ask (Not Technical Decisions to Make)

When requirements are unclear, ask about:
- User goals and workflows
- Business rules and constraints
- Error handling from user perspective
- Priority and scope

Do NOT ask about:
- Which components to use
- Database design
- API structure
- Code organization

Technical decisions belong in the **Planning** phase, not analysis.
