# Canvas & GhJSON Components (`D. GhJSON`)

Components in `src/SmartHopper.Components/Grasshopper/` for reading, placing, tidying, validating, diffing, merging, patching, and saving Grasshopper definitions in GhJSON (`.ghjson`) and GhPatch (`.ghpatch`) formats. All live under the single **`SmartHopper → D. GhJSON`** ribbon panel.

## `D. GhJSON` panel (`SmartHopper → D. GhJSON`)

### Live canvas tools (primary / secondary blocks)

| Component | Display Name | Nickname | Exposure | Purpose |
| --- | --- | --- | --- | --- |
| `GhGetComponents` | Get GhJSON | `GhGet` | primary | Exports the active canvas (or selected/filtered components) to GhJSON (`Names`, `Guids`, `GhJSON`) |
| `GhPutComponents` | Place GhJSON | `GhPut` | primary | Places or updates components on the canvas from a GhJSON string or `.ghjson` file path; preserves supplied pivots, auto-layouts pivotless new components relative to connected fixed neighbors, and updates matching `instanceGuid` objects in place |
| `GhPatchApplyToCanvasComponents` | Apply GhPatch to Canvas | `GhPatchApplyCanvas` | primary + obscure | Applies a `.ghpatch` diff directly to the active canvas after review |
| `AIGhReportComponent` | AI Canvas Report | `AIGhReport` | secondary | Generates a structured Markdown status report of the canvas (`Report`) plus an optional AI `Summary` (`gh_report`) |
| `AIGhConnectComponent` | AI Smart Connect | `AIGhConnect` | secondary | Uses AI (`gh_smart_connect`) to connect selected components on the canvas according to a described `Purpose` |
| `GhTidyUpComponents` | Tidy Up | `GhTidyUp` | secondary | Rearranges selected components into a clean left-to-right grid following their wire connections |

### GhJSON document & file operations (tertiary block)

| Component | Display Name | Nickname | Exposure | Purpose |
| --- | --- | --- | --- | --- |
| `GhPatchApplyComponents` | Apply GhPatch | `GhPatchApply` | tertiary + obscure | Applies a `.ghpatch` diff to a base GhJSON document in memory (`Result`, `Success`, `Conflicts`) |
| `GhDiffComponents` | Diff GhJSON | `GhDiff` | tertiary + obscure | Computes a `.ghpatch` diff between `Left` and `Right` GhJSON documents (`Patch`, `Comp Ops`, `Conn Ops`, `Group Ops`) |
| `GhMergeComponents` | Merge GhJSON | `GhMerge` | tertiary + obscure | Merges `Source` into `Target` GhJSON (`Target` wins on GUID conflicts) |
| `GhValidateComponents` | Validate GhJSON | `GhValidate` | tertiary | Validates a GhJSON string at Minimal (`0`), Standard (`1`), or Strict (`2`) level (`Valid`, `Errors`, `Warnings`, `Info`) |
| `OpenGhJSON` | Open GhJSON | `OpenGhJSON` | tertiary | Reads and validates a `.ghjson` file from disk (`GhJSON`, `Valid`, `Errors`) |
| `SaveGhJSON` | Save GhJSON | `SaveGhJSON` | tertiary | Writes a GhJSON string to a `.ghjson` file on disk (`Saved Path`) |
| `OpenGhPatch` | Open GhPatch | `OpenGhPatch` | tertiary + obscure | Reads and validates a `.ghpatch` file from disk (`GhPatch`, `Valid`, `Errors`) |
| `SaveGhPatch` | Save GhPatch | `SaveGhPatch` | tertiary + obscure | Writes a GhPatch string to a `.ghpatch` file on disk (`Saved Path`) |

### Introspection (quarternary block)

| Component | Display Name | Nickname | Exposure | Purpose |
| --- | --- | --- | --- | --- |
| `GhRetrieveComponents` | Retrieve Components | `GhRetrieveComponents` | quarternary + obscure | Lists installed Grasshopper component types as JSON (`Names`, `Guids`, `JSON`), with optional category filtering |
