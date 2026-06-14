# Grasshopper Components

Grasshopper-specific components for manipulating, validating, and managing Grasshopper definitions and patches.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

These components provide programmatic access to definition manipulation, patches, and validation within Grasshopper. They enable batch operations, version control workflows, and collaborative editing of Grasshopper definitions.

**You should read this if you:**

- Need to manipulate Grasshopper definitions programmatically
- Want to work with patches and version control
- Need to validate definition integrity

---

## End-User Guide

### Component Table

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

---

## Developer Reference

Components in this namespace derive from `StatefulComponentBase` and operate on Grasshopper document objects. Here is a pattern for retrieving components from a definition:

```csharp
public class GhGetComponents : StatefulComponentBase
{
    public GhGetComponents()
        : base("Gh Get", "Gh Get",
               "Retrieves components and data from Grasshopper definitions",
               "SmartHopper", "Definition")
    {
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Path", "P", "File path to Grasshopper definition", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddGenericParameter("Components", "C", "Retrieved components", GH_ParamAccess.list);
    }

    protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
    {
        return new Worker(this, AddRuntimeMessage);
    }

    private sealed class Worker : AsyncWorkerBase
    {
        private readonly GhGetComponents _component;
        private string _path;
        private List<IGH_Component> _components;

        public Worker(GhGetComponents component, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(component, addRuntimeMessage)
        {
            _component = component;
            _components = new List<IGH_Component>();
        }

        public override void GatherInputData()
        {
            _path = _component.GetInputData("Path", string.Empty);
        }

        public override async Task DoWorkAsync(CancellationToken token)
        {
            var doc = new GH_Document();
            if (!doc.Open(_path))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to open definition.");
                return;
            }
            _components = doc.Objects.OfType<IGH_Component>().ToList();
            await Task.Yield();
        }

        public override void SetOutputData()
        {
            _component.SetOutputData("Components", _components);
        }
    }
}

```

Applying a patch to a definition uses a diff-and-merge pattern:

```csharp
public class GhPatchApplyComponents : StatefulComponentBase
{
    protected override async Task DoWorkAsync(CancellationToken token)
    {
        var originalPath = GetInputData("Original", string.Empty);
        var patchPath = GetInputData("Patch", string.Empty);

        var originalDoc = new GH_Document();
        originalDoc.Open(originalPath);

        var patch = Patch.Load(patchPath);
        patch.Apply(originalDoc);

        SetOutputData("Result", originalDoc);
        await Task.Yield();
    }
}

```

---

## Architecture & Design

- Grasshopper components provide programmatic access to definition manipulation
- Patch system enables version control and collaborative editing of definitions
- Components support data tree processing for batch operations on multiple definitions
- JSON and patch formats enable serialization and interchange with external tools
- Validation components ensure definition integrity before and after modifications
