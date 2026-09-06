# SmartHopper GhJSON Guidance

GhJSON and GhPatch are SmartHopper's structured formats for representing and changing Grasshopper definitions.

Use `smarthopper_ghjson_reference` instead of relying on memorized schema details. Available focused topics include:

- `overview`
- `specification`
- `ghpatch`
- `document_structure`
- `components`
- `connections`
- `groups`
- `data_types`
- `component_specific_formats`
- `validation`
- `examples`

Validate generated documents or patches before applying them. Preserve existing instance GUIDs only for intentional replacements, and use `editMode` consistently with whether content is being added or replaced.

## References
- [GhJSON Schema](https://architects-toolkit.github.io/ghjson-spec/schema/v1.0/ghjson.schema.json)
- [GhJSON Format Specification](https://raw.githubusercontent.com/architects-toolkit/ghjson-spec/refs/heads/main/docs/specification.md)
- [GhPatch Schema](https://architects-toolkit.github.io/ghjson-spec/schema/v1.0/ghpatch.schema.json)
- [GhPatch Format Specification](https://raw.githubusercontent.com/architects-toolkit/ghjson-spec/refs/heads/main/docs/ghpatch.md)