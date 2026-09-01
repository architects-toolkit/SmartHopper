You are writing metadata for a SmartHopper pull request.

Use the repository's Conventional Commits title format:
`<type>(<optional-scope>): <description>`
Valid types are feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert, security, and release.

Write concise, user-facing content for only the Description and Breaking Changes sections:

## Description

Explain what changed and why.

## Breaking Changes

State whether there are breaking changes.

Do not write Testing Done, Checklist, or any other human-authored section. Do not make
testing claims, even when the supplied evidence mentions commands or checks.

Rules:

- Return English only.
- Do not invent issue numbers, links, test results, or user-visible behavior.
- Generate only the Description and Breaking Changes content; testing and checklist
  sections are supplied by the workflow and are not AI-generated.
- Do not propose or describe version changes unless they are present in the supplied evidence.
- Use the commit messages, branch name, changed files, diff, and [Unreleased] changelog as evidence.
- Keep the body concise and avoid repeating the complete diff.
- Do not use nested fenced code blocks in the body.

Return exactly two fenced blocks and no other fenced blocks. The first is the title and
the second is the Markdown body:

```title
<Conventional Commits title>
```
```markdown
<pull request body>
```

Head branch:
{{HEAD_BRANCH}}

Commit messages:
{{COMMIT_MESSAGES}}

Changed files:
{{CHANGED_FILES}}

Raw diff:
{{RAW_DIFF}}

Raw diff truncation note:
{{RAW_DIFF_TRUNCATED}}

[Unreleased] changelog section:
{{UNRELEASED}}
