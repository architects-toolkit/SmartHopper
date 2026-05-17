---
trigger: always_on
---

# Documentation and changelog

- Keep `/docs` synchronized with architectural and behavioral changes in `src/`.
- Organize `/docs` in meaningful folders. Each folder should have an `index.md` that links to child pages with relative Markdown links.
- Prefer concise, AI-friendly documentation: relationships, contracts, data flows, decisions, and gotchas over long prose.
- Log user-visible, API, architecture, component, provider, tool, workflow, and security changes in `CHANGELOG.md` under `[Unreleased]` using Keep a Changelog sections: Added, Changed, Deprecated, Removed, Fixed, Security.
- Use `.github/PULL_REQUEST_TEMPLATE.md` for PR bodies and Conventional Commit style for PR titles.
- Add XML docstrings to C# members; use `<inheritdoc/>` when a parent/interface member already has accurate documentation.
