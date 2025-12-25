# Data-tree processing schema

## 1. Goals

- **Centralize data-tree mechanics** (paths, matching, grouping, grafting) in `DataTreeProcessor`.
- **Keep component code focused** on UI, parameter definitions, and high-level "what to do".
- **Let workers focus** on async orchestration and data preparation for the function/AI tool.
- **Preserve existing features**: `onlyMatchingPaths`, `groupIdenticalBranches`, `NormalizeBranchLengths`.
- **Add explicit support for per-item grafted output paths**: `[q0,q1,q2](i)` → outputs under `[q0,q1,q2,i]`.
- **Unify the workflow** across components like `AITextGenerate`, `AIListFilter`, `AIListEvaluate`, `McNeelForumSearchComponent`, `McNeelForumTopicRelatedComponent`, `AIImgGenerateComponent`, `WebPageReadComponent`, `AIModelsComponent`.

This schema is now the **single** processing model: legacy branch/item helpers and `RunFunctionAsync` have been replaced by a unified, topology-driven API.

---

## 2. High‑level workflow and responsibilities

### 2.1 Component (GH_Component / StatefulComponentBase)

- **UI & contract**
  - Register input/output parameters and access (item/list/tree).
  - Expose options to the user (toggles, enums) that map to **processing topology**.
- **Declare processing intent**
  - Chooses a `ProcessingTopology` that defines both granularity and path behavior, for example:
    - `ItemToItem` for per‑item processing that stays in the same path/index (e.g. `AITextGenerate`, `WebPageReadComponent`, basic `AIImgGenerateComponent`).
    - `ItemGraft` for per‑item processing where each item creates its own branch (e.g. `McNeelForumSearchComponent`, `McNeelForumTopicRelatedComponent`).
    - `BranchToBranch` for treating each branch as a single logical unit and keeping its path (e.g. `AIListEvaluate`, `AIListFilter`).
    - `BranchFlatten` for treating each branch as a single logical unit but aggregating results into a flat list.
- **Delegates work**
  - Creates and configures a worker (`CreateWorker`).
  - Delegates execution to worker; does **not** manually iterate branches/items or touch `GH_Structure` paths.
- **Metrics & persistence**
  - Uses base class APIs (`SetDataCount`, `InitializeProgress`, `UpdateProgress`, `SetPersistentOutput`) driven by the runner APIs.

### 2.2 Worker (AsyncWorkerBase subclasses)

- **Async orchestration**
  - Implements `GatherInput` to read data from `DA` into `GH_Structure` variables / dictionaries.
  - Implements `DoWorkAsync` to call the processing runner with a per‑item/branch function.
  - Implements `SetOutput` to write back to `DA` (using `SetPersistentOutput` for persistence).
- **Data preparation**
  - Responsible for **semantic transformations** before calling the function/AI tool:
    - E.g. in `AIListEvaluate` / `AIListFilter`, the branch list is converted to a single JSON string (`AIResponseParser.ConcatenateItemsToJson`), treating the entire list as **one logical item**.
    - In other components, normalizes multiple input trees so that per‑item semantics are clear.
  - Chooses which input trees participate and how they are combined.
- **Delegation to DataTreeProcessor**
  - Does **not** implement path logic or branch matching manually.
  - Calls into a runner that uses `DataTreeProcessor` to:
    - Build the processing plan (`onlyMatchingPaths`, `groupIdenticalBranches`).
    - Enumerate branches/items in the desired order.
    - Select output path mode (same path, grafted, etc.).
- **Function invocation**
  - Provides a per‑branch or per‑item function delegate that encapsulates the **tool call / model call** and post‑processing of that result.
  - That function is called once per **logical unit** (branch or item) by the runner.

### 2.3 DataTreeProcessor

- **Single source of truth for data-tree mechanics**
  - Builds processing plans from input trees.
  - Handles path matching and grouping:
    - `onlyMatchingPaths`: restricts to paths that exist in all required trees.
    - `groupIdenticalBranches`: merges branches with identical paths.
  - Handles **branch length normalization** via `NormalizeBranchLengths`.
  - Derives **output path strategies** (item-to-item, item-graft, branch-flatten, branch-to-branch) from `ProcessingTopology`.
- **No knowledge of UI or AI**
  - Does not know about Grasshopper component UI, prompts, forums, models, etc.
  - Works only with `GH_Structure<T>`, `GH_Path`, and delegates.

### 2.4 Function / AI tool call

