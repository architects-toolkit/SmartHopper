---
trigger: glob
globs: **/SmartHopper.Core.Grasshopper/Tools/*.cs
---

# AI Tool conventions
- Implement IAIToolProvider in SmartHopper.Core.Grasshopper.AITools
- File name: scope_action.cs
- Define AITool metadata (Name, Description, schema)
- Auto-discover via AIToolManager
- Structure the file in (1) tool registration via GetTools(), (2) a region for specific tool methods
- Optionally, add an ScopeTool.cs file with general tools for that scope