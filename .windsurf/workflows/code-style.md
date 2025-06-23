---
description: Clean code style
---

1. Ask the user which files they want to clean the code style, if not provided.

2. With the given list of files, perform de following cleaning in *.cs files:
  - Add docstrings for all elements
  - When using `/// <inheritdoc/>`, ensure the parent has docstring
  - There must be a blank line between elements
  - Code should not contain multiple whitespace characters in a row, such as:
      Min       = 1, -> Min = 1,
  - Use trailing comma in multi-line initializers
  - Single-line comment must be preceded by blank line
  - Remove multiple consecutive blank lines
  - Closing brace should be followed by blank line
  - Add omitted braces in if, for, while, try...
  - Using statements should be sorted alphabetically, keeping the system ones at the top
  - The heading of the file should be as follows, replacing YYYY with the creation year of the file:
/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) YYYY Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */