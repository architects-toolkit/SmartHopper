---
trigger: glob
globs: src/SmartHopper.Providers.*
---

AI providers must wrap any chain-of-thought output or reasoning summary in `<think></think>` tags so that `ChatResourceManager.CreateMessageHtml` can detect and render reasoning.