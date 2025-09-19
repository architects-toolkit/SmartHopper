# Request‑Scoped Authentication for Providers

Location:

- Request model: `src/SmartHopper.Infrastructure/AICall/Core/Requests/AIRequestCall.cs`
- Base provider: `src/SmartHopper.Infrastructure/AIProviders/AIProvider.cs`
- Streaming base: `src/SmartHopper.Infrastructure/AIProviders/AIProviderStreamingAdapter.cs`
- Shared headers helper: `src/SmartHopper.Infrastructure/Utils/HttpHeadersHelper.cs`

## Overview

Authentication is configured per request for behavior, while API keys are resolved internally by providers. Providers set the auth scheme and any non‑secret headers in `PreCall(...)`. The base `AIProvider.CallApi(...)` and `AIProviderStreamingAdapter` apply authentication just‑in‑time using provider‑internal API keys. Extra non‑secret headers are applied via the shared `HttpHeadersHelper.ApplyExtraHeaders(...)`. Secrets must NOT be placed on `AIRequestCall`.

Benefits:

- Centralized, flexible, and testable auth handling
- Prevents secrets from flowing through request objects or logs
- Works uniformly for non‑streaming and streaming calls

## Request properties

- `AIRequestCall.Authentication` (string): Auth scheme.
  - Supported: `"none"`, `"bearer"`, `"x-api-key"`.
- `AIRequestCall.Headers` (IDictionary<string,string>): Additional HTTP headers (non‑secret). The following are reserved and applied internally: `Authorization`, `x-api-key`.

Validation: when `Authentication == "bearer"` or `Authentication == "x-api-key"`, the provider's internal API key is applied.

## Provider pattern (PreCall)

Set auth on the request object. Examples:

- Bearer token

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

- Provider‑applied API key header (e.g., Anthropic x-api-key)

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

## Non‑streaming HTTP calls

`AIProvider.CallApi(...)` now:

- Supports `"none"`, `"bearer"`, and `"x-api-key"` schemes.
- For `"bearer"`, uses the provider's stored API key.
- For `"x-api-key"`, adds the `x-api-key` header using the provider's stored API key.
- Applies `Headers` as additional request headers via `HttpHeadersHelper.ApplyExtraHeaders(...)` (overrides are respected; `Authorization` and `x-api-key` are reserved and applied internally).

Secrets are not exposed on the `AIRequestCall`, `AIReturn`, or logs.

## Streaming adapters

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

## Migration guide

- Remove overrides of `CustomizeHttpClientHeaders` used for auth.
- In `PreCall(...)`, set `Authentication` and any non‑secret provider‑specific headers. Do not attach API keys to `Headers`.
- Ensure streaming adapters resolve the API key from provider internals and call `ApplyAuthentication(...)`. Use `ApplyExtraHeaders(...)` for the rest.
- Update validation/tests:
  - Assert that no secrets are present on `AIRequestCall` or in logs/returns.

## Notes

- Additional auth schemes can be added; keep scheme selection in `PreCall(...)` and let providers apply secrets internally.
- Do not store secrets in source or pass them through request objects; retrieve from secure storage at runtime and apply only inside provider internals.
