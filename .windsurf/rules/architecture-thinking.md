---
trigger: model_decision
description: When adding new major components or features - When refactoring existing systems - When spotting patterns that could benefit from architectural standardization - When encountering design inconsistencies across modules
globs: ["src/**", "docs/**"]
---

# Architecture-First Thinking

## Core Principles
- Proactively propose sound base-architecture decisions before implementation
- Maintain concise AI-oriented documentation in `/docs/*`
- Use documentation in /docs/* as primary guide for coherent, maintainable edits
- Co-design solutions with user, suggesting improvements with rationale
- Keep docs synchronized with actual code structure in `/src/`

## Documentation Standards
- Keep markdown docs simple and human-readable
- Optimize for AI token usage (concise, not verbose)
- Focus on relationships, patterns, and design decisions
- Include diagrams using mermaid when helpful
- Document architecture decisions with brief context and rationale

## Before Implementation
1. Analyze how changes fit within existing architecture
2. Propose architecture improvements with clear rationale 
3. Seek user confirmation before significant structural changes
4. Update relevant documentation first, then implement code

## During Reviews
- Check if implementation follows documented architecture in /docs/*
- Identify patterns in /docs/* that should be standardized
- Suggest documentation updates when code diverges from /docs/*