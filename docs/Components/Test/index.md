# DataTreeProcessorEqualPathsTestComponent

Purpose
- Manually validate `DataTreeProcessor.RunFunctionAsync` using two hardcoded integer trees with equal paths.

Component
- Name: Test DataTreeProcessor (Equal Paths)
- Nickname: TEST-DTP-EQ
- Assembly: SmartHopper.Components.Test
- Category: SmartHopper
- Subcategory: Testing Data
- File: `src/SmartHopper.Components.Test/DataProcessor/DataTreeProcessorEqualPathsTestComponent.cs`

Usage (Grasshopper)
1. Drop the component on the canvas (SmartHopper > Testing Data).
2. Toggle the built-in `Run?` input to `true`.
3. Observe outputs:
   - Result (tree<int>): path {0} containing a single value 7.
   - Success (bool): true if the result matches expected value.
   - Messages (list<string>): diagnostic info about inputs and result.

Test Setup (internal)
- Inputs: A = 2 at path {0}, B = 5 at path {0}
- Function: returns `Sum = A + B` per branch
- Expected: Sum = 7 at path {0}

Notes
- Uses internal data; no external inputs.
- `RunOnlyOnInputChanges = false` so it runs when `Run?` is enabled even without input changes.
- Helpful for quick, manual verification of data-tree branch mapping and processing.
