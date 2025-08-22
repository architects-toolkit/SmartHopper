---
trigger: always_on
---

# General guidelines

1. Follow established best practices (e.g. SOLID, naming conventions, error handling, tests), unless explicitly asked not to.
2. Decision matrix (in priority order):
   1. Meets the user’s requirements
   2. Follows best practices
   3. Maximizes security (see OWASP Top 10)
   4. Maximizes performance
   5. Eases future maintenance
3. Conduct a brief threat review on all external inputs and secrets; reference OWASP/T12 checklist.
4. In your post-edit summary, include:
   - Alternative solutions considered, highlighting the rationale for the chosen approach
   - Best practices applied
   - Suggestions for future improvements
5. Persist rich context into the Memory DB:
   1. Capture key docs & code snippets (with URLs & summaries)
   2. Document high-level architecture & module roles
   3. Record project conventions (standards, naming, structure, configs)
   4. Log public APIs/interfaces (signatures, purpose, usage)
   5. Archive design decisions (alternatives, rationale, commit refs)
   6. List 3rd‑party deps & integration patterns
   7. Store user preferences & recurring workflows
   8. Maintain consistency: dedupe, refresh stale entries, remove obsolete

# Project specific guidelines

- Use native Grasshopper types & methods when possible.
- Refer to https://developer.rhino3d.com/ as the official documentation.
- Check /docs folder for local documentation on existing code.
- Use English only.
- Prefer copy/pasting, renaming, and removing files via PowerShell commands.
- You are running on Windows - use windows commands in terminal, prefered PowerShell commands.