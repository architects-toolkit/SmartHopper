# Metrics Components (`A. AI`)

Components in `src/SmartHopper.Components/Misc/` for inspecting and aggregating SmartHopper's JSON usage metrics. They render in the **`SmartHopper → A. AI`** ribbon panel (quarternary exposure block).

## Component catalog (`SmartHopper → A. AI`)

| Component | Display Name | Nickname | Purpose |
| --- | --- | --- | --- |
| `DeconstructMetricsComponent` | Deconstruct SmartHopper Metrics | `DMetrics` | Splits a `Metrics` JSON tree into `AI Provider` (`P`), `AI Model` (`M`), `Finish Reason` (`F`), `Completion Time` (`T`), `Context Usage` (`CU`), `Data Count` (`DC`), `Iterations Count` (`IC`), `Role` (`R`), plus `Quantities` (`Q`) and `Costs` (`C`) as JSON objects for the JSON tools — all preserving branch paths |
| `CombineMetricsComponent` | Combine SmartHopper Metrics | `CMetrics` | Aggregates all `Metrics` JSON items within each branch into a single combined metrics JSON object per branch |
