---
name: live-rebuild-test-cycle
description: Stop VS debugging, rebuild the plugin, relaunch Rhino/Grasshopper debug, sign freshly compiled DLLs, resolve the duplicate-DLL prompt, open the test .ghx, and reach a working SmartHopper MCP connection
triggers:
  - user
---

Runbook for a full live-test cycle after code changes. Tools used: `visualstudio` MCP, `windows-mcp`, SmartHopper MCP (`smarthopper`), and local PowerShell scripts under `tools/`.

## Procedure

1. **Stop the running debug session / in-flight compilation** via the `visualstudio` MCP (stop debugging / cancel build if one is running).
2. **Rebuild** with `tools/Build-Solution.ps1 -Testing` — fully automated, signs with the test password (`test`). `-Quiet`/concise flags may be combined with it.
3. **Relaunch debugging** via the `visualstudio` MCP (F5/debug profile that starts Rhino).
4. **IMPORTANT — re-sign after VS rebuild:** Visual Studio typically re-compiles before launching the debug environment, which produces **unsigned** DLLs (it does not run the signing scripts). As soon as VS compilation finishes and *before* Grasshopper loads the plugin, run:
   ```powershell
   tools/Sign-Authenticode.ps1 -SignDebug -Password test
   ```
   (default target is `bin/<SolutionVersion>/Debug`; use `-Sign <path>` or `-SignRelease` for other outputs.)
5. **Duplicate-DLL prompt:** on startup Grasshopper may report duplicate assemblies — the freshly compiled debug DLLs and the package-manager-installed copies. Use `windows-mcp` to choose the **just-compiled** ones (Rhino's dialog prints the assembly date/timestamp — pick the newest). Repeat per assembly if prompted multiple times.
6. **Open the test document:** once Rhino finishes startup, open the testing `.ghx` file (via `windows-mcp`/computer-use, or the SmartHopper MCP if already reachable). The test file should already contain the MCP server component with its enable toggle wired.
7. **Ensure MCP is up:** if the SmartHopper MCP is not reachable, place an MCP component on the canvas and connect a boolean toggle set to `true` to its enable input to start the server, then verify connectivity (e.g. `gh_get_errors` / document read) before running tests.
8. **Run the live test plan** (e.g. the certification checklist in `docs/Reviews/` for the current work).

## Notes

- Never commit `signing.snk`, `signing.pfx`, or the `SmartHopperPublicKey` mutation the build writes into `Directory.Build.props` — leave them unstaged.
- `Build-Solution.ps1 -Testing` already strong-names + Authenticode-signs; the separate `Sign-Authenticode` call is only needed for assemblies produced by the VS pre-debug rebuild.
- If Rhino crashes or hangs on load, check `%APPDATA%/SmartHopper/logs` (or the configured log dir) before retrying.
