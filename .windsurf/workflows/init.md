---
description: Initialize today's work session
---

1. Update dev date to today in Solution.props. It must follow X.X.X-dev.YYMMDD format. If it currently is not a -dev version, increment the patch number and add the -dev.YYMMDD part. Examples:
  - 0.1.0-beta -> 0.1.1-dev.YYMMDD
  - 0.1.0-dev -> 0.1.0-dev.YYMMDD
  - 0.1.0 -> 0.1.1-dev.YYMMDD
  - 0.1.0-dev.250101-> 0.1.0-dev.YYMMDD

2. Update the version badge in README.md. Follow the logic in .github/actions/update-badges/action.yml.

3. Ensure the top section in CHANGELOG.md is [Unreleased].