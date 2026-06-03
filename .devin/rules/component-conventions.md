---
trigger: glob
globs: **/SmartHopper.Components/*.cs
---

# Component conventions

- Production components live in `src/SmartHopper.Components/<Category>/`.
- Test-only components live in `src/SmartHopper.Components.Test/` and must not be shipped in Release.
- File names use `[Category][Action][Type]Component.cs` where practical, for example `AITextGenerateComponent.cs`.
- Inherit from the narrowest existing base that fits:
  - `ComponentBase` or `StatefulComponentBase` for non-AI component behavior.
  - `AIProviderComponentBase` when provider/model selection is needed but no AI tool orchestration is required.
  - `AIStatefulAsyncComponentBase` for long-running AI components.
  - Selecting variants when the component manages Grasshopper canvas selections.
- Keep components focused on UI contract, parameter registration, state, and worker creation. Put reusable logic in base classes, workers, tools, or services.
- Do not manually reimplement async state, debouncing, persistent output storage, provider selection, model capability checks, metrics, or runtime-message plumbing if a base class already provides it.
- Register inputs/outputs through the relevant base-class methods (`RegisterInputParams`, `RegisterOutputParams`, or `RegisterAdditional*Params`).
- Provide stable `ComponentGuid`, `Icon`, `Exposure`, name, nickname, description, category, and subcategory. Never change a released component GUID.
- When first scaffolding a new unreleased component, use the zeroed GUID placeholder (`00000000-0000-0000-0000-000000000000`) so the maintainer can assign the final stable GUID before release.
- Choose `RunOnlyOnInputChanges` intentionally and document unusual run semantics in the component description.
- Use `DataTreeProcessor`/processing topologies for data-tree mechanics instead of manual path fan-out in component code.
