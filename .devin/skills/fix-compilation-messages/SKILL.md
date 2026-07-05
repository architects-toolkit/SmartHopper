---
name: fix-compilation-messages
description: Fix compilation errors, warnings, and informational messages
allowed-tools:
  - read
  - edit
  - exec
  - grep
  - glob
triggers:
  - user
  - model
---

Fix the following compilation messages (errors, warnings, and informational messages).

Do not cause breaking changes.

Do not ask for confirmation before applying changes that directly fix the messages. Prompt the user for potential breaking changes or doubts on the accurate solution.

Summarize the type of issues fixed, do not list all changes.

Do not run a full rebuild at the end unless the user asks for it; this skill is for applying fixes from already-provided compiler output.
