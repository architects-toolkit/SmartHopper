---
trigger: glob
globs: **/SmartHopper.Providers.*/*.cs
---

# Provider authentication and secrets

Provider authentication is request-scoped for behavior and provider-scoped for secrets.

## Rules

- Do not put API keys, bearer tokens, or secret headers on `AIRequestCall`, `AIRequestCall.Headers`, `AIReturn`, logs, exceptions, docs, or source code.
- In `PreCall(...)`, set `AIRequestCall.Authentication` to the required scheme:
  - `none`
  - `bearer`
  - `x-api-key`
- Add only non-secret provider headers to `AIRequestCall.Headers`.
- Let `AIProvider.CallApi(...)` or provider streaming adapters apply secrets just-in-time from provider settings.
- Mark secret settings with `SettingDescriptor.IsSecret = true`.
- Mask or omit secret setting values in diagnostics.
- For streaming adapters, resolve API keys from provider internals and use shared authentication/header helpers.

See `docs/Providers/Authentication.md`.
