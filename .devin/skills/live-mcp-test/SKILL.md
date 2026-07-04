---
name: live-mcp-test
description: Test the SmartHopper MCP server with live Grasshopper components
triggers:
  - user
---

You have access to the SmartHopper and GhJson .NET repos (open repos in this workspace).

You also have access to the running SmartHopper plugin and Grasshopper/Rhino environment via SmartHopper MCP.

Using the MCP SmartHopper server, retrieve all components with errors in the current grasshopper document (via gh_get_errors).

If possible, suggest a fix either in Grasshopper file (by modifying the canvas with MCP tools) or determine the root cause and suggest a fix directly in SmartHopper or GhJson.NET code.

While using mcp tools, if you identify issues in SmartHopper or GhJson .NET code:
- Report the tool call you made
- Report the received response
- Report the expected response
- Suggest a fix for the root cause of the issue

Do not commit or stage changes. I'll do it manually.

Remember that, after making changes in code, I have to manually turn the program off, build the solution again and trigger the debugging environment. Notify me when you need this.
