---
description: Write a commit message for the currently staged or uncommitted changes
---

The aim is to return a Conventional Commit message `<type>(<scope>): <subject>` for the current staged or uncommitted changes.

1. Check for staged changes or, if none, all uncommitted changes.

2. Analyze them.

3. Return one semantic commit message.
