---
description: Write a commit message for the currently staged/uncommited changes
---

The aim is to return a semantic commit message "<type>(<scope>): <subject>" for the current staged or uncommited changes.

1. Check for staged changes or, if none, all uncommited changes

2. Analyze them

3. Return a semantinc commit message