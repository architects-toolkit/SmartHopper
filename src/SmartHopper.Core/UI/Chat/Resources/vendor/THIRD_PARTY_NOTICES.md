# Third-Party Notices

This folder contains vendored third-party code shipped inside the SmartHopper
WebView chat UI. Each entry lists the upstream project and its license.

## chat-ui toolkit

- **File**: `chat-ui.iife.js`
- **Project**: `@architects-toolkit/chat-ui` — framework-agnostic,
  transport-agnostic embeddable chat view for AI assistants and agent UIs.
- **Source**: `architects-toolkit/chat-ui` repository, `dist/chat-ui.iife.js`
  (self-contained IIFE bundle exposing `window.ChatUI`).
- **License**: MIT

```text
MIT License

Copyright (c) 2026 architects-toolkit contributors

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

The vendored file is a byte-for-byte copy of the upstream build artifact and is
inlined into the chat HTML page by `ChatResourceManager`. Do not edit it in
place; upgrade it by replacing it with a newer upstream build.
