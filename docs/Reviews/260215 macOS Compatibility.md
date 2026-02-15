# macOS Compatibility Review

**Date**: February 15, 2026
**Scope**: PR `fix(core): add macOS compatibility for provider loading and URL handling` + Issue #263  
**Branch**: `263-compatibility-with-mac`  
**Author**: Code Review (Cascade)

---

## Executive Summary

This review covers the partially-merged PR fixing macOS compatibility and the remaining open issues from #263. The PR addresses three of five known platform issues. Two remain unfixed (WebChatDialog crash, provider icon WinForms dependency). Additionally, the fixes as described introduce **security regressions** and **incomplete coverage** that should be addressed before merging to a release branch.

**Severity Legend**: CRITICAL = blocks usage or security hole; HIGH = causes crash/deadlock; MEDIUM = degraded functionality; LOW = cosmetic or future concern.

---

## 1. Issue 1: `VerifySignature` crashes on macOS (HIGH)

**Problem:** `ProviderManager.VerifySignature()` calls `X509Certificate.CreateFromSignedFile()`, a Windows-only API. On macOS this throws `PlatformNotSupportedException`, preventing all providers from loading.

**PR Fix:** Wrap the Authenticode block in `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`.

**Review:**

- **Correctness**: The fix is correct — Authenticode is a Windows concept and has no macOS equivalent.
- **Security Concern (CRITICAL)**: Skipping Authenticode on macOS means the **only** remaining verification is strong-name token matching. Strong-name tokens are **not** a security mechanism — they are an identity mechanism. An attacker can strip or re-sign a DLL with a matching public key token if the strong-name key is compromised or if the assembly is delay-signed. On macOS, there is effectively **zero cryptographic verification** of provider DLLs.

**Recommendations:**

1. **Short-term (this PR)**: Accept the platform guard but **log a warning** on non-Windows platforms indicating that Authenticode verification is unavailable and provider trust relies solely on strong-name tokens.
2. **Medium-term**: Investigate macOS code signing verification via `codesign --verify` subprocess call or Apple's `Security.framework` via P/Invoke, to provide equivalent tamper detection.
3. **Long-term**: Consider embedding a SHA-256 hash manifest of known-good provider DLLs, verified at load time regardless of platform.

---

## 2. Issue 2: `BuildFullUrl` produces `file://` URLs on macOS (HIGH)

**Problem:** `Uri.TryCreate(endpoint, UriKind.Absolute, out var abs)` on macOS treats `/chat/completions` as an absolute `file:///chat/completions` URI, causing HTTP requests to be sent to `file://` URLs.

**PR Fix:** Add scheme check: `abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps`.

**Review:**

- **Correctness**: The fix is correct and addresses the root cause.
- **Incomplete Coverage**: There are **two** `BuildFullUrl` methods in `AIProvider.cs`:
  1. `AIProvider<T>.BuildFullUrl()` (line 65-88) — the generic override used by most providers. This is the one that has the bug.
  2. `AIProvider.BuildFullUrl()` (line 820-834) — the base virtual method. This uses `new Uri(this.DefaultServerUrl, endpoint.TrimStart('/'))` which does **not** have the same bug but has a different issue: it doesn't validate the endpoint at all.
  Both methods are called via `AIProviderStreamingAdapter.BuildFullUrl()` which delegates to `this.Provider.BuildFullUrl(endpoint)`. The fix must be applied to the `AIProvider<T>` override (line 72-75).

**Recommendations:**

1. Apply the scheme check to `AIProvider<T>.BuildFullUrl()`.
2. Consider also adding scheme validation to the base `AIProvider.BuildFullUrl()` for defense-in-depth.
3. Add a unit test that verifies `/chat/completions` is correctly resolved against a base URL on all platforms.

---

## 3. Issue 3: `ComponentStateManager` deadlock on macOS (HIGH)

**Problem:** `ProcessTransitionQueue()` fires state transition events (`StateExited`, `StateEntered`, `StateChanged`) while holding `stateLock`. On macOS, Grasshopper's threading model causes event handlers to re-enter methods that also acquire `stateLock`, leading to deadlock.

**PR Fix:** Refactor to collect events inside the lock but fire them outside using `Monitor.Exit`/`Monitor.Enter`.

