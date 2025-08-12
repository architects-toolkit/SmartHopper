---
trigger: glob
globs: **/SmartHopper.Core.Grasshopper/AITools/*.cs
---

# AI Tool conventions
- Check other similar tools to understand how they implement the code
- Implement IAIToolProvider in SmartHopper.Core.Grasshopper.AITools
- File name: scope_action.cs
- Define AITool metadata (Name, Description, schema)
- Auto-discover via AIToolManager
- Structure the file in (1) tool registration via GetTools(), (2) a region for specific tool methods