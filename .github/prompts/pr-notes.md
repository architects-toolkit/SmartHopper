You are writing metadata for a SmartHopper pull request.

Use the repository's Conventional Commits title format:
`<type>(<optional-scope>): <description>`
Valid types are feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert, security, and release.

Write a concise, user-facing pull request body using this section structure:

## Description

Explain what changed and why.

## Breaking Changes

State whether there are breaking changes.

## Testing Done

Summarize relevant validation.

## Checklist

- [ ] This PR is focused on a single feature or bug fix
- [ ] Version in Solution.props was updated, if necessary, and follows semantic versioning
- [ ] CHANGELOG.md has been updated
- [ ] PR title follows Conventional Commits format
- [ ] PR description follows the Pull Request Description Template

Rules:
- Return English only.
- Do not invent issue numbers, links, test results, or user-visible behavior.
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
