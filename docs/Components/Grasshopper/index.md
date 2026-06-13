# Grasshopper Components

Grasshopper-specific components for manipulating, validating, and managing Grasshopper definitions and patches.

## Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `GhGetComponents` | Gh Get | Definition | Retrieves components and data from Grasshopper definitions |
| `GhPutComponents` | Gh Put | Definition | Writes components and data to Grasshopper definitions |
| `GhDiffComponents` | Gh Diff | Definition | Computes differences between Grasshopper definitions |
| `GhMergeComponents` | Gh Merge | Definition | Merges multiple Grasshopper definitions |
| `GhPatchApplyComponents` | Gh Patch Apply | Patches | Applies patches to Grasshopper definitions |
| `GhPatchApplyToCanvasComponents` | Gh Patch Apply Canvas | Patches | Applies patches directly to the Grasshopper canvas |
| `GhValidateComponents` | Gh Validate | Validation | Validates Grasshopper definitions and patches |
| `GhRetrieveComponents` | Gh Retrieve | Definition | Retrieves and extracts Grasshopper definition data |
| `GhTidyUpComponents` | Gh Tidy Up | Maintenance | Cleans up and optimizes Grasshopper definitions |
| `OpenGhJSONComponent` | Open Gh JSON | Files | Opens and loads Grasshopper JSON files |
| `SaveGhJSONComponent` | Save Gh JSON | Files | Saves Grasshopper definitions as JSON files |
| `OpenGhPatchComponent` | Open Gh Patch | Files | Opens and loads Grasshopper patch files |
| `SaveGhPatchComponent` | Save Gh Patch | Files | Saves patches as Grasshopper patch files |

## Architecture Notes

- Grasshopper components provide programmatic access to definition manipulation
- Patch system enables version control and collaborative editing of definitions
- Components support data tree processing for batch operations on multiple definitions
- JSON and patch formats enable serialization and interchange with external tools
- Validation components ensure definition integrity before and after modifications
