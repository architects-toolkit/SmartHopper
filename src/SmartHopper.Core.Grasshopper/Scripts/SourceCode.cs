/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Portions of this code adapted from:
 * https://github.com/4kk11/GHScriptGPT
 * MIT License
 * Copyright (c) 2023 Akihito Yokota
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHopper.Core.Grasshopper.Scripts
{
    public class SourceCode
    {
        public string CodeText { get; private set; }
        public IEnumerable<string> CodeLines { get; private set; }
        public SourceCode(string codeText)
        {
            var lines = codeText.Split('\n');
            CodeText = codeText;
            CodeLines = lines;
        }

        public SourceCode(IEnumerable<string> codeLines)
        {
            var text = string.Join("\n", codeLines);
            CodeText = text;
            CodeLines = codeLines;
        }

        public SourceCode ExtractFunctionCode(string functionStart)
        {
            string text = CodeText;
            int functionStartIndex = text.IndexOf(functionStart);

            if (functionStartIndex == -1)
            {
                throw new Exception("Not found function start");
            }

            int openBraces = 0;
            bool codeBlockStarted = false;
            StringBuilder extracted = new StringBuilder();

            for (int i = functionStartIndex; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    openBraces++;
                    if (!codeBlockStarted)
                    {
                        codeBlockStarted = true;
                        continue;  // Skip appending the first opening brace
                    }
                }
                else if (text[i] == '}')
                {
                    openBraces--;
                    if (openBraces == 0)
                    {
                        break;  // Stop processing after closing the outermost brace
                    }
                }

                if (codeBlockStarted)
                {
                    extracted.Append(text[i]);
                }
            }

            // Remove the closing brace and trim the result
            string result = extracted.ToString().TrimEnd('}');
            return new SourceCode(result);
        }


    }

}
