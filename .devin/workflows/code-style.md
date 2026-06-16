---
description: Clean C# code style based on project rules
auto_execution_mode: 1
---

1. Ask the user which files they want to clean if no file list is provided.

2. For the selected `*.cs` files, apply the repository style:
  - Add XML docstrings to members when missing. Use `/// <inheritdoc/>` only when the parent/interface member has accurate documentation.
  - Ensure 4-space indentation and remove repeated whitespace that is not meaningful alignment.
  - Keep one blank line between members and remove multiple consecutive blank lines.
  - Use braces for `if`, `for`, `foreach`, `while`, `try`, and similar blocks.
  - Use trailing commas in multi-line initializers.
  - Prefix instance member access with `this.` where appropriate.
  - Sort using directives alphabetically, with `System` namespaces first.
  - Preserve or add the standard file header, replacing `YYYY` with the file creation year or range:
/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) YYYY Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

3. Do not perform broad refactors during a style cleanup unless the user explicitly asks for them.