- **Pure business logic per logical unit**
  - Receives prepared, semantically meaningful input:
    - A "single logical item" for branch‑based components.
    - A "single item inside a branch" for item‑based components.
  - Performs the AI/tool call and returns **plain outputs** (e.g. `string`, `JObject`, images) or wrapped `IGH_Goo`.
- **No data-tree or path handling**
  - Does not know about `GH_Path`, branches, or grafting.
  - Returns results for **one logical invocation** only; fan‑out into branches is a responsibility of DataTreeProcessor and the runner.

---

## 3. Core concepts in the schema

### 3.1 Processing granularity

We distinguish **how we schedule work**:

- **Branch mode**:
  - One function call per **branch**.
  - Typical for components that treat a list/tree as a whole: `AIListEvaluate`, `AIListFilter`.
- **Item mode**:
  - One function call per **item within a branch**.
  - Typical for `AITextGenerate`, `WebPageReadComponent`, `AIImgGenerateComponent`.

In both cases, the job of `DataTreeProcessor` is to:

- Choose which paths/branches to process (`onlyMatchingPaths`, `groupIdenticalBranches`).
- Optionally normalize branch lengths before per‑item enumeration.
- Produce a **schedule** of logical units (branches or items).

### 3.2 Processing topology (item/branch matrix)

Components define **how input units map to output paths** via a `ProcessingTopology`. This is a 2×2 matrix on top of the existing branch/item granularity, with four canonical modes:

- **item-to-item**
  - Example mappings:
    - `[q0, q1, q2](0) → [q0, q1, q2](0)`
    - `[q0, q1, q3](1) → [q0, q1, q3](1)`
  - Input unit: single item inside a branch.
  - Output branch: **same path** as the item’s input branch; same item index.
  - Implementation: item-level schedule + "same branch" output.
  - Typical for: `AITextGenerate`, `WebPageReadComponent`, basic `AIImgGenerateComponent`.

- **item-graft**
  - Example mappings:
    - `[q0, q1, q2](0) → [q0, q1, q2, 0](0..N)`
    - `[q0, q1, q3](1) → [q0, q1, q3, 1](0..N)`
  - Input unit: single item inside a branch.
  - Output branch: **new branch per input item**, by appending the item index to the path.
  - Implementation: item-level schedule + implicit graft.
  - Typical for one-returns-many per item, e.g. `McNeelForumSearchComponent`, `McNeelForumTopicRelatedComponent`, or grafted image/list generators.

- **branch-flatten**
  - Example mappings:
    - `[q0, q1, q2](0..N) → [0](0)`
    - `[q0, q1, q3](0..N) → [0](1)`
  - Input unit: whole branch (0..N items).
  - Output branch: **single shared flat path**, each input branch becomes one item/index in `[0]`.
  - Implementation: branch-level schedule + implicit flatten.
  - Typical for components that summarise or aggregate each branch into a single result and then present those as a flat list.

- **branch-to-branch**
  - Example mappings:
    - `[q0, q1, q2](0..N) → [q0, q1, q2](0..N)`
    - `[q0, q1, q3](0..N) → [q0, q1, q3](0..N)`
  - Input unit: whole branch.
  - Output branch: **same path** as input branch.
  - Implementation: branch-level schedule + same-branch output.
  - Typical for list-as-whole components like `AIListEvaluate`, `AIListFilter` and for many tree-preserving operations.

Internally, this topology determines both:

- The **schedule type** (branch vs item) used by the runner.
- The **path behavior** (same branch, graft, flatten) owned by `DataTreeProcessor`.

### 3.3 Item/branch units and schedule

To support item‑centric processing and grafting, the runner builds an internal schedule of **processing units**:

- `ProcessingUnit<T>` (internal struct in `DataTreeProcessor`)
  - `GH_Path InputPath` – original branch path.
  - `int? ItemIndex` – index within the branch for item mode; `null` for branch mode.
  - `IReadOnlyList<GH_Path> TargetPaths` – one or more output paths mapped from this input path.

`DataTreeProcessor` builds:

- A **plan**: list of entries mapping primary paths to target paths.
- A **schedule**:
  - For item mode: a flattened enumeration of `ProcessingUnit<T>` across all relevant items in all branches.
  - For branch mode: one `ProcessingUnit<T>` per primary path/branch.

The runner then:

- Invokes the per‑item or per‑branch function for each `ProcessingUnit<T>`.
- Computes the **output path** using the selected `ProcessingTopology` and the unit context.
- Appends the returned values to the appropriate output trees.

The **delegate function is intentionally path‑unaware**:

