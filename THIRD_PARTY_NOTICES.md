# Third-Party Notices

SmartHopper incorporates or is derived in part from third-party software. The
components listed below are provided under their own license terms, reproduced
here in accordance with those terms.

## Cordyceps

The MCP server surface under `src/SmartHopper.Infrastructure/Mcp/` structurally
adapts patterns from the Cordyceps project (HTTP/JSON-RPC transport skeleton,
MCP method dispatch shape, and the tool-discovery pattern). No Cordyceps source
code is copied verbatim; the SmartHopper implementation bridges the existing
`AIToolManager` catalog rather than Cordyceps' own tool registry.

- Project: Cordyceps
- Source: https://github.com/brookstalley/cordyceps
- Copyright (c) 2026 Brooks Talley
- License: MIT

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
