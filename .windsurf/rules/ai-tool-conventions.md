---
trigger: glob
globs: **/SmartHopper.Core.Grasshopper/Tools/*.cs
---

# AI Tool conventions
- Implement IAIToolProvider in SmartHopper.Core.Grasshopper.Tools
- File name: *Tools.cs
- Define AITool metadata (Name, Description, schema)
- Auto-discover via AIToolManager
- Structure the file in (1) tool registration via GetTools(), (2) a region for each tool-specific method