- **No `GH_Path` or index parameters** are exposed to the delegate. It only receives a `Dictionary<string, List<T>>` that represents the logical unit being processed.
- The delegate is responsible **only for performing the action** (e.g. AI/tool call, transformation) and returning plain outputs.
- All responsibilities related to **paths, indices, grafting, flattening, and fan‑out** stay in the runner / `DataTreeProcessor` layer above the delegate.

For **one‑to‑many per item** (`ItemGraft`):

- Conceptual mapping: `[q0, q1, q2](0) → [q0, q1, q2, 0](0..N)`.
- Runner behaviour:
  - Treats each input item as one logical unit in the **item schedule**.
  - Calls the delegate once per item, with per‑key lists of length 1.
  - Interprets the delegate’s returned lists as the **fan‑out** for that item.
  - Appends those outputs under a **grafted branch** derived from the original path and item index, e.g. `[q0,q1,q2,0]`.

---

## 4. Preserved features

### 4.1 Flat tree broadcasting (single-path trees)

- **Critical behavior**: When an input tree has only a single path `{0}` (flat tree), it should be **applied to all other paths** without its own path appearing in outputs.
- **Implementation** (already exists in `DataTreeProcessor.GetProcessingPaths`):
  - After computing `allPaths` from all input trees, if `allPaths.Count > 1`:
    - Identify trees with only a single path (typically `{0}`).
    - Remove those single-path tree paths from `allPaths`.
  - Result: The flat tree's data is broadcast to all structured paths, but `{0}` doesn't appear as an output path.
- **Example**:
  - Input A: `{0}` → "Apple" (flat tree)
  - Input B: `{0;0}` → "keep", `{0;1}` → "only", `{0;2}` → "colors"
  - **Output paths**: `{0;0}`, `{0;1}`, `{0;2}` (NOT `{0}`)
  - Each output branch receives A's value "Apple" combined with B's respective values.
- **Where preserved**: `BuildProcessingPlan` → `GetProcessingPaths` → removes single-path tree paths from `allPaths` before building the plan.

### 4.2 onlyMatchingPaths

- Behavior is preserved at the **plan construction** level.
- Before building the item schedule, `DataTreeProcessor`:
  - Computes the set of paths in each input tree.
  - Restricts processing to paths that appear in **all required trees** when `onlyMatchingPaths = true`.
- Both branch and item modes, including grafted outputs, operate **on top of this filtered set** of primary paths.

### 4.3 groupIdenticalBranches

- Still applied **before scheduling units**.
- When multiple trees (or different sources) share identical branch paths, the plan maps them to a single primary path with multiple target paths.
- For item mode:
  - The item schedule is built per **primary path**.
  - Results are then replicated/assigned to all **target paths** derived from that primary path.
- Output path modes (same vs graft) are evaluated per **target path**.

### 4.4 NormalizeBranchLengths

- Remains a `DataTreeProcessor` utility used at the **worker preparation** level.
- Typical usage pattern:
  - Worker obtains per‑path branches for all relevant trees.
  - Calls `NormalizeBranchLengths` on the branch lists (e.g. prompts, instructions, limits).
  - Produces aligned lists of equal length.
- In the schema:
  - This normalization is still called from the worker when required (e.g. `AITextGenerate`, `AITextEvaluate`, `AIImgGenerateComponent`).
  - After normalization, the branches are passed to the per‑item function or integrated into the item schedule.

---

## 5. Runner APIs

### 5.1 Topology-driven runner API

Instead of exposing separate branch/item runners or explicit path modes, the runner accepts a **ProcessingTopology** via `ProcessingOptions` and infers both scheduling and path behavior from it. A *single* processing function delegate is used for both item- and branch-oriented topologies.

```csharp
public enum ProcessingTopology
{
    ItemToItem,
    ItemGraft,
    BranchFlatten,
    BranchToBranch,
}

public sealed class ProcessingOptions
{
    public ProcessingTopology Topology { get; set; }

    public bool OnlyMatchingPaths { get; set; }

    public bool GroupIdenticalBranches { get; set; }

    // Additional knobs can be added here as needed
}
```

Workers/components choose a topology through `ProcessingOptions.Topology`, and the runner derives the appropriate schedule internally.

#### Unified RunAsync

```csharp
Task<Dictionary<string, GH_Structure<U>>> RunAsync<T, U>(
    Dictionary<string, GH_Structure<T>> inputTrees,
    Func<Dictionary<string, List<T>>, Task<Dictionary<string, List<U>>>> function,
    ProcessingOptions options,
    Action<int, int> progressCallback = null,
    CancellationToken token = default)
```

- Invokes `function` once per logical unit (branch or item).
- Computes final output paths using the topology (same branch vs shared flat path vs grafted).