**Review:**

- **Correctness**: The approach is sound — collecting transition results inside the lock and firing events outside prevents re-entrant deadlocks.
- **Concern — `ForceState` method**: The same deadlock pattern exists in `ForceState()` (line 455-474), which fires `StateExited`, `StateEntered`, and `StateChanged` events while holding `stateLock`. This method is **not** addressed by the PR fix and will deadlock on macOS under the same conditions.
- **Concern — `RequestTransition` event firing**: `TransitionRejected?.Invoke()` is called at line 309 while holding `stateLock`. If a rejection handler tries to query or modify state, it will deadlock.
- **Concern — `DebounceStarted`/`DebounceCancelled`**: These events are fired at lines 758 and 776 while holding `stateLock`.

**Recommendations:**

1. Apply the same "collect inside lock, fire outside" pattern to **all** event-firing methods: `ForceState`, `RequestTransition` (for `TransitionRejected`), `StartDebounce` (for `DebounceStarted`), and `CancelDebounce` (for `DebounceCancelled`).
2. Consider switching from `lock`/`Monitor` to a non-reentrant pattern that makes deadlocks fail-fast rather than hang silently.

---

## 4. Issue 4: `WebChatDialog` crashes on macOS (HIGH — NOT FIXED)

**Problem:** `LoadInitialHtmlIntoWebView()` calls `this._webView.LoadHtml(html, new Uri("https://smarthopper.local/"))`. On macOS, Eto.Forms' `WKWebViewHandler.LoadHtml()` calls `WKWebView.LoadFileUrl()` for any non-null `baseUri`, but `LoadFileUrl()` only accepts `file://` URLs. Passing `https://` triggers `NSInvalidArgumentException`.

**Root Cause:** Eto.Forms macOS `WKWebViewHandler.LoadHtml()` implementation:

```csharp
public void LoadHtml(string html, Uri baseUri)
{
    var baseNSUrl = baseUri.ToNS();
    if (baseNSUrl != null)
        Control.LoadFileUrl(baseNSUrl, baseNSUrl);  // BUG: only works with file:// URLs
    Control.LoadHtmlString(html, baseNSUrl);
}
```

**Recommended Fix:**

Pass `null` as `baseUri` on non-Windows platforms. The `https://smarthopper.local/` origin is used on Windows for WebView2's virtual host mapping, but on macOS with WKWebView, JS-to-C# communication uses `WKUserContentController.AddScriptMessageHandler()` which is origin-independent.

```csharp
// On macOS, WKWebView.LoadFileUrl() only accepts file:// URIs.
// Pass null to use about:blank origin; JS↔C# bridge uses message handlers, not origin.
Uri baseUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new Uri("https://smarthopper.local/")
    : null;
this._webView.LoadHtml(html, baseUri);
```

**Risk**: Low. The `baseUri` is only used for same-origin policy enforcement. Since SmartHopper's WebView doesn't load external resources or use `fetch()` against the base URI, `null` (which resolves to `about:blank`) is safe.

---

## 5. Issue 5: Provider Icon Dependency on System.Drawing (MEDIUM — NOT CONFIRMED)

### Problem Description

`IAIProvider.Icon` returns `System.Drawing.Image`. All provider implementations create `System.Drawing.Bitmap` from embedded resources. The settings UI (`ProvidersSettingsPage.cs`, `GenericProviderSettingsPage.cs`) converts these to Eto bitmaps via `provider.Icon.Save(ms, System.Drawing.Imaging.ImageFormat.Png)`.

On macOS, `System.Drawing.Common` (GDI+) requires `libgdiplus` which may not be installed. Rhino 8 for Mac ships its own .NET runtime and **does** include `System.Drawing.Common` support, so this may work. However:

- `System.Drawing.Common` is officially **unsupported on non-Windows** starting from .NET 7 (throws `PlatformNotSupportedException` unless `System.Drawing.EnableUnixSupport` is set).
- The `.csproj` files reference `System.Drawing.Common` version 8.0.11 with `ExcludeAssets="runtime"`, relying on Rhino's bundled runtime.

### Risk Assessment

- **If Rhino 8 Mac bundles a working `System.Drawing.Common`**: No issue. The community tester did not report icon problems.
- **If Rhino updates its runtime or removes GDI+ support**: All icon loading will throw `PlatformNotSupportedException`.

