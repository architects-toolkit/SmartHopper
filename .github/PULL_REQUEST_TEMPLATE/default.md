<details>
<summary>Pull Request Title Format Guidelines (Click to expand)</summary>

The Pull Request title should follow this format:
`<type>(<optional-scope>): <description>`

Where:
- `<type>` is one of:
  - `feat`: A new feature
  - `fix`: A bug fix
  - `docs`: Documentation changes
  - `style`: Code style changes (formatting, etc)
  - `refactor`: Code refactoring
  - `perf`: Performance improvements
  - `test`: Adding or modifying tests
  - `build`: Changes to build process or tools
  - `ci`: Changes to CI configuration
  - `chore`: Other changes that don't modify src or test files
  - `revert`: Revert a previous commit
- `<optional-scope>`: Scope of the change (e.g., core, ui, components)
- `<description>`: Short description of the change

Example: `feat(component): add new grasshopper component`
</details>

## Description

Brief description of the changes and the problem it solves.

## Breaking Changes

List any breaking changes here. Mention if there is no breaking change.

## Testing Done

Describe the testing you've done to validate your changes.

## Checklist

- [ ] This PR is focused on a single feature or bug fix
- [ ] Version in Solution.props was updated, if necessary, and follows semantic versioning
- [ ] CHANGELOG.md has been updated
- [ ] PR title follows [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) format
- [ ] PR description follows [Pull Request Description Template](#pull-request-description-template)
