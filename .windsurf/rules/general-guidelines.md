---
trigger: always_on
---

# General guidelines
- Use native Grasshopper types & methods when possible.
- Refer to https://developer.rhino3d.com/ as the official documentation.
- Use English only.
- Only change code lines directly needed to implement the request; avoid unrelated refactors.
- Prefer copy/pasting, renaming, and removing files via PowerShell commands, rather than direct edits.
- You are running on Windows - use windows commands in terminal, prefered PowerShell commands.
- Avoid defining default values twice.
- Use base classes and methods when possible. Inheritdoc when possible.
- Avoid code duplication. If you detect duplicated code or potentially mergable code, warn the user. Do not merge duplicated code until the user confirms.