Responsibilities:

- `DataTreeProcessor` builds the processing plan (paths, matching, grouping) using `ProcessingOptions`.
- For item topologies, it builds an item schedule of `ProcessingUnit<T>`; for branch topologies, a branch schedule.
- For each logical unit (branch or item):
  - Calls the unified `function` with a `Dictionary<string, List<T>>` representing either a single item (lists of length 1) or a full branch (lists of length N).
  - Computes the final output path according to topology (same branch, grafted per item, or flattened).
  - Appends outputs to the corresponding `GH_Structure<U>`.
- Calls back into the component base to update **metrics and progress**:
  - `data_count` = number of logical units (branches/items).
  - `iterations_count` = number of function invocations.
  - Progress messages use the convention **`current/total`**, where:
    - `current` is the index of the currently processed logical unit (1‑based).
    - `total` is the total number of logical units that will be processed, computed from the input trees after applying matching, grouping, and any normalization required by the selected topology.

### 5.2 Relationship to StatefulComponentBase

- `StatefulComponentBase` exposes a high‑level helper

```csharp
protected Task<Dictionary<string, GH_Structure<U>>> RunProcessingAsync<T, U>(
    Dictionary<string, GH_Structure<T>> trees,
    Func<Dictionary<string, List<T>>, Task<Dictionary<string, List<U>>>> function,
    ProcessingOptions options,
    CancellationToken token = default)
```

which builds a processing plan, computes metrics, initialises progress, and then forwards to `DataTreeProcessor.RunAsync`.

- Legacy overloads of `RunProcessingAsync` using `onlyMatchingPaths` / `groupIdenticalBranches` flags and `ProcessingUnitMode` have been removed in favour of this unified API.
- Workers should use this **base class helper** instead of calling `DataTreeProcessor` directly, so that:
  - Metrics and progress are centrally managed.
  - Cancellation and error handling remain consistent.

---

## 6. Component mapping

This section lists all components under `src/SmartHopper.Components` that are based on
`StatefulComponentBase`, `AIStatefulAsyncComponentBase` or
`AISelectingStatefulAsyncComponentBase`, and describes how they fit into the
processing schema.

### 6.1 Text components

- **AITextGenerate** (`AIStatefulAsyncComponentBase`)
  - **Topology**: `ItemToItem`.
  - **Granularity**: per‑item within each branch.
  - **Path mode**: same as input branch and item index.
  - **Changes required**:
    - Move implicit per‑item loop into the runner by using `ProcessingTopology.ItemToItem`.
    - Keep worker focused on:
      - Reading `Prompt` / `Instructions` trees.
      - Using `NormalizeBranchLengths` where needed.
      - Defining a per‑item delegate that:
        - Receives a single prompt/instructions pair.
        - Calls `text_generate` and returns one result item.

- **AITextEvaluate** (`AIStatefulAsyncComponentBase`)
  - **Topology**: `ItemToItem`. Each item in the list returns a True/False value in the output.
  - **Changes required**:
    - Refactor worker to use the new runner instead of manual iteration, following the same pattern as `AITextGenerate` / `AIList*`.

- **AITextListGenerate** (`AIStatefulAsyncComponentBase`)
  - **Topology**: `ItemGraft`.
  - **Changes required**:
    - Shift path logic and any ad‑hoc grafting into the runner.
    - Keep worker responsible only for preparing input arrays and parsing the AI result list.

### 6.2 List components

- **AIListEvaluate** (`AIStatefulAsyncComponentBase`)
  - **Topology**: `BranchToBranch`.
  - **Granularity**: branch‑as‑single logical item.
  - **Path mode**: same as input branch.
  - **Changes required**:
    - Use `ProcessingTopology.BranchToBranch` with branch‑level delegate.
    - Worker continues to:
      - Convert list branch to JSON (`AIResponseParser.ConcatenateItemsToJson`).
      - Prepare question tree and call the AI tool once per branch.
    - Runner handles path replication and metrics.

- **AIListFilter** (`AIStatefulAsyncComponentBase`)
  - **Topology**: `BranchToBranch`.
  - **Granularity**: branch‑as‑single logical item.
  - **Path mode**: same as input branch.
  - **Changes required**:
    - Same pattern as `AIListEvaluate`:
      - Delegate remains branch‑based.
      - Runner owns path replication and progress.

### 6.3 Image components