### Recommendations

1. **Short-term**: Wrap icon loading in try-catch (already done in settings pages). Verify the `CreateFallbackIcon()` in `CanvasButton.cs` also works on macOS (it uses `Graphics.FromImage()` which is GDI+).
2. **Long-term**: Consider migrating `IAIProvider.Icon` to return `byte[]` (PNG bytes) instead of `System.Drawing.Image`, letting consumers convert to their UI framework's bitmap type. This eliminates the GDI+ dependency from the provider interface.

---

## 6. Additional Security Concerns

### 6.1 Assembly Loading Without Verification on macOS (CRITICAL)

With Authenticode skipped on macOS, `LoadProviderAssembly()` loads **any** DLL matching `SmartHopper.Providers.*.dll` from the assembly directory. An attacker who can write to the Grasshopper Libraries folder can inject a malicious provider DLL that:

- Exfiltrates API keys (providers have access to `GetSetting<string>("ApiKey")`)
- Executes arbitrary code in the Rhino process
- Modifies Grasshopper documents silently

**Mitigation**: The strong-name token check provides some protection, but strong-naming is not a security boundary. Consider:

- Embedding a SHA-256 hash allowlist of known provider DLLs
- Requiring user confirmation with the DLL's hash displayed
- Using macOS `codesign` verification as an Authenticode equivalent

### 6.2 Trust Prompt Blocking on UI Thread

`LoadProviderAssembly()` (line 128-145) shows a blocking confirmation dialog via `TaskCompletionSource` + `RhinoApp.InvokeOnUiThread`. If `InvokeOnUiThread` is called **from** the UI thread, this creates a synchronous wait that could deadlock. The `tcs.Task.Result` call is a sync-over-async pattern.

**Recommendation**: Use `async`/`await` for the trust prompt flow, or ensure `LoadProviderAssembly` is never called on the UI thread.

---

## 7. Summary of Required Changes

|#|Issue|Severity|Status|Action Required|
|---|---|---|---|---|
|1|VerifySignature crash|HIGH|Fix ready|Apply platform guard + add warning log|
|2|BuildFullUrl file://|HIGH|Fix ready|Apply scheme check to `AIProvider<T>.BuildFullUrl()`|
|3|StateManager deadlock|HIGH|Fix ready|Apply event-outside-lock pattern to ALL event-firing methods|
|4|WebChatDialog crash|HIGH|NOT FIXED|Pass `null` baseUri on non-Windows|
|5|Provider icon GDI+|MEDIUM|NOT CONFIRMED|Wrap in try-catch, monitor for future breakage|
|6|No verification on macOS|CRITICAL|Security gap|Add warning log, plan hash-based verification|
|7|Trust prompt deadlock risk|MEDIUM|Latent|Refactor to async flow|

---

## 8. Alternatives Considered

### For Issue 2 (BuildFullUrl)

- **Alternative A**: Use `UriKind.RelativeOrAbsolute` and check if the result has an HTTP scheme. *Chosen — simplest and most robust.*
- **Alternative B**: Always prepend base URL and let `Uri` constructor handle deduplication. *Rejected — breaks when endpoint is already absolute (e.g., custom server URLs).*
- **Alternative C**: Use string-based URL construction instead of `Uri` class. *Rejected — loses URI validation and normalization.*

### For Issue 3 (Deadlock)

- **Alternative A**: Collect events inside lock, fire outside. *Chosen — minimal change, proven pattern.*
- **Alternative B**: Use `ReaderWriterLockSlim` with recursion policy. *Rejected — adds complexity, doesn't solve the fundamental re-entrancy issue with event handlers.*
- **Alternative C**: Make all state access lock-free with `Interlocked`. *Rejected — state machine transitions require atomic read-modify-write of multiple fields.*

### For Issue 4 (WebChatDialog)

- **Alternative A**: Pass `null` as baseUri on non-Windows. *Chosen — minimal risk, JS bridge is origin-independent.*
- **Alternative B**: Pass a `file://` temp directory URI. *Rejected — creates filesystem dependency and potential security issues with file:// origin.*
- **Alternative C**: Patch Eto.Forms. *Rejected — external dependency, slow to propagate.*
