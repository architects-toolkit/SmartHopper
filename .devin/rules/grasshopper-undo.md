---
trigger: model_decision
description: Registering undo events so that the user can later ctrl+z an action. Check this rule when writing code that makes changes to the canvas (adding, moving, changing, or deleting components or connections)
---

# Grasshopper Undo Rule

## Purpose
Ensure that any code which mutates Grasshopper canvas objects (moving, adding, deleting, wiring, etc.) records an undo event so users can undo with Ctrl+Z.

## Rule
- **Before** mutating any `IGH_DocumentObject` (e.g. setting `Attributes.Pivot`, adding/removing objects, changing wires), insert:
  ```csharp
  obj.RecordUndoEvent("[SH] <Action Description>");
  ```
- For batch or multi-step operations, create and commit a `GH_UndoRecord`:
  ```csharp
  using Grasshopper.Kernel.Undo;

  var record = new GH_UndoRecord("[SH] Batch Action");
  // for each object change:
  obj.RecordUndoEvent(record);
  // after all changes:
  doc.UndoUtil.RecordEvent(record);
  ```

## Examples
- MoveInstance
  ```diff
  - var current = obj.Attributes.Pivot;
  + obj.RecordUndoEvent("[SH] Move Instance");
  + var current = obj.Attributes.Pivot;
  ```
- AddObjectToCanvas
  ```diff
  - doc.AddObject(obj, false);
  + obj.RecordUndoEvent("[SH] Add Object");
  + doc.AddObject(obj, false);
  ```

## Enforcement
Code reviews or static analysis should flag any GH canvas mutations missing a preceding RecordUndoEvent or undo-util usage.