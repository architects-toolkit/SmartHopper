---
description: Initialize work session
---

1. Check if the current branch name has a X.Y.Z version indicator. If so, use this version in the following steps.

2. Update dev date to today in Solution.props. It must follow X.Y.Z-dev.YYMMDD format. If it currently is not a -dev version, increment the patch number or use the branch's version, and add the -dev.YYMMDD part. Examples:
  - 0.1.0-beta -> 0.1.1-dev.YYMMDD
  - 0.1.0-dev -> 0.1.0-dev.YYMMDD
  - 0.1.0 -> 0.1.1-dev.YYMMDD
  - 0.1.0-dev.250101-> 0.1.0-dev.YYMMDD

3. Update the version badge in README.md. Follow the logic in .github/actions/update-badges/action.yml. Update also the color accordingly, specified in the update-badges action.

4. Update the status badge in README.md, according to the version, following the instructions in the update-badges action.

5. Ensure the top section in CHANGELOG.md is [Unreleased].