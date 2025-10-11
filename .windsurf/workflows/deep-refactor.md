---
description: Instruct the AI to fully refactor code, prioritizing best quality over minimal changes
auto_execution_mode: 1
---

- We are fully refactoring the code.
- Do not limit yourself to minimal targeted edits. Large high-level refactors or discussion with the user are expected.
- Prioritize best code quality, clear architecture, clean, simple, and easy-to-maintain code.
- Place new features as high as possible, to allow reuse and deduplication.
- Focus on the files the user wants to target to produce high-quality code.
- Forget about potential breaking changes, we'll fix them in the future.
- Let the user choose if they want refactoring to be backward compatible or not.
- Keep documentation and changelog updates for later. Now focus on rethinking the code.
- Remove legacy and obsolete code. 
- The result should be ideal code. Don't stack to existing code limitations.
- Understand the essential logic behind the code and the user's will before suggesting refactors. Ask for human validation if you doubt.