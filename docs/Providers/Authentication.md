# Request‑Scoped Authentication for Providers

Authentication is configured per request for behavior, while API keys are resolved internally by providers.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/Authentication/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document describes how SmartHopper handles authentication across providers in a secure, request-scoped way. It explains the separation between authentication scheme selection and secret application, ensuring secrets never leak through request objects or logs.

**You should read this if you:**

- Are implementing a new provider and need to understand the authentication lifecycle
- Want to customize HTTP headers or auth schemes per request
- Need to migrate from legacy `CustomizeHttpClientHeaders` overrides
- Are auditing security to ensure API keys do not flow through request objects

---

## End-User Guide

### Overview

Authentication is configured per request for behavior, while API keys are resolved internally by providers. Providers set the auth scheme and any non‑secret headers in `PreCall(...)`. The base `AIProvider.CallApi(...)` and `AIProviderStreamingAdapter` apply authentication just‑in‑time using provider‑internal API keys. Extra non‑secret headers are applied via the shared `HttpHeadersHelper.ApplyExtraHeaders(...)`. Secrets must NOT be placed on `AIRequestCall`.

Benefits:

- Centralized, flexible, and testable auth handling
- Prevents secrets from flowing through request objects or logs
- Works uniformly for non‑streaming and streaming calls

### Request Properties

- `AIRequestCall.Authentication` (string): Auth scheme.
  - Supported: `"none"`, `"bearer"`, `"x-api-key"`.
- `AIRequestCall.Headers` (IDictionary<string,string>): Additional HTTP headers (non‑secret). The following are reserved and applied internally: `Authorization`, `x-api-key`.

Validation: when `Authentication == "bearer"` or `Authentication == "x-api-key"`, the provider's internal API key is applied.

### Migration Guide

- Remove overrides of `CustomizeHttpClientHeaders` used for auth.
- In `PreCall(...)`, set `Authentication` and any non‑secret provider‑specific headers. Do not attach API keys to `Headers`.
- Ensure streaming adapters resolve the API key from provider internals and call `ApplyAuthentication(...)`. Use `ApplyExtraHeaders(...)` for the rest.
- Update validation/tests:
  - Assert that no secrets are present on `AIRequestCall` or in logs/returns.

### Notes

- Additional auth schemes can be added; keep scheme selection in `PreCall(...)` and let providers apply secrets internally.
- Do not store secrets in source or pass them through request objects; retrieve from secure storage at runtime and apply only inside provider internals.

---

## Developer Reference

### Provider Pattern (PreCall)

Set auth on the request object. Examples:

**Bearer token:**

```csharp
public override AIRequestCall PreCall(AIRequestCall request)
{
    // Select the scheme. Do NOT attach secrets to the request.
    request.Authentication = "bearer";

    // Optional non‑secret headers
    request.Headers["OpenAI-Beta"] = "assistants=v2";

    return base.PreCall(request);
}

```

**Provider‑applied API key header (e.g., Anthropic x-api-key):**

```csharp
public override AIRequestCall PreCall(AIRequestCall request)
{
    // Select the scheme. The actual API key is applied internally.
    request.Authentication = "x-api-key";

    // Optional non‑secret headers
    request.Headers["anthropic-version"] = "2023-06-01";

    return base.PreCall(request);
}

```

### Non‑Streaming HTTP Calls

`AIProvider.CallApi(...)` now:

- Supports `"none"`, `"bearer"`, and `"x-api-key"` schemes.
- For `"bearer"`, uses the provider's stored API key.
- For `"x-api-key"`, adds the `x-api-key` header using the provider's stored API key.
- Applies `Headers` as additional request headers via `HttpHeadersHelper.ApplyExtraHeaders(...)` (overrides are respected; `Authorization` and `x-api-key` are reserved and applied internally).

Secrets are not exposed on the `AIRequestCall`, `AIReturn`, or logs.

### Streaming Adapters

Derive from `AIProviderStreamingAdapter` and apply auth using provider‑internal keys plus request‑scoped extra headers:

```csharp
var client = CreateHttpClient();
// Resolve API key from provider internals (settings/secure store), not from the request
string apiKey = /* resolve securely from provider internals */ null;
ApplyAuthentication(client, request.Authentication, apiKey);
ApplyExtraHeaders(client, request.Headers); // delegates to HttpHeadersHelper (excludes Authorization and x-api-key)

```

- `ApplyAuthentication(...)` sets `Authorization: Bearer <token>` or `x-api-key: <key>` based on `request.Authentication`.
- `ApplyExtraHeaders(...)` delegates to `HttpHeadersHelper.ApplyExtraHeaders(...)` to add all headers from `request.Headers` except reserved ones (`Authorization`, `x-api-key`).

---

## Architecture & Design

Location:

- Request model: `src/SmartHopper.Infrastructure/AICall/Core/Requests/AIRequestCall.cs`
- Base provider: `src/SmartHopper.Infrastructure/AIProviders/AIProvider.cs`
- Streaming base: `src/SmartHopper.Infrastructure/AIProviders/AIProviderStreamingAdapter.cs`
- Shared headers helper: `src/SmartHopper.Infrastructure/Utils/HttpHeadersHelper.cs`

The authentication architecture follows a clear separation of concerns:

1. **Scheme Selection**: The provider's `PreCall(...)` method selects the authentication scheme and adds non-secret headers to the request.
2. **Secret Resolution**: API keys and tokens are resolved from provider-internal secure storage at runtime.
3. **Just-In-Time Application**: The base `AIProvider.CallApi(...)` or streaming adapter applies the secret immediately before the HTTP call, ensuring it never appears in request logs or serialized request objects.
4. **Shared Helpers**: `HttpHeadersHelper.ApplyExtraHeaders(...)` centralizes header application logic for both streaming and non-streaming paths.

This design ensures that:

- Secrets are isolated from request/response DTOs
- Authentication behavior is testable without real credentials
- Providers can switch schemes without changing the core HTTP infrastructure
