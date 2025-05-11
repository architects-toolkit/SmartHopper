---
trigger: glob
globs: **/SmartHopper.Components/*.cs
---

# Component conventions
- Inherit from ComponentBase or derived class (e.g. AIStatefulAsyncComponentBase)
- File name: [Category][Action][Type]Component.cs
- Override RegisterInputParams(), RegisterOutputParams()
- Provide unique Guid, ComponentName, Nickname, Description