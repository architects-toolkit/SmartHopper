---
trigger: always_on
---

# General guidelines

1. Follow established best practices (e.g. SOLID, naming conventions, error handling, tests), unless explicitly asked not to.
2. Decision matrix (in priority order):
   1. Meet the user’s requirements
   2. Follow best practices
   3. Avoid patching symptoms, identify root causes and fix them instead
   4. Maximize security (see OWASP Top 10)
   5. Improve performance
   6. Ease future maintenance
3. When you get stuck in a maze of reasoning, you should stop, give the user a full summary of what you've found, and ask for help.
4. Conduct a brief threat review on all external inputs and secrets; reference OWASP/T12 checklist.
5. In your post-edit summary, include:
   - Alternative solutions considered, highlighting the rationale for the chosen approach
   - Best practices applied
   - Suggestions for future improvements
6. Persist rich context into the Memory DB:
   1. Capture key docs & code snippets (with URLs & summaries)
   2. Document high-level architecture & module roles
   3. Record project conventions (standards, naming, structure, configs)
   4. Log public APIs/interfaces (signatures, purpose, usage)
   5. Archive design decisions (alternatives, rationale, commit refs)
   6. List 3rd‑party deps & integration patterns
   7. Store user preferences & recurring workflows
   8. Maintain consistency of memories: dedupe, refresh stale entries, remove obsolete

# Project specific guidelines

- Use native Grasshopper types & methods when possible.
- Refer to https://developer.rhino3d.com/ as the official documentation.
- Check /docs folder for local documentation on existing code.
- Use English only.
- Prefer copy/pasting, renaming, and removing files via PowerShell commands.
- You are running on Windows - use windows commands in terminal, prefered PowerShell commands.