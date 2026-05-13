# Third-Party Notices

SmartHopper redistributes binaries from several third-party open-source
projects via NuGet, and incorporates small, attributed code excerpts in source
form where noted below. SmartHopper itself is distributed under the GNU
Lesser General Public License v3 (LGPL-3.0); see [`LICENSE`](LICENSE) for the
SmartHopper licence text.

This file is informational and complements (but does not replace) the licence
metadata embedded in each redistributed binary.

## Source code incorporated with attribution

### Cordyceps — `brookstalley/cordyceps`

- Upstream: https://github.com/brookstalley/cordyceps
- Upstream licence: MIT (compatible with LGPL-3.0)
- Files in SmartHopper that derive from Cordyceps:
  - `src/SmartHopper.Core.Grasshopper/Utils/Canvas/ComponentNameAliases.cs` —
    the informal-name alias map is adapted from
    `src/Cordyceps/Core/ComponentRegistry.cs`. The original copyright header
    and MIT licence statement are reproduced in the SmartHopper source file.

#### Cordyceps MIT licence

```
MIT License

Copyright (c) 2026 Brooks Talley

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Redistributed binaries (NuGet)

SmartHopper depends on the following NuGet packages at build/runtime. Each
package retains its own licence; see the package metadata or the linked
project for the canonical text.

- **`GhJSON.Core`**, **`GhJSON.Grasshopper`** — Apache-2.0 (architects-toolkit/ghjson-dotnet).
- **`Newtonsoft.Json`** — MIT.
- **`Grasshopper`** (Rhino 8 SDK) — Robert McNeel & Associates, distributed
  with Rhino 8; only consumed at build time via Rhino's official SDK.
- Provider SDKs (`OpenAI`, `Anthropic`, `MistralAI`, etc.) — see each
  `SmartHopper.Providers.*` project for the package list; all are MIT or
  Apache-2.0 licensed at the time of writing.

If you spot a missing attribution or a licence concern, please open an issue.