- **AIImgGenerateComponent** (`AIStatefulAsyncComponentBase`)
  - **Topology**: `ItemGraft`.
  - **Granularity**: per‑item (each prompt is one logical unit).
  - **Path mode**: `ItemGraft` to move each prompt’s results to `[q0,q1,q2,i]`.
  - **Changes required**:
    - Keep worker responsible for:
      - Getting `Prompt`, `Size`, `Quality`, `Style` trees.
      - Calling `NormalizeBranchLengths`.
      - Defining a per‑item delegate that returns one or many images and revised prompts.
    - Let the runner decide whether to keep or graft paths based on topology.

- **ImageViewerComponent** (`GH_Component`) — schema not applicable.

### 6.4 AI / models / context components

- **AIChatComponent** (`AIStatefulAsyncComponentBase`) — schema not applicable.

- **AIFileContextComponent** (`GH_Component`) — schema not applicable.

- **AIModelsComponent** (`AIProviderComponentBase`) — schema not applicable.

### 6.5 Knowledge components

- **WebPageReadComponent** (`StatefulComponentBase`)
  - **Topology**: `ItemToItem`.
  - **Granularity**: per‑item (each URL is one logical unit).
  - **Path mode**: same as input branch and item index.
  - **Changes required**:
    - Use `ProcessingTopology.ItemToItem` with an item‑level delegate that:
      - Receives a single URL.
      - Calls `web_generic_page_read`.
      - Returns one `Content` string.
    - Remove explicit loops over URLs in the worker; let the runner drive iteration.

- **AIMcNeelForumPostSummarizeComponent**, **AIMcNeelForumTopicSummarizeComponent** (`AIStatefulAsyncComponentBase`)
  - **Topology**: `ItemToItem`.
  - **Changes required**:
    - Refactor workers to use `ProcessingTopology.ItemToItem` with item-level delegates.
    - Keep branch semantics simple: each input item maps to one output summary.

- **McNeelForumDeconstructPostComponent** (`GH_Component`) — schema not applicable.

- **McNeelForumPostGetComponent**, **McNeelForumPostOpenComponent** (`StatefulComponentBase`)
  - **Topology**: `ItemToItem` (each ID or URL is one logical unit), but with side‑effects (opening posts).
  - **Changes required**:
    - For pure data retrieval, using `ItemToItem` is natural.
    - For side‑effect‑heavy operations (like opening posts in a browser), centralizing scheduling brings less value; migration is optional.

- **McNeelForumSearchComponent**, **McNeelForumTopicRelatedComponent** (`StatefulComponentBase`)
  - **Topology**: `ItemGraft`.
  - **Granularity**: per‑item (each query or topic ID is one logical unit).
  - **Path mode**: graft per input item (`[q0,q1,q2](i) → [q0,q1,q2,i](0..N)`).
  - **Changes required**:
    - Workers define per‑item delegates that:
      - Receive a single query/topic ID.
      - Call the corresponding web/AI tool.
      - Return a list of results.
    - Runner handles grafted output paths and fan‑out.

### 6.6 Script components

- **AIScriptGeneratorComponent**, **AIScriptReviewComponent** (`AISelectingStatefulAsyncComponentBase`) — do not implement for now.
  - **Topology**: mix of branch‑ and item‑oriented behaviour depending on how code and parameters are represented.
  - **Recommendation**:
    - These components orchestrate script generation/review, often with richer UI and selection logic.
    - The schema can help for batched script operations, but migration is optional and should be evaluated separately.

### 6.7 Misc / metrics components

- **DeconstructMetricsComponent** (`GH_Component`) — schema not applicable.

---

## 7. Separation of concerns summary

- **Component**
  - Owns UI, parameter definitions, and exposes options that map to `ProcessingTopology`.
  - Chooses a **topology** (item-to-item, item-graft, branch-flatten, branch-to-branch) but does not implement loops or tree mechanics.

- **Worker**
  - Reads inputs and prepares data into semantic units (branches or items).
  - Chooses which trees participate and how (e.g. list‑as‑single‑item).
  - Defines per‑branch or per‑item function delegating to tools/models.
  - Uses base class runners so metrics and progress are handled centrally.

- **DataTreeProcessor**
  - Builds processing plans: paths, matching, grouping.
  - Enumerates branches/items and computes target output paths (including grafted ones).
  - Preserves `onlyMatchingPaths`, `groupIdenticalBranches`, `NormalizeBranchLengths` semantics.

- **Function / AI tool call**
  - Pure, stateless logic per logical input unit.
  - No knowledge of data trees or paths.

This schema keeps path and data-tree concerns **out of component implementations**, while still letting each component choose its desired behavior (branch vs item, same path vs grafted). It also supports the new per‑item grafted output paths in a consistent way across components.
