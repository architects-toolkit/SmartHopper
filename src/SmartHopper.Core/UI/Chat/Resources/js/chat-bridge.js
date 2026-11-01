/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Chat bridge (chat-bridge.js)
 * ============================
 * Mounts the vendored chat-ui toolkit (window.ChatUI, Resources/vendor/chat-ui.iife.js)
 * and exposes the compatibility globals the C# host drives via ExecuteScript.
 *
 * Shell ownership split:
 *   - Toolkit (chat-ui): status line, scrollable message list (.cui-messages),
 *     jump-to-bottom button, attachment strip, composer (input/send/cancel),
 *     context-usage card, quote chip, model picker (feature-gated).
 *   - Bridge (this file): compat globals, html part renderer that emits the
 *     legacy .message markup raw, keyed upsert/patch pipeline, plugin cards
 *     mapped to native chat-ui parts (consent -> card:approval, question ->
 *     card:question, task plan -> card:task_plan, suggested prompts ->
 *     suggestions part, canvas pointer -> evidence + meta.actions), autonomy
 *     overlay, toast, collapsibles, metrics tooltips, sanitization.
 *
 * Structured-html decomposition: incoming .message bubbles are sniffed by
 * partsForHtml(). Short single-block notice bubbles (system/summary/info/
 * warning/error/debug role classes) become `evidence` parts; a meaningful
 * metrics footer (.metrics-icon not .hidden) becomes a `usage` part appended
 * after the (footer-stripped) html part. Everything else stays an `html` part.
 *
 * Compatibility contract (do NOT rename; C# call sites use these exact names):
 *   addMessage, upsertMessage, upsertMessageAfter, removeThinkingMessage,
 *   addLoadingMessage, replaceLastMessageByRole, processCodeBlocks, processLinks,
 *   scrollToBottom, setStatus, setProcessing, setQueue, addAttachmentChip,
 *   removeAttachmentChip, clearAttachments, setAttachEnabled, updateAutonomyUsage,
 *   showPlanConsent, resolvePlanConsent, updateTaskPlan, showSuggestedPrompts,
 *   showCanvasPointer, showUserQuestion, resolveUserQuestion, resetMessages,
 *   getAllMessageKeys, showToast  (+ window.SH_READY readiness flag)
 *   Optional forward-compat global (no C# call site yet): setModels.
 *
 * Session queue: features.queue is on and the host pushes the merged outgoing
 * queue (queued user sends + per-worker pending instructions) through setQueue.
 * While a turn runs the composer stays editable (type-ahead): the bridge keeps
 * the toolkit's internal processing flag unset and drives the send/cancel swap
 * through [data-processing] itself, relabeling Send -> Queue so mid-turn sends
 * land in the host FIFO instead of being rejected. Queue row actions travel
 * back as sh://event?type=queue&action=remove|edit|submit-now&id=…(&text=…).
 *
 * JS -> C# events keep the sh:// / clipboard:// scheme contract unchanged.
 */

// Scroll thresholds (pixels)
const SCROLL_BOTTOM_THRESHOLD = 30; // consider near-bottom within this distance

// Render limits and thresholds
const TEMPLATE_CACHE_MAX_HTML = 256 * 1024; // larger payloads (e.g. embedded images) render uncached
const PERF_LOG_THRESHOLD_MS = 16; // only log perf outliers (>1 frame)
const LRU_MAX_ENTRIES = 100; // recent DOM html cache size
const FLUSH_INTERVAL_MS = 50; // max wait before flushing queued DOM ops
const DIFF_SAMPLE_RATE = 1.0; // always diff before mutating the DOM; equality check is far cheaper than a DOM write
const RENDER_ANIM_DURATION_MS = 280; // wipe animation duration
const PERF_SAMPLE_RATE = 0.25; // sample perf counters to reduce overhead

// Internal caches
const _templateCache = new Map(); // html string -> DocumentFragment
const _htmlLru = new Map(); // key -> html (maintains LRU order)
const _pendingOps = [];
let _flushScheduled = false;
const _perfCounters = {
    renders: 0,
    renderMs: 0,
    renderSlow: 0,
    equalityChecks: 0,
    equalityMs: 0,
    flushes: 0,
};

// Toolkit shell state. _view is the ChatViewHandle returned by ChatUI.createChatView;
// _els holds the internal elements the bridge rewires (see initChatUiShell).
let _view = null;
const _els = {
    root: null,        // .chat-ui root
    status: null,      // .cui-status
    messages: null,    // .cui-messages (renamed to #chat-container at init)
    composer: null,    // .cui-composer
    input: null,       // .cui-input     (renamed to #user-input)
    attach: null,      // .cui-attach    (renamed to #attach-button)
    send: null,        // .cui-send      (renamed to #send-button)
    cancel: null,      // .cui-cancel    (renamed to #cancel-button)
    attachStrip: null, // .cui-attachments
    spinner: null,     // <span id="spinner"> injected into .cui-status
};
let _anonSeq = 0;

function lruSet(key, value) {
    if (!key) return;
    if (_htmlLru.has(key)) {
        _htmlLru.delete(key);
    }
    _htmlLru.set(key, value);
    if (_htmlLru.size > LRU_MAX_ENTRIES) {
        const oldest = _htmlLru.keys().next().value;
        _htmlLru.delete(oldest);
    }
}

function lruGet(key) {
    if (!key) return null;
    if (!_htmlLru.has(key)) return null;
    const val = _htmlLru.get(key);
    // touch
    _htmlLru.delete(key);
    _htmlLru.set(key, val);
    return val;
}

function scheduleFlush() {
    if (_flushScheduled) return;
    _flushScheduled = true;
    // Prefer rAF, fall back to setTimeout
    const flushFn = () => {
        _flushScheduled = false;
        flushDomOps();
    };
    if (typeof requestAnimationFrame === 'function') {
        requestAnimationFrame(flushFn);
    } else {
        setTimeout(flushFn, FLUSH_INTERVAL_MS);
    }
}

function enqueueDomOp(op) {
    _pendingOps.push(op);
    scheduleFlush();
}

function flushDomOps() {
    const ops = _pendingOps.splice(0, _pendingOps.length);
    if (Math.random() <= PERF_SAMPLE_RATE) {
        _perfCounters.flushes += 1;
    }
    for (let i = 0; i < ops.length; i++) {
        try {
            ops[i]();
        } catch (err) {
            console.error('[JS] flushDomOps error', err);
        }
    }
}

function shouldSkipBecauseSame(key, html) {
    if (!key || !html) return false;
    // Sample to reduce cost
    if (Math.random() > DIFF_SAMPLE_RATE) return false;
    const previous = lruGet(key);
    if (!previous) return false;
    return previous === html;
}

function recordHtmlCache(key, html) {
    if (!key || !html) return;
    lruSet(key, html);
}

/**
 * Sanitizes an untrusted HTML string using DOMPurify before it is inserted into
 * the DOM. This removes script tags, event handlers, javascript: URLs and other
 * XSS vectors while preserving the safe markup Markdig produces.
 * @param {string} html - The HTML string to sanitize.
 * @returns {string} The sanitized HTML, or an empty string if sanitization is unavailable.
 */
function sanitizeHtml(html) {
    if (!html) return '';
    if (typeof DOMPurify === 'undefined' || !DOMPurify.sanitize) {
        console.warn('[JS] DOMPurify not available; returning escaped text instead of HTML');
        return html.replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }
    return DOMPurify.sanitize(html, {
        USE_PROFILES: { html: true },
        ALLOW_DATA_ATTR: true,
        ALLOW_ARIA_ATTR: true
    });
}

function addWipeAnimation(node) {
    try {
        if (!node || !node.classList) return;
        node.classList.add('wipe-in');
        node.style.setProperty('--wipe-duration', `${RENDER_ANIM_DURATION_MS}ms`);
        const remove = () => node.classList.remove('wipe-in');
        node.addEventListener('animationend', remove, { once: true });
    } catch { /* ignore */ }
}

function cloneFromTemplate(html, context) {
    if (!html) return null;
    const originalHtml = html;
    let frag = _templateCache.get(originalHtml);
    if (!frag) {
        // Large payloads (data-URI images in tool results, big JSON dumps) are
        // legitimate and must render in full — truncating mid-attribute corrupts
        // the markup. They only bypass the fragment cache to keep it lightweight.
        if (html.length > TEMPLATE_CACHE_MAX_HTML) {
            console.warn(`[JS] ${context}: html length ${html.length} exceeds template-cache cap; rendering uncached`);
            return null;
        }
        const safeHtml = sanitizeHtml(html);
        if (!safeHtml) {
            console.warn(`[JS] ${context}: sanitized html is empty`);
            return null;
        }
        const temp = document.createElement('div');
        temp.innerHTML = safeHtml;
        frag = document.createDocumentFragment();
        while (temp.firstChild) {
            frag.appendChild(temp.firstChild);
        }
        _templateCache.set(originalHtml, frag.cloneNode(true));
    }
    return frag.cloneNode(true).firstElementChild || frag.cloneNode(true).firstChild || null;
}

function parsePatchPayload(messageHtml) {
    // Supports JSON string like {"patch":"append","html":"..."} to avoid resending full bodies
    if (!messageHtml || typeof messageHtml !== 'string') return null;
    if (messageHtml.length === 0 || messageHtml[0] !== '{') return null;
    try {
        const obj = JSON.parse(messageHtml);
        if (obj && typeof obj === 'object' && obj.patch && obj.html) {
            return obj;
        }
    } catch {
        // Not a patch object
    }
    return null;
}

/**
 * Patches an existing message bubble in place by swapping only the parts of the DOM that
 * can legitimately change between renders (the `.message-content` body and the metrics
 * footer), instead of replacing the entire bubble subtree. This avoids destroying and
 * rebuilding the avatar/header/footer chrome on every streaming delta, which is the main
 * cause of DOM-churn related jank during long streamed responses.
 * @param {HTMLElement} existing - The currently rendered message bubble.
 * @param {HTMLElement} incoming - The freshly parsed message bubble containing the new content.
 * @returns {boolean} True if the patch was applied successfully.
 */
function patchExistingMessage(existing, incoming) {
    if (!existing || !incoming) return false;
    try {
        const oldContent = existing.querySelector('.message-content');
        const newContent = incoming.querySelector('.message-content');
        if (!oldContent || !newContent) return false;

        // Swap the expensive part: the rendered Markdown body.
        oldContent.innerHTML = newContent.innerHTML;

        // Keep the copy-to-clipboard source text in sync (cheap attribute copy).
        const copyContent = newContent.getAttribute('data-copy-content');
        if (copyContent !== null) {
            oldContent.setAttribute('data-copy-content', copyContent);
        }

        // Keep the metrics footer in sync (e.g. becomes visible once final metrics arrive).
        const oldFooterIcon = existing.querySelector('.message-footer .metrics-icon');
        const newFooterIcon = incoming.querySelector('.message-footer .metrics-icon');
        if (oldFooterIcon && newFooterIcon) {
            oldFooterIcon.className = newFooterIcon.className;
            const metricsAttrs = ['data-in', 'data-out', 'data-provider', 'data-model', 'data-reason', 'data-context-usage'];
            for (const attr of metricsAttrs) {
                const val = newFooterIcon.getAttribute(attr);
                if (val !== null) {
                    oldFooterIcon.setAttribute(attr, val);
                }
            }
        }

        return true;
    } catch (err) {
        console.error('[JS] patchExistingMessage error', err);
        return false;
    }
}

function applyPatchToExisting(existing, patchObj, context) {
    if (!existing || !patchObj) return null;
    const content = existing.querySelector('.message-content');
    if (!content) {
        // Never append patch markup into a node without a content anchor (e.g. a
        // native-part .cui-msg has none) — the host resends the full payload.
        console.warn(`[JS] ${context}: patch target has no .message-content`);
        return null;
    }
    const safeHtml = sanitizeHtml(patchObj.html || '');
    if (patchObj.patch === 'append') {
        const temp = document.createElement('div');
        temp.innerHTML = safeHtml;
        // Append children to content
        while (temp.firstChild) {
            content.appendChild(temp.firstChild);
        }
        return existing;
    }
    if (patchObj.patch === 'replace-content') {
        content.innerHTML = safeHtml;
        return existing;
    }
    console.warn(`[JS] ${context}: unsupported patch type`, patchObj.patch);
    return null;
}

/**
 * Returns the chat container element and whether it was at (or near) bottom before changes.
 * Helps standardize error handling and scroll-state capture before DOM mutations.
 * @param {string} context - Caller context for logging
 * @returns {{ chatContainer: HTMLElement|null, wasAtBottom: boolean }}
 */
function getContainerWithBottom(context) {
    const chatContainer = document.getElementById('chat-container');
    if (!chatContainer) {
        console.error(`[JS] ${context}: chat-container element not found`);
        // Default wasAtBottom to true if container missing to avoid unintended UI side effects
        return { chatContainer: null, wasAtBottom: true };
    }
    return { chatContainer, wasAtBottom: isAtBottom(chatContainer, SCROLL_BOTTOM_THRESHOLD) };
}

/**
 * Safely parses an HTML string into a single node (first element or text node).
 * Returns null and logs on error to keep callers simple.
 * @param {string} messageHtml - HTML content to parse
 * @param {string} context - Caller context for logging
 * @returns {HTMLElement|null}
 */
function createNodeFromHtml(messageHtml, context) {
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = sanitizeHtml(messageHtml || '');
    const node = tempDiv.firstElementChild || tempDiv.firstChild;
    if (!node) {
        console.error(`[JS] ${context}: no valid node in messageHtml`);
        return null;
    }
    return node;
}

/**
 * Parses an HTML string and returns the firstElementChild only.
 * Logs and returns null when no element is present to avoid text-node insertions.
 * @param {string} messageHtml
 * @param {string} context
 * @returns {HTMLElement|null}
 */
function createElementFromHtml(messageHtml, context) {
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = sanitizeHtml(messageHtml || '');
    const el = tempDiv.firstElementChild || null;
    if (!el) {
        console.error(`[JS] ${context}: no valid element in HTML`);
        return null;
    }
    return el;
}

/**
 * Sets dataset.key on a node if possible, ignoring failures (e.g., text nodes).
 * @param {HTMLElement} node
 * @param {string} key
 */
function setDatasetKeySafe(node, key) {
    try { if (node && node.dataset) node.dataset.key = key || ''; } catch {}
}

/**
 * Finds a keyed part node inside container by its dataset.key value. Keyed nodes
 * are the rendered part roots (legacy `.message` bubbles, `.cui-part-html`
 * wrappers) or the `.cui-msg` chrome itself when the interaction rendered only
 * native parts (evidence/usage/cards carry no `.message` element). The `.cui-msg`
 * ancestor sorts before its inner `.message` in document order, so callers that
 * need the toolkit wrapper (topOf/moveAfter) and callers that patch
 * `.message-content` both work off the same lookup.
 * @param {HTMLElement} chatContainer
 * @param {string} key
 * @returns {HTMLElement|null}
 */
function findExistingMessageByKey(chatContainer, key) {
    const parts = Array.from(chatContainer.querySelectorAll('[data-key]'));
    return parts.find(m => (m.dataset && m.dataset.key) === (key || '')) || null;
}

/**
 * Inserts a node immediately after a reference node within the same container.
 * If the reference is the last child, appends; otherwise uses insertBefore(nextSibling).
 * @param {HTMLElement} container
 * @param {HTMLElement} node
 * @param {HTMLElement} reference
 * @param {string} context - Caller context for logging
 */
function insertAfterNode(container, node, reference, context) {
    if (reference && reference.nextSibling) {
        container.insertBefore(node, reference.nextSibling);
    } else {
        container.appendChild(node);
    }
    if (context) console.log(`[JS] ${context}: inserted node after reference`);
}

/**
 * Inserts the given node either above the last persistent thinking (loading) message
 * or appends it to the end when no loader is the last child.
 * This keeps the thinking bubble as the last item while new messages stack above it.
 * @param {HTMLElement} chatContainer
 * @param {HTMLElement} node
 */
function insertAboveThinkingIfPresent(chatContainer, node) {
    try {
        const loaders = Array.from(chatContainer.querySelectorAll('.message.loading'));
        const lastLoader = loaders.length > 0 ? loaders[loaders.length - 1] : null;
        const isLastChildLoader = lastLoader && chatContainer.lastElementChild === lastLoader;
        if (isLastChildLoader) {
            chatContainer.insertBefore(node, lastLoader);
        } else {
            chatContainer.appendChild(node);
        }
        console.log('[JS] insertAboveThinkingIfPresent:', {
            foundLoader: !!lastLoader,
            isLastChildLoader: !!isLastChildLoader,
            action: isLastChildLoader ? 'insertBefore(loader)' : 'append',
            nodeClass: node && node.className
        });
    } catch (err) {
        console.error('[JS] insertAboveThinkingIfPresent error:', err);
        try { chatContainer.appendChild(node); } catch {}
    }
}

// ---------------------------------------------------------------------------
// chat-ui shell integration
// ---------------------------------------------------------------------------

/**
 * The toolkit message list element (.cui-messages). At init it is renamed to
 * #chat-container so every legacy selector keeps working.
 * @returns {HTMLElement|null}
 */
function messagesEl() {
    return _els.messages || document.getElementById('chat-container');
}

/** Unique id for interactions pushed without a host-side domKey (addMessage). */
function nextAnonId() {
    return `sh-anon-${++_anonSeq}`;
}

/**
 * Derives the canonical chat-ui role for the .cui-msg wrapper from the legacy
 * `.message <role>` class carried by the rendered markup.
 * @param {string} html
 * @returns {'user'|'assistant'|'system'|'tool'}
 */
function sniffRole(html) {
    const m = /class\s*=\s*["']?\s*message\s+([a-zA-Z-]+)/i.exec(html || '');
    const cls = m && m[1] ? m[1].toLowerCase() : '';
    if (cls === 'user') return 'user';
    if (cls === 'tool') return 'tool';
    if (cls === 'assistant') return 'assistant';
    if (cls) return 'system'; // system|summary|error|warning|info|debug|loading
    return 'assistant';
}

/**
 * The top-level node of a keyed part inside the message list: the enclosing
 * .cui-msg wrapper when the part lives inside the toolkit chrome, or the node
 * itself for raw children (loading bubbles, plugin cards).
 * @param {HTMLElement} el
 * @returns {HTMLElement|null}
 */
function topOf(el) {
    if (!el) return null;
    const wrap = el.closest ? el.closest('.cui-msg') : null;
    return wrap || el;
}

/**
 * The stamped part node inside a just-rendered .cui-msg (the `.message` root or
 * the `.cui-part-html` wrapper). Returns the .cui-msg itself when the message
 * has no html part (e.g. a different part type rendered natively).
 * @param {HTMLElement} msgEl
 * @returns {HTMLElement|null}
 */
function innerContent(msgEl) {
    if (!msgEl) return null;
    if (msgEl.classList && (msgEl.classList.contains('message') || msgEl.classList.contains('cui-part-html'))) return msgEl;
    return msgEl.querySelector ? msgEl.querySelector('.message, .cui-part-html') : null;
}

/**
 * Moves a node appended by the toolkit at the end of the list above a trailing
 * raw loading bubble, preserving the legacy "loader stays last" ordering.
 * @param {HTMLElement} node
 */
function hoistAboveTrailingLoader(node) {
    try {
        if (!node || !node.parentElement) return;
        const prev = node.previousElementSibling;
        if (prev && prev.classList && prev.classList.contains('message') && prev.classList.contains('loading')) {
            node.parentElement.insertBefore(node, prev);
        }
    } catch (err) {
        console.warn('[JS] hoistAboveTrailingLoader error:', err);
    }
}

/**
 * Positions `node` immediately after `followTop` inside the message list,
 * honoring the upsertMessageAfter ordering contract.
 * @param {HTMLElement} chatContainer
 * @param {HTMLElement} node
 * @param {HTMLElement} followTop
 */
function moveAfter(chatContainer, node, followTop) {
    try {
        if (!node || !followTop || node === followTop) return;
        // followTop must still be a direct child (it is detached when a
        // same-key re-render replaced it — degenerate followKey === key case).
        if (followTop.parentElement !== chatContainer) return;
        insertAfterNode(chatContainer, node, followTop, 'moveAfter');
    } catch (err) {
        console.warn('[JS] moveAfter error:', err);
    }
}

/**
 * chat-ui custom part renderer for `type: 'html'` parts. Emits the sanitized
 * legacy markup as the part element itself (no extra .cui-part-html chrome when
 * the payload is a single-root .message bubble) and stamps `data-key` from the
 * interaction domKey so the bridge's keyed pipeline can address it.
 * Multi-root payloads keep every sibling inside a .cui-part-html wrapper.
 * @param {{html:string}} part
 * @param {{message:object, ownerDocument:Document}} ctx
 * @returns {HTMLElement|null}
 */
function renderHtmlPart(part, ctx) {
    const doc = (ctx && ctx.ownerDocument) || document;
    const html = part && typeof part.html === 'string' ? part.html : '';
    const tmp = doc.createElement('div');
    tmp.innerHTML = sanitizeHtml(html);

    let node = null;
    if (tmp.children.length === 1) {
        node = tmp.firstElementChild;
    } else if (tmp.childNodes.length > 0) {
        node = doc.createElement('div');
        node.className = 'cui-part-html';
        while (tmp.firstChild) {
            node.appendChild(tmp.firstChild);
        }
    }
    if (!node) return null;

    // Stamp the stable key only when the interaction carries a real domKey —
    // anonymous addMessage inserts (generated ids) stay keyless, matching the
    // legacy contract where getAllMessageKeys skipped unkeyed bubbles.
    const key = ctx && ctx.message ? ctx.message.domKey : null;
    if (key && node.dataset) node.dataset.key = key;
    return node;
}

// ---------------------------------------------------------------------------
// Structured-html -> canonical part decomposition
// ---------------------------------------------------------------------------

/**
 * `.message <role>` classes that render as notice lines rather than full
 * bubbles when their content is short and free of block markup. Special turns
 * and background lines (subagent evidence, document/context events, runtime
 * notices) arrive through these classes.
 */
const NOTICE_LEVEL_BY_ROLE = {
    error: 'error',
    warning: 'warning',
    info: 'info',
    debug: 'info',
    system: 'info',
    summary: 'info',
};

/** Longest notice body that still maps to an evidence part. */
const EVIDENCE_MAX_CHARS = 600;

/** Markup that requires full html rendering (never maps to evidence). */
const EVIDENCE_BLOCK_TAGS = new Set([
    'PRE', 'TABLE', 'UL', 'OL', 'BLOCKQUOTE', 'DETAILS', 'IMG', 'VIDEO', 'AUDIO',
    'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'HR', 'FORM', 'INPUT', 'BUTTON',
    'SELECT', 'TEXTAREA', 'CANVAS', 'IFRAME', 'SVG', 'OBJECT',
]);

/**
 * Decomposes a legacy `.message` html payload into canonical chat-ui parts:
 *  - notice-role bubbles with short, block-free bodies -> a single `evidence`
 *    part (severity comes from the role class);
 *  - a `.message-footer` metrics icon carrying data (not `.hidden`) -> a
 *    `usage` part appended after the html body; the footer chrome is stripped
 *    either way so the html part only carries header + content;
 *  - everything else -> the payload stays one `html` part.
 * @param {string} messageHtml
 * @returns {{parts:object[], role:string, el:HTMLElement|null, rawEl:HTMLElement|null, plain:boolean}}
 *   `el` is the parsed (possibly footer-stripped) `.message` element; `rawEl` is
 *   the same element before the footer strip, reused by the in-place patch path
 *   so its metrics-icon sync still sees the incoming footer. `plain` is true
 *   only when the result is exactly one html part, which is the only shape the
 *   cheap in-place patch can apply.
 */
function partsForHtml(messageHtml) {
    const role = sniffRole(messageHtml);
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = sanitizeHtml(messageHtml || '');
    const el = tempDiv.firstElementChild || null;
    const isMessage = !!el && tempDiv.children.length === 1 &&
        el.classList && el.classList.contains('message');
    if (!isMessage) {
        return { parts: [{ type: 'html', html: messageHtml || '' }], role, el, rawEl: el, plain: true };
    }

    const roleMatch = /(^|\s)message\s+([a-zA-Z-]+)/.exec(el.className || '');
    const roleCls = roleMatch ? roleMatch[2].toLowerCase() : '';
    const content = el.querySelector('.message-content');

    // Notice roles -> evidence part when the body is short, plain text.
    if (Object.prototype.hasOwnProperty.call(NOTICE_LEVEL_BY_ROLE, roleCls) && content) {
        const text = (content.textContent || '').trim();
        let hasBlock = false;
        if (text) {
            const descendants = content.querySelectorAll('*');
            for (const node of descendants) {
                if (EVIDENCE_BLOCK_TAGS.has(node.tagName)) { hasBlock = true; break; }
            }
        }
        if (text && text.length <= EVIDENCE_MAX_CHARS && !hasBlock) {
            return {
                parts: [{ type: 'evidence', level: NOTICE_LEVEL_BY_ROLE[roleCls], text }],
                role: 'system',
                el,
                rawEl: el,
                plain: false,
            };
        }
    }

    // Metrics footer -> usage part. The footer chrome is stripped from the html
    // body either way; a `.hidden` icon means the host had no metrics to report.
    // `rawEl` keeps a pre-strip copy for patchExistingMessage's footer sync.
    const rawEl = el.cloneNode(true);
    const footer = el.querySelector('.message-footer');
    const icon = footer ? footer.querySelector('.metrics-icon') : null;
    let usage = null;
    if (icon && !icon.classList.contains('hidden')) {
        const positiveInt = (name) => {
            const n = parseInt(icon.getAttribute(name), 10);
            return Number.isFinite(n) && n > 0 ? n : null;
        };
        const provider = icon.getAttribute('data-provider') || '';
        const model = icon.getAttribute('data-model') || '';
        const reason = icon.getAttribute('data-reason') || '';
        const ctxUsage = icon.getAttribute('data-context-usage') || '';
        const label = [
            provider,
            model,
            reason && reason !== 'unknown' ? reason : '',
            ctxUsage ? `ctx ${ctxUsage}` : '',
        ].filter(Boolean).join(' · ');
        usage = {};
        if (label) usage.label = label;
        const tokensIn = positiveInt('data-in');
        const tokensOut = positiveInt('data-out');
        if (tokensIn != null) usage.tokensIn = tokensIn;
        if (tokensOut != null) usage.tokensOut = tokensOut;
    }
    if (footer) footer.remove();

    const parts = [{ type: 'html', html: el.outerHTML }];
    if (usage) parts.push({ type: 'usage', usage });
    return { parts, role, el, rawEl, plain: !usage };
}

/**
 * Stamps the toolkit `.cui-msg` chrome with dataset identifiers: `data-iid`
 * always (interaction id — used by plugin-card freezes and byKey lookups) and
 * `data-key` when the interaction carries a real domKey, which is what
 * getAllMessageKeys and the keyed upsert pipeline operate on. Plugin/notice
 * cards only get `data-iid` so they stay out of getAllMessageKeys.
 * @param {HTMLElement|null} top - the `.cui-msg` node
 * @param {{id:string, domKey?:string}} interaction
 */
function stampToolkitMsg(top, interaction) {
    try {
        if (!top || !top.dataset || !interaction) return;
        top.dataset.iid = interaction.id || '';
        if (interaction.domKey) top.dataset.key = interaction.domKey;
    } catch { /* ignore */ }
}

/**
 * Registry of native plugin interactions (consent/question/task-plan/pointer)
 * so `routeMessageAction` can recover the host-side entity id from the toolkit
 * message id and the `resolve*` globals can locate the card to freeze.
 * interactionId -> { kind, refId, options? }
 */
const _pluginCards = new Map();

/**
 * Finds the `.cui-msg` chrome stamped with a plugin interaction id.
 * @param {string} iid
 * @returns {HTMLElement|null}
 */
function pluginMsgEl(iid) {
    const nodes = document.querySelectorAll('.cui-msg[data-iid]');
    for (const node of nodes) {
        if (node.dataset && node.dataset.iid === iid) return node;
    }
    return null;
}

/**
 * Freezes a resolved plugin card in place: disables every control and marks
 * the card resolved. Mirrors the legacy `.resolved` DOM freeze without a
 * re-render so the user's chosen state label stays visible.
 * @param {string} iid - plugin interaction id
 */
function freezePluginCard(iid) {
    const node = pluginMsgEl(iid);
    if (!node) return;
    node.querySelectorAll('button, input, select, textarea').forEach(c => { c.disabled = true; });
    const card = node.querySelector('.cui-card');
    if (card) card.dataset.resolved = 'true';
}

/**
 * Pushes a native plugin interaction (card/part message) into the toolkit
 * message list, records it in `_pluginCards`, stamps `data-iid`, and keeps the
 * trailing-loader ordering rule.
 * @param {string} iid - interaction id (becomes message.id for action routing)
 * @param {string} refId - host-side entity id embedded in sh:// event urls
 * @param {string} kind - registry kind label
 * @param {string} role - canonical chat-ui role
 * @param {object[]} parts
 * @param {object} [meta] - message meta (e.g. actions rows)
 * @param {string[]} [options] - question option labels for choice-index recovery
 */
function pushPluginInteraction(iid, refId, kind, role, parts, meta, options) {
    _pluginCards.set(iid, { kind, refId, options });
    const interaction = { id: iid, role, parts };
    if (meta) interaction.meta = meta;
    try {
        _view.addInteraction(interaction);
    } catch (err) {
        _pluginCards.delete(iid); // don't leave a stale refId/dedupe entry
        console.error(`[JS] pushPluginInteraction(${kind}) failed:`, err);
        return;
    }
    const container = document.getElementById('chat-container');
    const top = container ? container.lastElementChild : null;
    stampToolkitMsg(top, interaction);
    hoistAboveTrailingLoader(top);
}

/**
 * Composes the approval card's pre-wrap description from a consent plan:
 * summary, numbered steps (tool + canvas-change note), assumptions, success
 * criteria and the scope note the legacy card rendered as .plan-consent-note.
 * @param {object} plan
 * @returns {string}
 */
function composePlanDescription(plan) {
    const lines = [];
    if (plan && plan.summary) lines.push(String(plan.summary));
    const steps = plan && Array.isArray(plan.steps) ? plan.steps : [];
    if (steps.length) {
        if (lines.length) lines.push('');
        steps.forEach((step, i) => {
            const text = (step && (step.description || step.id)) || 'Step';
            const tool = step && step.tool
                ? ` — ${step.tool}${step.mutatesCanvas ? ' (canvas change)' : ''}`
                : '';
            lines.push(`${i + 1}. ${text}${tool}`);
        });
    }
    const section = (label, values) => {
        if (!Array.isArray(values) || values.length === 0) return;
        lines.push('', `${label}:`);
        values.forEach(v => lines.push(`- ${v}`));
    };
    section('Assumptions', plan && plan.assumptions);
    section('Success criteria', plan && plan.successCriteria);
    lines.push('', 'Approving this approach does not approve canvas changes. Each concrete change receives a graphical review.');
    return lines.join('\n');
}

/**
 * Records a perf sample for a completed render/patch operation.
 * @param {number} start
 * @param {string} context
 * @param {number} len
 */
function noteRenderPerf(start, context, len) {
    const dur = ((typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now()) - start;
    if (Math.random() <= PERF_SAMPLE_RATE) {
        _perfCounters.renders += 1;
        _perfCounters.renderMs += dur;
    }
    if (dur > PERF_LOG_THRESHOLD_MS) {
        _perfCounters.renderSlow += 1;
        console.debug(`[JS] ${context} slow render`, { ms: dur.toFixed(2), len });
    }
}

// ---------------------------------------------------------------------------
// Compat globals — message inserts/upserts (C# -> JS contract)
// ---------------------------------------------------------------------------

/**
 * Adds a message to the chat container.
 * Routes through the toolkit as an anonymous (keyless) html interaction.
 * @param {string} messageHtml - HTML content of the message
 */
function addMessage(messageHtml) {
    enqueueDomOp(() => {
        const start = (typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now();
        const { chatContainer, wasAtBottom } = getContainerWithBottom('addMessage');
        if (!chatContainer) return;

        if (!_view) {
            // Fallback shell (no toolkit): legacy raw DOM insertion.
            const node = cloneFromTemplate(messageHtml, 'addMessage') || createNodeFromHtml(messageHtml, 'addMessage');
            if (!node) return;
            addWipeAnimation(node);
            insertAboveThinkingIfPresent(chatContainer, node);
            finalizeMessageInsertion(node, wasAtBottom);
            noteRenderPerf(start, 'addMessage', messageHtml ? messageHtml.length : 0);
            return;
        }

        try {
            const mapped = partsForHtml(messageHtml);
            const interaction = { id: nextAnonId(), role: mapped.role, parts: mapped.parts };
            _view.addInteraction(interaction);
            const top = chatContainer.lastElementChild;
            stampToolkitMsg(top, interaction); // data-iid only — anonymous stays keyless
            hoistAboveTrailingLoader(top);
            const inner = innerContent(top) || top;
            if (inner && inner.classList && inner.classList.contains('message')) addWipeAnimation(inner);
            finalizeMessageInsertion(inner, wasAtBottom);
        } catch (err) {
            console.error('[JS] addMessage toolkit error:', err);
        }
        noteRenderPerf(start, 'addMessage', messageHtml ? messageHtml.length : 0);
    });
}

/**
 * Upserts a message identified by `key` immediately after the message identified by `followKey`.
 * If `followKey` is not found, falls back to a normal upsert by key.
 * @param {string} followKey - The key of the message to insert after
 * @param {string} key - The key for the incoming message
 * @param {string} messageHtml - HTML string for the message
 */
function upsertMessageAfter(followKey, key, messageHtml) {
    enqueueDomOp(() => {
        const start = (typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now();
        const { chatContainer, wasAtBottom } = getContainerWithBottom('upsertMessageAfter');
        if (!chatContainer) return false;

        // If content matches last rendered, skip
        if (shouldSkipBecauseSame(key, messageHtml)) {
            if (Math.random() <= PERF_SAMPLE_RATE) {
                _perfCounters.equalityChecks += 1;
            }
            return true;
        }

        const patchObj = parsePatchPayload(messageHtml);
        const follow = findExistingMessageByKey(chatContainer, followKey);
        if (!follow) {
            console.warn('[JS] upsertMessageAfter: followKey not found, falling back to upsertMessage');
            return upsertMessage(key, messageHtml);
        }

        const existing = findExistingMessageByKey(chatContainer, key);
        if (patchObj) {
            if (!existing) {
                console.warn('[JS] upsertMessageAfter: patch payload for missing key', key);
                return false;
            }
            const patched = applyPatchToExisting(existing, patchObj, 'upsertMessageAfter');
            if (patched) {
                // Patch updates: no animation (bubble already exists from first chunk)
                finalizeMessageInsertion(patched, wasAtBottom);
                recordHtmlCache(key, messageHtml);
                noteRenderPerf(start, 'upsertMessageAfter(patch)', messageHtml.length);
                return true;
            }
            return false;
        }

        // Full-HTML payload: prefer patching the existing bubble in place (keeps
        // position and chrome, avoids toolkit re-render churn on every delta).
        // Only a payload that maps to a single html part is patchable in place —
        // one that decomposes to native parts (evidence/usage) needs a re-render.
        const mapped = partsForHtml(messageHtml);
        if (existing) {
            if (mapped.plain && mapped.rawEl && patchExistingMessage(existing, mapped.rawEl)) {
                finalizeMessageInsertion(existing, wasAtBottom);
                recordHtmlCache(key, messageHtml);
                noteRenderPerf(start, 'upsertMessageAfter(patch-in-place)', messageHtml.length);
                return true;
            }
        }

        const followTop = topOf(follow);

        if (!_view) {
            // Fallback shell: legacy raw DOM insertion honoring followKey.
            const incoming = cloneFromTemplate(messageHtml, 'upsertMessageAfter') || createNodeFromHtml(messageHtml, 'upsertMessageAfter');
            if (!incoming) return false;
            setDatasetKeySafe(incoming, key);
            addWipeAnimation(incoming);
            if (existing) {
                topOf(existing).replaceWith(incoming);
            }
            insertAfterNode(chatContainer, incoming, followTop, 'upsertMessageAfter');
            finalizeMessageInsertion(incoming, wasAtBottom);
            recordHtmlCache(key, messageHtml);
            noteRenderPerf(start, 'upsertMessageAfter', messageHtml.length);
            return true;
        }

        try {
            if (existing) {
                // Toolkit re-render (native-part payload or a shape the in-place
                // patch rejected).
                _view.updateInteraction(key, { parts: mapped.parts });
            } else {
                _view.addInteraction({
                    id: key,
                    domKey: key,
                    role: mapped.role,
                    parts: mapped.parts,
                });
            }
            const top = chatContainer.lastElementChild;
            stampToolkitMsg(top, { id: key, domKey: key });
            // Honor the ordering contract: the (re)rendered message lands after follow.
            moveAfter(chatContainer, top, followTop);
            hoistAboveTrailingLoader(top);
            finalizeMessageInsertion(innerContent(top) || top, wasAtBottom);
            recordHtmlCache(key, messageHtml);
        } catch (err) {
            console.error('[JS] upsertMessageAfter toolkit error:', err);
            return false;
        }
        noteRenderPerf(start, 'upsertMessageAfter', messageHtml.length);
        return true;
    });
}

/**
 * Upserts a message DOM node identified by a stable key. If a node with the same key exists,
 * its content/metrics are patched in place; otherwise, a toolkit interaction is appended
 * (just above any persistent loading bubble).
 * @param {string} key - Stable identity for the message
 * @param {string} messageHtml - HTML string for the message
 */
function upsertMessage(key, messageHtml) {
    enqueueDomOp(() => {
        const start = (typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now();
        const { chatContainer, wasAtBottom } = getContainerWithBottom('upsertMessage');
        if (!chatContainer) return false;

        // Diff sampling to avoid redundant DOM work
        if (shouldSkipBecauseSame(key, messageHtml)) {
            if (Math.random() <= PERF_SAMPLE_RATE) {
                _perfCounters.equalityChecks += 1;
            }
            return true;
        }

        const patchObj = parsePatchPayload(messageHtml);

        // Find existing by data-key (avoid querySelector escaping issues by scanning)
        const existing = findExistingMessageByKey(chatContainer, key);

        if (patchObj) {
            if (!existing) {
                console.warn('[JS] upsertMessage: patch payload for missing key', key);
                return false;
            }
            const patched = applyPatchToExisting(existing, patchObj, 'upsertMessage');
            if (patched) {
                finalizeMessageInsertion(patched, wasAtBottom);
                recordHtmlCache(key, messageHtml);
                noteRenderPerf(start, 'upsertMessage(patch)', messageHtml.length);
                return true;
            }
            return false;
        }

        // Full-HTML payload: patch the existing bubble in place rather than
        // re-rendering through the toolkit (keeps position; avoids DOM churn).
        // Only a payload mapping to a single html part is patchable in place —
        // native-part payloads (evidence/usage) re-render through the toolkit.
        const mapped = partsForHtml(messageHtml);
        if (existing) {
            if (mapped.plain && mapped.rawEl && patchExistingMessage(existing, mapped.rawEl)) {
                finalizeMessageInsertion(existing, wasAtBottom);
                recordHtmlCache(key, messageHtml);
                noteRenderPerf(start, 'upsertMessage(patch-in-place)', messageHtml.length);
                return true;
            }

            if (!_view) {
                // Fallback shell: replace the whole bubble in place.
                const incomingMsg = createElementFromHtml(messageHtml, 'upsertMessage');
                if (incomingMsg) {
                    setDatasetKeySafe(incomingMsg, key);
                    topOf(existing).replaceWith(incomingMsg);
                    finalizeMessageInsertion(incomingMsg, wasAtBottom);
                    recordHtmlCache(key, messageHtml);
                    noteRenderPerf(start, 'upsertMessage(replace)', messageHtml.length);
                    return true;
                }
                return false;
            }

            try {
                // Toolkit re-render preserving the message's current slot.
                const slot = topOf(existing).nextElementSibling;
                _view.updateInteraction(key, { parts: mapped.parts });
                const top = chatContainer.lastElementChild;
                stampToolkitMsg(top, { id: key, domKey: key });
                if (slot && top !== slot) {
                    chatContainer.insertBefore(top, slot);
                } else {
                    hoistAboveTrailingLoader(top);
                }
                finalizeMessageInsertion(innerContent(top) || top, wasAtBottom);
                recordHtmlCache(key, messageHtml);
            } catch (err) {
                console.error('[JS] upsertMessage toolkit error:', err);
                return false;
            }
            noteRenderPerf(start, 'upsertMessage(re-render)', messageHtml.length);
            return true;
        }

        // New keyed message.
        if (!_view) {
            const incoming = cloneFromTemplate(messageHtml, 'upsertMessage') || createNodeFromHtml(messageHtml, 'upsertMessage');
            if (!incoming) return false;
            setDatasetKeySafe(incoming, key);
            addWipeAnimation(incoming);
            insertAboveThinkingIfPresent(chatContainer, incoming);
            finalizeMessageInsertion(incoming, wasAtBottom);
            recordHtmlCache(key, messageHtml);
            noteRenderPerf(start, 'upsertMessage(new)', messageHtml.length);
            return true;
        }

        try {
            const interaction = { id: key, domKey: key, role: mapped.role, parts: mapped.parts };
            _view.addInteraction(interaction);
            const top = chatContainer.lastElementChild;
            stampToolkitMsg(top, interaction);
            hoistAboveTrailingLoader(top);
            const inner = innerContent(top) || top;
            if (inner && inner.classList && inner.classList.contains('message')) addWipeAnimation(inner);
            finalizeMessageInsertion(inner, wasAtBottom);
            recordHtmlCache(key, messageHtml);
        } catch (err) {
            console.error('[JS] upsertMessage toolkit error:', err);
            return false;
        }
        noteRenderPerf(start, 'upsertMessage(new)', messageHtml.length);
        return true;
    });
}

/**
 * Removes the last persistent thinking/loading message if present.
 * @returns {boolean} True if a loader was found and removed
 */
function removeThinkingMessage() {
    try {
        const chatContainer = document.getElementById('chat-container');
        if (!chatContainer) return false;
        const loaders = Array.from(chatContainer.querySelectorAll('.message.loading'));
        if (loaders.length === 0) return false;
        const lastLoader = loaders[loaders.length - 1];
        // Raw loaders are direct children of the list; a loader found inside a
        // .cui-msg wrapper removes the whole toolkit message node instead.
        const victim = topOf(lastLoader);
        victim.parentElement && victim.parentElement.removeChild(victim);
        console.log('[JS] removeThinkingMessage: removed last loading bubble');
        return true;
    } catch (e) {
        console.warn('[JS] removeThinkingMessage error:', e);
        return false;
    }
}

/**
 * Adds a temporary loading bubble for a given role (defaults to assistant).
 * The bubble carries the 'loading' class so CSS shows a spinner via ::before.
 * Loading bubbles stay raw DOM children of the message list (no toolkit wrapper)
 * so removeThinkingMessage keeps its exact legacy semantics.
 * @param {string} role
 * @param {string} text
 */
function addLoadingMessage(role, text) {
    console.log('[JS] addLoadingMessage called, role:', role, 'text:', text);
    const { chatContainer, wasAtBottom } = getContainerWithBottom('addLoadingMessage');
    if (!chatContainer) return;
    role = (role || 'assistant').toLowerCase();
    const content = (text || 'Thinking…');
    const wrapper = document.createElement('div');

    // If role is 'loading' or falsy, create a generic loading message without role class
    if (!role || String(role).toLowerCase() === 'loading') {
        wrapper.className = 'message loading';
    } else {
        wrapper.className = `message ${role} loading`;
    }
    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';
    contentDiv.textContent = content;
    contentDiv.dataset.copyContent = content;
    wrapper.appendChild(contentDiv);
    insertAboveThinkingIfPresent(chatContainer, wrapper);
    console.log('[JS] addLoadingMessage: loading bubble added for role:', role);
    finalizeMessageInsertion(wrapper, wasAtBottom);
}

/**
 * Replaces the last message of a given role with provided HTML. If none exists, appends it.
 * @param {string} role - Role class to target (e.g., 'assistant', 'user', 'system', 'tool')
 * @param {string} messageHtml - Full message HTML (wrapper + content)
 * @returns {boolean} True if replacement/appended, false otherwise
 */
function replaceLastMessageByRole(role, messageHtml) {
    console.log('[JS] replaceLastMessageByRole called, role:', role, 'HTML length:', messageHtml ? messageHtml.length : 0);
    const { chatContainer, wasAtBottom } = getContainerWithBottom('replaceLastMessageByRole');
    if (!chatContainer) return false;
    const messages = Array.from(chatContainer.querySelectorAll(`.message.${role}`));
    console.log('[JS] replaceLastMessageByRole: found', messages.length, 'existing messages for role:', role);

    // Parse incoming HTML into an element
    const incoming = createNodeFromHtml(messageHtml, 'replaceLastMessageByRole');
    if (!incoming) return false;

    enqueueDomOp(() => {
        const start = (typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now();
        if (messages.length > 0) {
            const lastMessage = messages[messages.length - 1];
            // The .message element swaps inside its .cui-msg chrome, preserving the
            // toolkit wrapper; unwrapped nodes (fallback shell) replace directly.
            lastMessage.replaceWith(incoming);
        } else if (_view) {
            // Native-part bubbles (evidence/usage render no .message element):
            // replace the last .cui-msg chrome of that role instead. A keyed or
            // interaction-stamped victim goes through the toolkit so the keyed
            // pipeline keeps addressing it; anything else gets a raw swap.
            const msgs = Array.from(chatContainer.querySelectorAll(`.cui-msg[data-role="${role}"]`));
            const victim = msgs.length ? msgs[msgs.length - 1] : null;
            if (victim) {
                const byKeyRef = victim.dataset && (victim.dataset.key || victim.dataset.iid);
                const mapped = partsForHtml(messageHtml);
                if (byKeyRef) {
                    try {
                        const slot = victim.nextElementSibling;
                        _view.updateInteraction(byKeyRef, { parts: mapped.parts });
                        const top = chatContainer.lastElementChild;
                        stampToolkitMsg(top, { id: byKeyRef, domKey: victim.dataset.key });
                        if (slot && top !== slot) {
                            chatContainer.insertBefore(top, slot);
                        } else {
                            hoistAboveTrailingLoader(top);
                        }
                        finalizeMessageInsertion(innerContent(top) || top, wasAtBottom);
                        noteRenderPerf(start, 'replaceLastMessageByRole', messageHtml.length);
                        return true;
                    } catch (err) {
                        console.warn('[JS] replaceLastMessageByRole toolkit error:', err);
                    }
                }
                // Raw swap fallback: carry the domKey over so keyed upserts keep
                // finding the replacement node.
                setDatasetKeySafe(incoming, victim.dataset && victim.dataset.key);
                victim.replaceWith(incoming);
            } else {
                insertAboveThinkingIfPresent(chatContainer, incoming);
            }
        } else {
            insertAboveThinkingIfPresent(chatContainer, incoming);
        }

        finalizeMessageInsertion(incoming, wasAtBottom);
        noteRenderPerf(start, 'replaceLastMessageByRole', messageHtml.length);
        return true;
    });
}

/**
 * Processes code blocks for potential syntax highlighting
 */
function processCodeBlocks() {
    // Add copy icon for each code block
    document.querySelectorAll('pre').forEach(pre => {
        if (pre.querySelector('.copy-code-icon')) return;
        pre.style.position = 'relative';
        const button = document.createElement('button');
        button.className = 'copy-code-icon';
        button.type = 'button';
        button.title = 'Copy code';
        button.innerText = '📋';
        button.addEventListener('click', () => {
            const code = pre.querySelector('code');
            if (code) {
                const text = encodeURIComponent(code.innerText);
                console.debug("[chat-bridge] Triggering host copy", text);
                emitShEvent(`clipboard://copy?text=${text}`);
            }
        });
        pre.appendChild(button);
    });
}

/**
 * Processes links to make them open in a new window
 */
function processLinks() {
    // Make all links open in a new window
    const links = document.querySelectorAll('.message-content a');
    links.forEach(link => {
        link.setAttribute('target', '_blank');
        link.setAttribute('rel', 'noopener noreferrer');
    });
}

/**
 * Scrolls the message list to the bottom (unconditional — host-compat global).
 */
function scrollToBottom() {
    try {
        if (_view) {
            _view.scrollToBottom(true);
            return;
        }
    } catch { /* fall through to DOM scroll */ }
    const chatContainer = document.getElementById('chat-container');
    if (chatContainer && typeof chatContainer.scrollTop === 'number') {
        chatContainer.scrollTop = chatContainer.scrollHeight;
    } else {
        window.scrollTo(0, document.body.scrollHeight);
    }
}

/**
 * Determines whether the chat container is scrolled to (or near) the bottom.
 * @param {HTMLElement} container
 * @param {number} threshold Pixels threshold to still consider as bottom (default 30)
 */
function isAtBottom(container, threshold = SCROLL_BOTTOM_THRESHOLD) {
    if (!container) return true;
    const distanceFromBottom = container.scrollHeight - (container.scrollTop + container.clientHeight);
    return distanceFromBottom <= threshold;
}

/**
 * Shows the "new messages" affordance. The toolkit renders its own jump button;
 * a non-forced scroll request reveals it whenever the user is not pinned.
 */
function showNewMessagesIndicator() {
    try { if (_view) _view.scrollToBottom(false); } catch {}
}

/**
 * Hides the new messages indicator (toolkit hides the jump button when pinned).
 */
function hideNewMessagesIndicator() {
    // No-op: the toolkit's .cui-jump button visibility is driven by scroll state.
}

/**
 * Re-evaluates scroll affordances after external DOM changes (kept for parity;
 * the toolkit tracks pin state on its own scroll listener).
 */
function updateScrollControls() {
    try { if (_view) _view.scrollToBottom(false); } catch {}
}

/**
 * Finalizes a message insertion by reprocessing dynamic features and deciding auto-scroll behavior.
 * @param {HTMLElement} rootNode - The root node of the newly added/replaced content.
 * @param {boolean} wasAtBottom - Whether the chat was at bottom before the DOM change.
 */
function finalizeMessageInsertion(rootNode, wasAtBottom) {
    try {
        // Re-process dynamic features
        processCodeBlocks();
        processLinks();
        setupMetricsTooltip();
        if (rootNode) {
            if (typeof setupCollapsibleHandlers === 'function') {
                setupCollapsibleHandlers(rootNode);
            } else {
                console.warn('[JS] finalizeMessageInsertion: setupCollapsibleHandlers is not defined');
            }
        }
    } finally {
        // Auto-scroll decision delegated to the toolkit: force when the user was
        // pinned at bottom, otherwise reveal the jump button via a non-forced call.
        try {
            if (_view) {
                _view.scrollToBottom(wasAtBottom === true);
            } else if (wasAtBottom) {
                scrollToBottom();
            } else {
                showNewMessagesIndicator();
            }
        } catch (e) {
            console.warn('[JS] finalizeMessageInsertion scroll error:', e);
        }
    }
}

/**
 * Sets up collapse/expand handlers for system/tool messages under the given root node.
 * - Shows the header-integrated chevron only when content overflows the collapsed height.
 * - Toggles `.expanded` on the `.message` element and updates aria-expanded.
 * - Supports mouse click and keyboard (Enter/Space) on the chevron.
 * - Clicking the header (excluding the chevron) also toggles for convenience.
 * @param {HTMLElement} rootNode
 */
function setupCollapsibleHandlers(rootNode) {
    try {
        const scope = rootNode && rootNode.classList && rootNode.classList.contains('message')
            ? [rootNode]
            : Array.from((rootNode || document).querySelectorAll('.message.tool, .message.system, .message.summary'));

        scope.forEach(msg => {
            if (!msg || (msg.dataset && msg.dataset.collapsibleBound === '1')) return;

            // Only applicable to tool/system/summary messages
            if (!(msg.classList.contains('tool') || msg.classList.contains('system') || msg.classList.contains('summary'))) return;

            const btn = msg.querySelector('.toggle-arrow');
            const content = msg.querySelector('.message-content');
            const header = msg.querySelector('.message-header');
            if (!btn || !content) return;

            // Link button to content for a11y
            if (!content.id) {
                content.id = 'mc-' + Math.random().toString(36).slice(2, 10);
            }
            btn.setAttribute('aria-controls', content.id);

            const refresh = () => {
                try {
                    const overflow = content.scrollHeight > (content.clientHeight + 2);
                    if (!overflow) {
                        // If not overflowing, hide toggle and mark as expanded to remove gradient overlay
                        btn.style.display = 'none';
                        msg.classList.add('expanded');
                        btn.setAttribute('aria-expanded', 'true');
                    } else {
                        btn.style.display = '';
                        const expanded = msg.classList.contains('expanded');
                        btn.setAttribute('aria-expanded', expanded ? 'true' : 'false');
                    }
                } catch (e) {
                    console.warn('[JS] setupCollapsibleHandlers.refresh error:', e);
                }
            };

            const toggle = () => {
                const expanded = msg.classList.toggle('expanded');
                btn.setAttribute('aria-expanded', expanded ? 'true' : 'false');
            };

            // Click + keyboard on chevron
            btn.addEventListener('click', toggle);
            btn.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    toggle();
                }
            });

            // Convenience: click on header toggles (ignore direct chevron clicks)
            if (header) {
                header.addEventListener('click', (e) => {
                    if (e.target && e.target.closest && e.target.closest('.toggle-arrow')) return;
                    toggle();
                });
            }

            // Messages that contain images start expanded so the image is visible
            if (content.querySelector('img')) {
                msg.classList.add('expanded');
            }

            // Initial state
            refresh();

            // Recompute on next tick for accurate measurements after layout
            setTimeout(refresh, 0);

            msg.dataset.collapsibleBound = '1';
        });

        // One-time global resize observer to refresh visibility
        if (!window._shCollapsibleResizeBound) {
            window.addEventListener('resize', () => {
                try {
                    document.querySelectorAll('.message.tool, .message.system, .message.summary').forEach(msg => {
                        const btn = msg.querySelector('.toggle-arrow');
                        const content = msg.querySelector('.message-content');
                        if (!btn || !content) return;
                        const overflow = content.scrollHeight > (content.clientHeight + 2);
                        if (!overflow) {
                            btn.style.display = 'none';
                            msg.classList.add('expanded');
                            btn.setAttribute('aria-expanded', 'true');
                        } else {
                            btn.style.display = '';
                            const expanded = msg.classList.contains('expanded');
                            btn.setAttribute('aria-expanded', expanded ? 'true' : 'false');
                        }
                    });
                } catch (e) {
                    console.warn('[JS] collapsible resize update error:', e);
                }
            });
            window._shCollapsibleResizeBound = true;
        }
    } catch (err) {
        console.error('[JS] setupCollapsibleHandlers error:', err);
    }
}

/**
 * Handles tooltip creation and positioning for message metrics
 */
function setupMetricsTooltip() {
    // Find all metrics icons
    const metricIcons = document.querySelectorAll('.metrics-icon');

    metricIcons.forEach(icon => {
        // Remove any previous event listeners
        icon.removeEventListener('mouseenter', showTooltip);
        icon.removeEventListener('mouseleave', hideTooltip);

        // Add event listeners
        icon.addEventListener('mouseenter', showTooltip);
        icon.addEventListener('mouseleave', hideTooltip);
    });
}

/**
 * Shows the metrics tooltip
 * @param {Event} event - The mouse event
 */
function showTooltip(event) {
    // Remove any existing tooltips
    hideAllTooltips();

    const icon = event.target;
    const inTokens = icon.getAttribute('data-in');
    const outTokens = icon.getAttribute('data-out');
    const provider = icon.getAttribute('data-provider');
    const model = icon.getAttribute('data-model');
    const reason = icon.getAttribute('data-reason');
    const contextUsage = icon.getAttribute('data-context-usage');

    // Create tooltip element
    const tooltip = document.createElement('div');
    tooltip.className = 'metrics-tooltip';
    const providerDiv = document.createElement('div');
    providerDiv.innerHTML = '<strong>Provider:</strong> ';
    providerDiv.appendChild(document.createTextNode(provider || 'Unknown'));

    const modelDiv = document.createElement('div');
    modelDiv.innerHTML = '<strong>Model:</strong> ';
    modelDiv.appendChild(document.createTextNode(model || 'Unknown'));

    const inTokensDiv = document.createElement('div');
    inTokensDiv.innerHTML = '<strong>Tokens In:</strong> ';
    inTokensDiv.appendChild(document.createTextNode(inTokens || '0'));

    const outTokensDiv = document.createElement('div');
    outTokensDiv.innerHTML = '<strong>Tokens Out:</strong> ';
    outTokensDiv.appendChild(document.createTextNode(outTokens || '0'));

    const reasonDiv = document.createElement('div');
    reasonDiv.innerHTML = '<strong>Finish Reason:</strong> ';
    reasonDiv.appendChild(document.createTextNode(reason || 'Unknown'));

    tooltip.appendChild(providerDiv);
    tooltip.appendChild(modelDiv);
    tooltip.appendChild(inTokensDiv);
    tooltip.appendChild(outTokensDiv);
    tooltip.appendChild(reasonDiv);

    // Add context usage if available
    if (contextUsage) {
        const contextDiv = document.createElement('div');
        contextDiv.innerHTML = '<strong>Context Usage:</strong> ';
        contextDiv.appendChild(document.createTextNode(contextUsage));
        tooltip.appendChild(contextDiv);
    }

    // Position tooltip
    const iconRect = icon.getBoundingClientRect();
    tooltip.style.left = `${iconRect.left}px`;

    // Add tooltip and compute top so its bottom aligns with icon bottom
    document.body.appendChild(tooltip);
    const tooltipRect = tooltip.getBoundingClientRect();
    // Position tooltip: bottom of tooltip == bottom of icon
    let tooltipTop = iconRect.bottom - tooltipRect.height;
    // If tooltip would be off-screen at top, place it below icon
    if (tooltipTop < 0) {
        tooltipTop = iconRect.bottom + 5;
    }
    tooltip.style.top = `${tooltipTop}px`;
}

/**
 * Hides all metrics tooltips
 */
function hideTooltip() {
    hideAllTooltips();
}

/**
 * Removes all tooltips from the document
 */
function hideAllTooltips() {
    const tooltips = document.querySelectorAll('.metrics-tooltip');
    tooltips.forEach(tooltip => tooltip.remove());
}

/**
 * Shows a temporary toast message at the bottom of the screen.
 * @param {string} message - The message to display
 */
function showToast(message) {
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.innerText = message;
    document.body.appendChild(toast);
    // Trigger show animation
    setTimeout(() => toast.classList.add('visible'), 100);
    // Hide after 2 seconds
    setTimeout(() => {
        toast.classList.remove('visible');
        setTimeout(() => document.body.removeChild(toast), 300);
    }, 2000);
}

// Disable the browser context menu (e.g. WebView2's "Refresh"). Reloading the
// page would wipe the chat UI, since messages are rendered into the DOM by the
// host and are not part of the document itself.
document.addEventListener('contextmenu', function (e) {
    e.preventDefault();
});

// ---------------------------------------------------------------------------
// Host-controlled helpers (C# -> JS compat globals)
// ---------------------------------------------------------------------------

function setStatus(text) {
    console.log('[JS] setStatus called with text:', text);
    if (_view) {
        try { _view.setStatus(text || ''); } catch (e) { console.warn('[JS] setStatus toolkit error:', e); }
        // The toolkit writes .cui-status textContent, which wipes child nodes —
        // re-append the spinner element so it survives every status update.
        try {
            if (_els.status && _els.spinner) _els.status.appendChild(_els.spinner);
        } catch { /* ignore */ }
        return;
    }
    // Fallback shell (no toolkit): legacy #status-text element.
    const el = document.getElementById('status-text');
    if (el) {
        el.textContent = text || '';
    } else {
        console.error('[JS] setStatus: status element not found');
    }
}

function setProcessing(on) {
    console.log('[JS] setProcessing called with value:', on);
    if (_view) {
        try {
            if (_els.root) {
                // Session queue (features.queue): keep the toolkit's internal
                // processing flag unset so the composer stays editable mid-turn —
                // sends become host-side queue entries. The send/cancel swap is
                // driven by the root's data-processing attribute instead, and the
                // send button stays visible relabeled "Queue" (chat-styles.css).
                _els.root.dataset.processing = on ? 'true' : 'false';
            } else {
                _view.setProcessing(!!on);
            }
        } catch (e) { console.warn('[JS] setProcessing toolkit error:', e); }
    }

    const spinner = _els.spinner || document.getElementById('spinner');
    const input = _els.input || document.getElementById('user-input');
    const sendBtn = _els.send || document.getElementById('send-button');
    const cancelBtn = _els.cancel || document.getElementById('cancel-button');

    if (spinner) {
        spinner.classList.toggle('hidden', !on);
    } else if (!_view) {
        console.error('[JS] setProcessing: spinner element not found');
    }

    // Processing end is the authoritative run-finished signal — freeze the autonomy
    // overlay (last push may still report running) and start its hide countdown.
    if (!on) {
        try { freezeAutonomyOverlay(); } catch (e) {
            console.warn('[JS] setProcessing: freezeAutonomyOverlay threw', e);
        }
    }

    // Processing start is the authoritative new-run signal — reset the autonomy overlay
    // so leftover state (and any stale last-run snapshot pushed before the session's run
    // state resets) can never paint previous-run values during the new run.
    if (on) {
        try {
            _autonomyAwaitingRun = true;
            hideAutonomyOverlay();
        } catch (e) {
            console.warn('[JS] setProcessing: hideAutonomyOverlay threw', e);
        }
    }

    // Fail-safe: when processing stops, ensure any lingering loading bubble is removed
    if (!on && typeof removeThinkingMessage === 'function') {
        try {
            const removed = removeThinkingMessage();
            if (removed) {
                console.log('[JS] setProcessing: removed lingering loading bubble');
            }
        } catch (e) {
            console.warn('[JS] setProcessing: removeThinkingMessage threw', e);
        }
    }

    // Toggle controls according to processing state
    // When processing: queue type-ahead keeps input + send editable (relabeled
    // "Queue") and enables cancel; when idle: send is "Send", cancel disabled.
    // (the toolkit swaps send/cancel visibility via [data-processing]; CSS keeps
    // the send button visible while processing so queued sends stay one click away)
    try {
        if (_view) {
            // The composer never locks — mid-turn sends enqueue host-side. The
            // internal processing flag is left unset (see above) so
            // applyComposerState keeps input/send/attach editable.
            _view.setComposerEnabled(true);
            if (_els.send) _els.send.textContent = on ? 'Queue' : 'Send';
        } else {
            // Fallback shell: legacy raw DOM toggles.
            if (input) input.disabled = !!on;
            if (sendBtn) sendBtn.disabled = !!on;
            if (input) {
                try {
                    input.setAttribute('aria-disabled', input.disabled ? 'true' : 'false');
                } catch {}
            }
        }

        // The toolkit only toggles .cui-cancel visibility via [data-processing]
        // — its disabled flag stays host-managed in both shells so a stale
        // state can never leave the stop button dead mid-run.
        if (cancelBtn) cancelBtn.disabled = !on;

        // Optional: reflect disabled state via CSS class and ARIA for better a11y
        [sendBtn, cancelBtn].forEach(btn => {
            if (!btn) return;
            try {
                btn.classList.toggle('disabled', !!btn.disabled);
                btn.setAttribute('aria-disabled', btn.disabled ? 'true' : 'false');
            } catch {}
        });
    } catch (err) {
        console.warn('[JS] setProcessing: control toggle failed', err);
    }
}

/**
 * Pushes the session's outgoing queue into the toolkit queue panel
 * (features.queue). Each item is a QueuedMessage {id, text, createdAt?, meta?};
 * meta.agent (or meta.author) renders as a muted byline — used for
 * agent-injected entries such as per-worker pending instructions.
 * The host owns the queue; this only re-renders it.
 * @param {Array<object>} items
 */
function setQueue(items) {
    if (!_view) {
        console.warn('[JS] setQueue: chat view not ready');
        return;
    }
    try {
        _view.setQueue(Array.isArray(items) ? items : []);
    } catch (err) {
        console.error('[JS] setQueue error:', err);
    }
}

// Attachment helpers — called by the host via ExecuteScript.
// The host owns the attachment bytes; toolkit-staged ChatAttachment items are
// visual chips only, correlated by the pending id inside meta.
function addAttachmentChip(id, name, dataUri) {
    if (!_view) {
        console.warn('[JS] addAttachmentChip: chat view not ready');
        return;
    }
    try {
        const mimeMatch = /^data:([^;,]+)/.exec(dataUri || '');
        _view.stageAttachments([{
            kind: 'image',
            name: name || 'attachment',
            url: dataUri || '',
            mimeType: mimeMatch ? mimeMatch[1] : 'image/png',
            meta: { id: id },
        }]);
    } catch (err) {
        console.error('[JS] addAttachmentChip error:', err);
    }
}

function removeAttachmentChip(id) {
    if (!_view) {
        console.warn('[JS] removeAttachmentChip: chat view not ready');
        return;
    }
    try {
        const keep = _view.getAttachments().filter(a => !(a && a.meta && a.meta.id === id));
        _view.clearAttachments();
        if (keep.length) _view.stageAttachments(keep);
    } catch (err) {
        console.error('[JS] removeAttachmentChip error:', err);
    }
}

function clearAttachments() {
    try { if (_view) _view.clearAttachments(); } catch (err) {
        console.error('[JS] clearAttachments error:', err);
    }
}

/**
 * Feeds the native composer model picker (features.modelPicker). Entries are
 * {id, label?, hint?} objects or bare strings; `selected` is the current model
 * id. Changes emit sh://event?type=model&model=<id> — the host route is opt-in
 * (today's host logs it as an unknown event type until C# wires it).
 * @param {Array<string|object>} models
 * @param {string} [selected]
 */
function setModels(models, selected) {
    if (!_view) {
        console.warn('[JS] setModels: chat view not ready');
        return;
    }
    try {
        const normalized = (Array.isArray(models) ? models : [])
            .filter(m => m != null)
            .map(m => typeof m === 'string' ? { id: m } : m)
            .filter(m => m && typeof m.id === 'string' && m.id !== '');
        _view.setModels(normalized, selected);
    } catch (err) {
        console.error('[JS] setModels error:', err);
    }
}

function setAttachEnabled(enabled, tooltip) {
    const btn = _els.attach || document.getElementById('attach-button');
    if (!btn) return;
    btn.disabled = !enabled;
    if (tooltip) btn.title = tooltip;
}

// Autonomy overlay — floating card showing live autonomous-run consumption (elapsed time and
// provider tokens versus the configured limits). Visible only while a run is in progress;
// lingers briefly when a budget is exhausted so the limit state can be read.
let _autonomy = null;
let _autonomyTimer = null;
let _autonomyHideTimer = null;
// True from run start until the first running snapshot arrives. While set, a not-running
// usage push is a stale snapshot of the previous run (the run-start push reads session
// autonomy state before BeginAutonomyRun resets it) and must never paint.
let _autonomyAwaitingRun = false;

const AUTONOMY_FIRST_PAINT_MS = 15000;     // long-running turns still surface the overlay even with few interactions
const AUTONOMY_MIN_INTERACTIONS = 10;      // don't flash the overlay for runs that produce little
const AUTONOMY_HIDE_MS = 1200;             // normal completion: hide shortly after the run ends
const AUTONOMY_EXHAUSTED_LINGER_MS = 6000; // keep the "limit reached" state readable

function updateAutonomyUsage(usage) {
    if (!usage) return;
    // Drop snapshots that cannot belong to a live run: a not-running state arriving before the
    // run's first live snapshot (stale last-run push at run start — the session's autonomy
    // state is only reset once the turn loop begins) or with no run state at all. Painting it
    // would show last run's values until the first meaningful render (~15 s in).
    if (!usage.IsRunning && (_autonomyAwaitingRun || !_autonomy)) return;
    if (usage.IsRunning) _autonomyAwaitingRun = false;
    _autonomy = {
        elapsedSec: usage.ElapsedSeconds || 0,
        maxSec: usage.MaxSeconds || 0,
        tokens: usage.Tokens || 0,
        maxTokens: usage.MaxTokens || 0,
        tokensEstimated: !!usage.TokensEstimated,
        interactions: usage.Interactions || 0,
        running: !!usage.IsRunning,
        exhausted: !!usage.IsExhausted,
        receivedAt: Date.now()
    };
    renderAutonomyOverlay();
    if (_autonomy.running) {
        cancelAutonomyHide();
        if (!_autonomyTimer) _autonomyTimer = setInterval(renderAutonomyOverlay, 1000);
    } else {
        stopAutonomyTick();
        scheduleAutonomyHide();
    }
}

function renderAutonomyOverlay() {
    const el = document.getElementById('autonomy-overlay');
    if (!el || !_autonomy) return;
    const elapsed = _autonomy.elapsedSec + (_autonomy.running ? (Date.now() - _autonomy.receivedAt) / 1000 : 0);

    // Delayed first paint: the overlay appears once the run has produced enough interactions
    // or run long enough to be meaningful, so short-lived runs do not flash metrics.
    // Exhaustion is always surfaced.
    const meaningful = _autonomy.exhausted || _autonomy.interactions >= AUTONOMY_MIN_INTERACTIONS ||
        elapsed * 1000 >= AUTONOMY_FIRST_PAINT_MS;

    // Mirror the binding budget constraint into the toolkit context card while
    // the overlay is meaningful (bounded runs only — an unbounded run has no
    // percent to show, so the card is hidden).
    try {
        if (_view) {
            const timePct = _autonomy.maxSec > 0 ? elapsed / _autonomy.maxSec : 0;
            const tokPct = _autonomy.maxTokens > 0 ? _autonomy.tokens / _autonomy.maxTokens : 0;
            const pct = Math.round(Math.max(timePct, tokPct) * 100);
            if (meaningful && (_autonomy.maxSec > 0 || _autonomy.maxTokens > 0)) {
                _view.setContextUsage({ percent: pct, elapsedMs: Math.round(elapsed * 1000) });
            } else {
                _view.setContextUsage(null);
            }
        }
    } catch { /* context card is best-effort */ }

    if (!meaningful) return;

    const timeRow = document.getElementById('autonomy-time-row');
    const timeText = document.getElementById('autonomy-time-text');
    const timeFill = document.getElementById('autonomy-time-fill');
    if (timeText) {
        timeText.textContent = fmtAutonomyDuration(elapsed) +
            (_autonomy.maxSec > 0 ? ' / ' + fmtAutonomyDuration(_autonomy.maxSec) : '');
    }
    if (timeFill) {
        timeFill.style.width = _autonomy.maxSec > 0 ? Math.min(100, elapsed / _autonomy.maxSec * 100) + '%' : '0%';
    }
    if (timeRow) {
        timeRow.classList.toggle('autonomy-limit-hit', _autonomy.maxSec > 0 && elapsed >= _autonomy.maxSec);
    }

    const tokRow = document.getElementById('autonomy-tokens-row');
    const tokText = document.getElementById('autonomy-tokens-text');
    const tokFill = document.getElementById('autonomy-tokens-fill');
    if (tokText) {
        // '~' marks heuristic estimates (provider did not report usage for at least one call)
        const tokPrefix = _autonomy.tokensEstimated ? '~' : '';
        tokText.textContent = tokPrefix + fmtAutonomyTokens(_autonomy.tokens) +
            (_autonomy.maxTokens > 0 ? ' / ' + fmtAutonomyTokens(_autonomy.maxTokens) : ' tok');
        tokText.title = _autonomy.tokensEstimated ? 'Estimated token usage' : '';
    }
    if (tokFill) {
        tokFill.style.width = _autonomy.maxTokens > 0 ? Math.min(100, _autonomy.tokens / _autonomy.maxTokens * 100) + '%' : '0%';
    }
    if (tokRow) {
        tokRow.classList.toggle('autonomy-limit-hit', _autonomy.maxTokens > 0 && _autonomy.tokens >= _autonomy.maxTokens);
    }

    // Unbounded setup: both limits disabled — surface a warning instead of implying a bound.
    const unlimited = _autonomy.maxSec <= 0 && _autonomy.maxTokens <= 0;
    const warn = document.getElementById('autonomy-warning');
    if (warn) warn.classList.toggle('hidden', !unlimited);
    el.classList.toggle('autonomy-unlimited', unlimited);

    // Extend row: meaningless when unbounded; disabled once the run ended or a budget is
    // already exhausted (extending a finished run cannot resume it).
    const extendRow = document.getElementById('autonomy-extend-row');
    const extendBtn = document.getElementById('autonomy-extend-btn');
    if (extendRow) extendRow.classList.toggle('hidden', unlimited);
    if (extendBtn) extendBtn.disabled = !_autonomy.running || _autonomy.exhausted;

    el.classList.toggle('autonomy-exhausted', _autonomy.exhausted);
    el.classList.remove('hidden');
}

function stopAutonomyTick() {
    if (_autonomyTimer) {
        clearInterval(_autonomyTimer);
        _autonomyTimer = null;
    }
}

function cancelAutonomyHide() {
    if (_autonomyHideTimer) {
        clearTimeout(_autonomyHideTimer);
        _autonomyHideTimer = null;
    }
}

function scheduleAutonomyHide() {
    cancelAutonomyHide();
    const linger = _autonomy && _autonomy.exhausted ? AUTONOMY_EXHAUSTED_LINGER_MS : AUTONOMY_HIDE_MS;
    _autonomyHideTimer = setTimeout(hideAutonomyOverlay, linger);
}

// Called from setProcessing(false). The last usage push can arrive while the run is still
// marked running (the session clock stops in the iterator's finally, after the final
// notification), so processing end is the authoritative signal to freeze and schedule hide.
function freezeAutonomyOverlay() {
    if (!_autonomy) return;
    _autonomy.running = false;
    stopAutonomyTick();
    renderAutonomyOverlay();
    scheduleAutonomyHide();
}

function hideAutonomyOverlay() {
    _autonomy = null;
    stopAutonomyTick();
    cancelAutonomyHide();
    try { if (_view) _view.setContextUsage(null); } catch {}
    const el = document.getElementById('autonomy-overlay');
    if (el) el.classList.add('hidden');
}

function fmtAutonomyDuration(sec) {
    sec = Math.max(0, Math.floor(sec));
    const h = Math.floor(sec / 3600);
    const m = Math.floor((sec % 3600) / 60);
    const s = sec % 60;
    const ss = String(s).padStart(2, '0');
    return h > 0 ? h + ':' + String(m).padStart(2, '0') + ':' + ss : m + ':' + ss;
}

function fmtAutonomyTokens(n) {
    if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
    if (n >= 10000) return Math.round(n / 1000) + 'k';
    if (n >= 1000) return (n / 1000).toFixed(1) + 'k';
    return String(n);
}

function resetMessages() {
    console.log('[JS] resetMessages called');
    const chatContainer = document.getElementById('chat-container');
    if (!chatContainer) {
        console.error('[JS] resetMessages: chat-container element not found');
        return;
    }
    // The toolkit clears its keyed message index and empties the list; plugin
    // cards and loading bubbles are raw children and go with it.
    try {
        if (_view) {
            _view.clearMessages();
        } else {
            chatContainer.innerHTML = '';
        }
    } catch (err) {
        console.warn('[JS] resetMessages clear error:', err);
        try { chatContainer.innerHTML = ''; } catch {}
    }
    hideAutonomyOverlay();
    hideTaskPlanPanel();
    clearSuggestedPrompts();
    _pluginCards.clear();

    try {
        _templateCache.clear();
        _htmlLru.clear();
        _pendingOps.length = 0;
        _flushScheduled = false;
    } catch {
        // ignore
    }

    console.log('[JS] resetMessages: cleared messages and caches');
}

/**
 * Returns an array of all message keys currently in the DOM (document order).
 * Keyed nodes are `.message` bubbles and `.cui-part-html` wrappers stamped by
 * the html part renderer; unkeyed bubbles and cards are skipped.
 * @returns {string[]} Array of message keys (dataset.key values)
 */
function getAllMessageKeys() {
    try {
        const chatContainer = document.getElementById('chat-container');
        if (!chatContainer) return [];

        const seen = new Set();
        const keys = [];
        // Keyed nodes are the stamped part roots (.message / .cui-part-html) or
        // the .cui-msg chrome for native-part interactions (evidence etc.) that
        // carry no stamped inner element. The .cui-msg ancestor sorts first in
        // document order, so each key is recorded once.
        const parts = chatContainer.querySelectorAll('[data-key]');
        for (const el of parts) {
            const k = el.dataset && el.dataset.key;
            if (k && !seen.has(k)) {
                seen.add(k);
                keys.push(k);
            }
        }
        return keys;
    } catch (err) {
        console.error('[JS] getAllMessageKeys error:', err);
        return [];
    }
}

// Copy handler: only override when one or more FULL messages are selected
document.addEventListener('copy', function(e) {
    const sel = window.getSelection();
    if (!sel || sel.isCollapsed) return;             // nothing selected

    const text = sel.toString().trim();
    if (!text) return;                               // only whitespace

    // 1) grab all message‐content elements (ignores header/footer)
    const msgs = Array.from(
      document.querySelectorAll('.message-content')
    );
    if (msgs.length === 0) return;

    // 2) wrap the user’s selection as a single Range
    const range = sel.getRangeAt(0);

    // 3) find any message‐content DIVs the selection even *touches*
    const touched = msgs.filter(msg => range.intersectsNode(msg));
    if (touched.length === 0) return;                // selection outside chat

    // 4) of those, which are fully contained?
    const fully = touched.filter(msg => {
      const contentRange = document.createRange();
      contentRange.selectNodeContents(msg);
      return (
        range.compareBoundaryPoints(Range.START_TO_START, contentRange) <= 0 &&
        range.compareBoundaryPoints(Range.END_TO_END,   contentRange) >= 0
      );
    });

    // 5) if none are fully contained, fall back to default copy
    if (fully.length === 0) return;

    // 6) otherwise, build a clipboard string by pulling each DIV’s data-copy-content
    const out = touched
      .map(m => m.dataset.copyContent || '')
      .filter(Boolean)
      .join('\n\n');

    if (out) {
      e.clipboardData.setData('text/plain', out);
      e.preventDefault();
    }
  });

// ---------------------------------------------------------------------------
// Plugin cards — native chat-ui parts/cards in the message list. The legacy
// DOM implementations below stay as the no-toolkit fallback shell.
// ---------------------------------------------------------------------------

function showPlanConsent(plan) {
    const container = document.getElementById('chat-container');
    if (!container || !plan || !plan.id) return;

    if (_view) {
        // Native card:approval — Approve/Reject reports back through
        // transport.messageAction -> sh://event?type=consent.
        const iid = `consent-${plan.id}`;
        if (_pluginCards.has(iid)) return; // same dedupe as the legacy card
        pushPluginInteraction(iid, plan.id, 'consent', 'assistant', [{
            type: 'card',
            kind: 'approval',
            data: {
                action: 'consent',
                state: 'pending',
                title: plan.goal || 'Proposed plan',
                description: composePlanDescription(plan),
                approveLabel: 'Approve plan',
                rejectLabel: 'Reject',
            },
        }]);
        scrollToBottom();
        return;
    }

    const existing = Array.from(document.querySelectorAll('.plan-consent-card'))
        .find(node => node.dataset.consentId === plan.id);
    if (existing) return;

    const card = document.createElement('section');
    card.className = 'plan-consent-card';
    card.dataset.consentId = plan.id;

    const title = document.createElement('h3');
    title.textContent = plan.goal || 'Proposed plan';
    card.appendChild(title);

    const summary = document.createElement('p');
    summary.textContent = plan.summary || '';
    card.appendChild(summary);

    const list = document.createElement('ol');
    (plan.steps || []).forEach(step => {
        const item = document.createElement('li');
        item.textContent = step.description || step.id || 'Step';
        if (step.tool) {
            const tool = document.createElement('code');
            tool.textContent = ` ${step.tool}${step.mutatesCanvas ? ' (canvas change)' : ''}`;
            item.appendChild(tool);
        }
        list.appendChild(item);
    });
    card.appendChild(list);

    const appendDetails = (label, values) => {
        if (!values || values.length === 0) return;
        const heading = document.createElement('strong');
        heading.textContent = label;
        card.appendChild(heading);
        const details = document.createElement('ul');
        values.forEach(value => {
            const item = document.createElement('li');
            item.textContent = value;
            details.appendChild(item);
        });
        card.appendChild(details);
    };
    appendDetails('Assumptions', plan.assumptions);
    appendDetails('Success criteria', plan.successCriteria);

    const note = document.createElement('p');
    note.className = 'plan-consent-note';
    note.textContent = 'Approving this approach does not approve canvas changes. Each concrete change receives a graphical review.';
    card.appendChild(note);

    const actions = document.createElement('div');
    actions.className = 'plan-consent-actions';
    const reject = document.createElement('button');
    reject.type = 'button';
    reject.textContent = 'Reject';
    reject.addEventListener('click', () => {
        emitShEvent(`sh://event?type=consent&id=${encodeURIComponent(plan.id)}&decision=reject`);
    });
    const approve = document.createElement('button');
    approve.type = 'button';
    approve.textContent = 'Approve plan';
    approve.addEventListener('click', () => {
        emitShEvent(`sh://event?type=consent&id=${encodeURIComponent(plan.id)}&decision=approve`);
    });
    actions.appendChild(reject);
    actions.appendChild(approve);
    card.appendChild(actions);
    insertAboveThinkingIfPresent(container, card);
    scrollToBottom();
}

function resolvePlanConsent(id) {
    if (_view) {
        freezePluginCard(`consent-${id}`);
        return;
    }
    const card = Array.from(document.querySelectorAll('.plan-consent-card'))
        .find(node => node.dataset.consentId === id);
    if (!card) return;
    card.classList.add('resolved');
    card.querySelectorAll('button').forEach(button => { button.disabled = true; });
}

// Renders the current task plan. Toolkit shell: a card:task_plan interaction in
// the message list — one plan at a time (same-key upsert, like the HUD panel
// before it); the final state stays visible until the chat is reset.
// Fallback shell: the HUD panel docked below the autonomy overlay.
function updateTaskPlan(plan) {
    if (_view && plan && plan.id) {
        const tasks = Array.isArray(plan.tasks) ? plan.tasks : [];
        const statusMap = { in_progress: 'active', completed: 'done' };
        const steps = tasks.map(task => ({
            status: statusMap[task.status] || 'pending',
            label: task.description || task.id || 'Task',
        }));
        const completed = tasks.filter(task => task.status === 'completed').length;
        const title = (plan.goal || 'Task plan') + (tasks.length ? ` (${completed}/${tasks.length})` : '');
        pushPluginInteraction('sh-task-plan', plan.id, 'task_plan', 'system', [{
            type: 'card',
            kind: 'task_plan',
            data: { title, steps },
        }]);
        return;
    }

    const panel = document.getElementById('task-plan-panel');
    if (!panel || !plan || !plan.id) return;

    let card = Array.from(panel.querySelectorAll('.task-plan-card'))
        .find(node => node.dataset.planId === plan.id);
    if (!card) {
        panel.innerHTML = '';
        card = document.createElement('section');
        card.className = 'task-plan-card';
        card.dataset.planId = plan.id;
        panel.appendChild(card);
        panel.classList.remove('hidden');
    } else {
        card.innerHTML = '';
    }

    const tasks = Array.isArray(plan.tasks) ? plan.tasks : [];
    const completed = tasks.filter(task => task.status === 'completed').length;

    const header = document.createElement('div');
    header.className = 'task-plan-header';
    const title = document.createElement('h3');
    title.textContent = plan.goal || 'Task plan';
    header.appendChild(title);
    const progressLabel = document.createElement('span');
    progressLabel.className = 'task-plan-progress-label';
    progressLabel.textContent = `${completed}/${tasks.length}`;
    header.appendChild(progressLabel);
    card.appendChild(header);

    const bar = document.createElement('div');
    bar.className = 'task-plan-progress';
    const fill = document.createElement('div');
    fill.className = 'task-plan-progress-fill';
    fill.style.width = tasks.length > 0 ? `${Math.round((completed / tasks.length) * 100)}%` : '0%';
    bar.appendChild(fill);
    card.appendChild(bar);

    const list = document.createElement('ul');
    list.className = 'task-plan-tasks';
    tasks.forEach(task => {
        const status = task.status === 'in_progress' || task.status === 'completed' ? task.status : 'pending';
        const item = document.createElement('li');
        item.className = `task-plan-task status-${status.replace('_', '-')}`;

        const icon = document.createElement('span');
        icon.className = 'task-plan-icon';
        icon.textContent = status === 'completed' ? '✓' : status === 'in_progress' ? '▶' : '○';
        item.appendChild(icon);

        const text = document.createElement('span');
        text.className = 'task-plan-text';
        text.textContent = task.description || task.id || 'Task';
        item.appendChild(text);

        list.appendChild(item);
    });
    card.appendChild(list);
}

function hideTaskPlanPanel() {
    try { if (_view) _view.removeMessage('sh-task-plan'); } catch {}
    _pluginCards.delete('sh-task-plan');
    const panel = document.getElementById('task-plan-panel');
    if (!panel) return;
    panel.innerHTML = '';
    panel.classList.add('hidden');
}

/**
 * Renders suggested follow-up prompt chips. Toolkit shell: a `suggestions`
 * part interaction at the end of the list — chips report `suggestion` through
 * messageAction, which fills the composer without sending (same semantics as
 * the legacy strip). Fallback shell: the strip above the input bar.
 * @param {string[]} suggestions - 0-3 prompt strings
 */
function showSuggestedPrompts(suggestions) {
    const items = Array.isArray(suggestions) ? suggestions.filter(s => typeof s === 'string' && s.trim()).slice(0, 3) : [];

    if (_view) {
        if (items.length === 0) {
            clearSuggestedPrompts();
            return;
        }
        pushPluginInteraction('sh-suggestions', 'sh-suggestions', 'suggestions', 'assistant',
            [{ type: 'suggestions', prompts: items }]);
        return;
    }

    const strip = document.getElementById('suggestions-strip');
    if (!strip) return;
    strip.innerHTML = '';

    if (items.length === 0) {
        strip.classList.add('hidden');
        return;
    }

    items.forEach(text => {
        const chip = document.createElement('button');
        chip.type = 'button';
        chip.className = 'suggestion-chip';
        chip.textContent = text;
        chip.title = text;
        chip.setAttribute('role', 'listitem');
        chip.addEventListener('click', () => {
            // Same semantics as the toolkit 'suggestion' action: fill, never send.
            fillComposerInput(text);
        });
        strip.appendChild(chip);
    });
    strip.classList.remove('hidden');
}

function clearSuggestedPrompts() {
    try { if (_view) _view.removeMessage('sh-suggestions'); } catch {}
    _pluginCards.delete('sh-suggestions');
    const strip = document.getElementById('suggestions-strip');
    if (!strip) return;
    strip.innerHTML = '';
    strip.classList.add('hidden');
}

/**
 * Renders a canvas pointer card: the tool's message plus a button that replays
 * the pan/zoom/highlight on the Grasshopper canvas via the sh:// bridge.
 * Toolkit shell: an evidence line + a meta.actions row whose
 * `canvas_pointer` action routes through messageAction to the same
 * sh://event?type=canvas_pointer&id=... the legacy button emitted.
 * @param {object} ptr - { id, message }
 */
function showCanvasPointer(ptr) {
    const container = document.getElementById('chat-container');
    if (!container || !ptr || !ptr.id) return;

    if (_view) {
        const iid = `pointer-${ptr.id}`;
        pushPluginInteraction(iid, ptr.id, 'canvas_pointer', 'assistant',
            [{
                type: 'evidence',
                level: 'info',
                text: `◎ ${ptr.message || 'The assistant is pointing at something on the canvas.'}`,
            }],
            { actions: [{ id: 'canvas_pointer', label: 'Show on canvas', payload: { id: ptr.id } }] });
        scrollToBottom();
        return;
    }

    const card = document.createElement('section');
    card.className = 'canvas-pointer-card';
    card.dataset.pointerId = ptr.id;

    const header = document.createElement('div');
    header.className = 'canvas-pointer-header';
    const glyph = document.createElement('span');
    glyph.className = 'canvas-pointer-glyph';
    glyph.textContent = '◎';
    header.appendChild(glyph);
    const message = document.createElement('div');
    message.className = 'canvas-pointer-message';
    message.textContent = ptr.message || 'The assistant is pointing at something on the canvas.';
    header.appendChild(message);
    card.appendChild(header);

    const actions = document.createElement('div');
    actions.className = 'canvas-pointer-actions';
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = 'Show on canvas';
    button.addEventListener('click', () => {
        emitShEvent(`sh://event?type=canvas_pointer&id=${encodeURIComponent(ptr.id)}`);
    });
    actions.appendChild(button);
    card.appendChild(actions);

    insertAboveThinkingIfPresent(container, card);
    scrollToBottom();
}

/**
 * Renders a blocking question card: question text, 2-4 option buttons and an
 * always-present free-text field. Answers travel back via the sh:// bridge.
 * Toolkit shell: a card:question — chip clicks and the free-text field report
 * {answer} through messageAction; the bridge registry keeps the option labels
 * so chip answers recover their choice index for the sh:// event.
 * @param {object} q - { id, question, options }
 */
function showUserQuestion(q) {
    const container = document.getElementById('chat-container');
    if (!container || !q || !q.id) return;

    if (_view) {
        const iid = `question-${q.id}`;
        const options = Array.isArray(q.options) ? q.options.filter(o => typeof o === 'string') : [];
        pushPluginInteraction(iid, q.id, 'question', 'assistant', [{
            type: 'card',
            kind: 'question',
            data: {
                question: q.question || '',
                action: 'question',
                options,
                allowFreeText: true,
                placeholder: 'Type an answer…',
                submitLabel: 'Send',
            },
        }], undefined, options);
        scrollToBottom();
        return;
    }

    const card = document.createElement('section');
    card.className = 'user-question-card';
    card.dataset.questionId = q.id;

    const title = document.createElement('h3');
    title.textContent = 'The assistant is asking';
    card.appendChild(title);

    const question = document.createElement('p');
    question.className = 'user-question-text';
    question.textContent = q.question || '';
    card.appendChild(question);

    const options = document.createElement('div');
    options.className = 'user-question-options';
    (Array.isArray(q.options) ? q.options : []).forEach((option, index) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'user-question-option';
        button.textContent = option;
        button.addEventListener('click', () => {
            options.querySelectorAll('button').forEach(b => b.classList.remove('selected'));
            button.classList.add('selected');
            emitShEvent(`sh://event?type=question&id=${encodeURIComponent(q.id)}&choice=${index}`);
        });
        options.appendChild(button);
    });
    card.appendChild(options);

    const divider = document.createElement('div');
    divider.className = 'user-question-divider';
    divider.textContent = 'or type your own answer';
    card.appendChild(divider);

    const freeRow = document.createElement('div');
    freeRow.className = 'user-question-free';
    const input = document.createElement('input');
    input.type = 'text';
    input.placeholder = 'Type an answer…';
    input.maxLength = 4000;
    const send = document.createElement('button');
    send.type = 'button';
    send.textContent = 'Send';
    const submitFree = () => {
        const value = input.value.trim();
        if (!value) return;
        emitShEvent(`sh://event?type=question&id=${encodeURIComponent(q.id)}&text=${encodeURIComponent(value)}`);
    };
    send.addEventListener('click', submitFree);
    input.addEventListener('keydown', ev => {
        if (ev.key === 'Enter') {
            ev.preventDefault();
            submitFree();
        }
    });
    freeRow.appendChild(input);
    freeRow.appendChild(send);
    card.appendChild(freeRow);

    insertAboveThinkingIfPresent(container, card);
    scrollToBottom();
}

/**
 * Freezes a question card once an answer (or cancellation) was processed.
 * @param {string} id - question id
 */
function resolveUserQuestion(id) {
    if (_view) {
        freezePluginCard(`question-${id}`);
        return;
    }
    const card = Array.from(document.querySelectorAll('.user-question-card'))
        .find(node => node.dataset.questionId === id);
    if (!card) return;
    card.classList.add('resolved');
    card.querySelectorAll('button, input').forEach(el => { el.disabled = true; });
}

// ---------------------------------------------------------------------------
// Shell bootstrap: toolkit transport, messageAction routing, fallback DOM, init
// ---------------------------------------------------------------------------

/** Navigates to an sh:// event (the WebView intercepts and cancels navigation). */
function emitShEvent(url) {
    window.location.href = url;
}

/** Fills the toolkit composer input without sending (suggestion-chip semantics). */
function fillComposerInput(text) {
    if (typeof text !== 'string') return;
    if (_view) {
        // Composer text goes through the toolkit handle — it sets the value,
        // re-runs autosize and focuses with the caret at the end. No DOM poke.
        try {
            _view.setComposerText(text, { focus: true });
            return;
        } catch (err) {
            console.warn('[JS] fillComposerInput toolkit error:', err);
        }
    }
    const input = _els.input || document.getElementById('user-input');
    if (!input) return;
    input.value = text;
    try { input.dispatchEvent(new Event('input', { bubbles: true })); } catch {}
    input.focus();
}

/**
 * chat-ui `transport.messageAction` — interactive parts/cards report back here.
 * Maps each action to the matching sh:// event the C# host already handles.
 * The `_pluginCards` registry translates the toolkit message id back to the
 * host-side entity id (consent/question/pointer ids), so the emitted urls are
 * byte-identical to what the legacy DOM cards sent.
 * @param {string} messageId - toolkit interaction id (e.g. 'consent-<plan id>')
 * @param {string} action
 * @param {object} payload
 */
function routeMessageAction(messageId, action, payload) {
    const p = payload && typeof payload === 'object' ? payload : {};
    const plugin = _pluginCards.get(messageId) || null;
    const refId = plugin && plugin.refId ? plugin.refId : messageId;
    switch (action) {
        case 'approval':
        case 'consent':
            emitShEvent(`sh://event?type=consent&id=${encodeURIComponent(refId)}&decision=${p.decision === 'approved' ? 'approve' : 'reject'}`);
            return;
        case 'question':
            if (typeof p.answer === 'string' && p.answer) {
                // Chip answers recover their choice index; anything else is free text.
                const opts = plugin && Array.isArray(plugin.options) ? plugin.options : [];
                const idx = opts.indexOf(p.answer);
                if (idx >= 0) {
                    emitShEvent(`sh://event?type=question&id=${encodeURIComponent(refId)}&choice=${idx}`);
                } else {
                    emitShEvent(`sh://event?type=question&id=${encodeURIComponent(refId)}&text=${encodeURIComponent(p.answer)}`);
                }
            }
            return;
        case 'canvas_pointer':
            emitShEvent(`sh://event?type=canvas_pointer&id=${encodeURIComponent(typeof p.id === 'string' ? p.id : refId)}`);
            return;
        case 'suggestion':
            // Same behavior as the legacy chip: fill the composer, never auto-send.
            fillComposerInput(typeof p.text === 'string' ? p.text : '');
            return;
        case 'model_select':
            if (typeof p.model === 'string' && p.model) {
                emitShEvent(`sh://event?type=model&model=${encodeURIComponent(p.model)}`);
            }
            return;
        default:
            console.warn('[JS] messageAction: unmapped action', action, 'messageId:', messageId);
    }
}

/**
 * ChatTransport implementation — user intent to sh:// host events.
 * `attachments` in a submission stay host-side (staged chips mirror C#-held
 * pending images); only the text travels over the URL scheme.
 */
const bridgeTransport = {
    send(input) {
        try { setProcessing(true); } catch {}
        try { clearSuggestedPrompts(); } catch {}
        const text = input && typeof input.text === 'string' ? input.text : '';
        emitShEvent(`sh://event?type=send&text=${encodeURIComponent(text)}`);
    },
    cancel() {
        emitShEvent('sh://event?type=cancel');
    },
    requestAttachments() {
        emitShEvent('sh://event?type=attach');
    },
    messageAction(messageId, action, payload) {
        routeMessageAction(messageId, action, payload);
    },
    selectModel(modelId) {
        emitShEvent(`sh://event?type=model&model=${encodeURIComponent(modelId || '')}`);
    },
    editQueued(id, text) {
        emitShEvent(`sh://event?type=queue&action=edit&id=${encodeURIComponent(id)}&text=${encodeURIComponent(typeof text === 'string' ? text : '')}`);
    },
    sendQueuedNow(id) {
        emitShEvent(`sh://event?type=queue&action=submit-now&id=${encodeURIComponent(id)}`);
    },
    removeQueued(id) {
        emitShEvent(`sh://event?type=queue&action=remove&id=${encodeURIComponent(id)}`);
    },
};

/**
 * Capture-phase click delegation on the toolkit attachment strip. The toolkit's
 * own remove button mutates staged state locally without a transport callback;
 * the host owns the real pending list, so each removal also emits detach&id.
 * Runs in capture so the chip index maps to staged items BEFORE the toolkit's
 * bubble-phase handler splices the clicked item out.
 */
function onAttachStripClick(e) {
    try {
        const rm = e.target && e.target.closest ? e.target.closest('.cui-attachment-rm') : null;
        if (!rm || !_view) return;
        const chip = rm.closest('.cui-attachment');
        if (!chip || !chip.parentElement) return;
        const idx = Array.prototype.indexOf.call(chip.parentElement.children, chip);
        if (idx < 0) return;
        const staged = _view.getAttachments();
        const item = staged[idx];
        const id = item && item.meta && item.meta.id;
        if (id) {
            emitShEvent(`sh://event?type=detach&id=${encodeURIComponent(id)}`);
        }
    } catch (err) {
        console.warn('[JS] attachment detach delegation error:', err);
    }
}

/**
 * Builds a minimal legacy DOM when the toolkit failed to load (defensive
 * fallback — the vendored bundle is embedded, so this should never run).
 * Recreates #chat-container plus a wired composer so compat globals still work.
 */
function buildFallbackShell(rootEl) {
    const status = document.createElement('div');
    status.id = 'status-bar';
    const statusText = document.createElement('span');
    statusText.id = 'status-text';
    statusText.textContent = 'Ready';
    const spinner = document.createElement('span');
    spinner.id = 'spinner';
    spinner.className = 'spinner hidden';
    status.appendChild(statusText);
    status.appendChild(spinner);

    const container = document.createElement('div');
    container.id = 'chat-container';

    const inputBar = document.createElement('div');
    inputBar.id = 'input-bar';
    const attachStrip = document.createElement('div');
    attachStrip.id = 'attachment-strip';
    attachStrip.className = 'hidden';
    const input = document.createElement('textarea');
    input.id = 'user-input';
    input.placeholder = 'Type a message...';
    input.rows = 3;
    const actions = document.createElement('div');
    actions.className = 'actions';
    const left = document.createElement('div');
    left.className = 'actions-left';
    const attachBtn = document.createElement('button');
    attachBtn.id = 'attach-button';
    attachBtn.type = 'button';
    attachBtn.title = 'Attach image';
    attachBtn.textContent = '\u{1F4CE}';
    const right = document.createElement('div');
    right.className = 'actions-right';
    const sendBtn = document.createElement('button');
    sendBtn.id = 'send-button';
    sendBtn.type = 'button';
    sendBtn.textContent = 'Send';
    const cancelBtn = document.createElement('button');
    cancelBtn.id = 'cancel-button';
    cancelBtn.type = 'button';
    cancelBtn.textContent = 'Cancel';

    const suggestions = document.getElementById('suggestions-strip');
    if (suggestions) inputBar.appendChild(suggestions);
    inputBar.appendChild(attachStrip);
    inputBar.appendChild(input);
    left.appendChild(attachBtn);
    const debug = document.getElementById('sh-debug-actions');
    if (debug) left.appendChild(debug);
    actions.appendChild(left);
    right.appendChild(sendBtn);
    right.appendChild(cancelBtn);
    actions.appendChild(right);
    inputBar.appendChild(actions);

    rootEl.appendChild(status);
    rootEl.appendChild(container);
    rootEl.appendChild(inputBar);

    attachBtn.addEventListener('click', () => emitShEvent('sh://event?type=attach'));
    cancelBtn.addEventListener('click', () => emitShEvent('sh://event?type=cancel'));
    const doSend = () => {
        const text = (input.value || '').trim();
        const hasAttachments = attachStrip.children.length > 0;
        if (!text && !hasAttachments) return;
        try { setProcessing(true); } catch {}
        try { clearSuggestedPrompts(); } catch {}
        emitShEvent(`sh://event?type=send&text=${encodeURIComponent(text)}`);
        input.value = '';
    };
    sendBtn.addEventListener('click', doSend);
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            doSend();
        }
    });

    // Minimal attachment chips for fallback mode (DOM-only, detach via sh://).
    addAttachmentChip = function (id, name, dataUri) {
        attachStrip.classList.remove('hidden');
        const chip = document.createElement('div');
        chip.className = 'attachment-chip';
        chip.dataset.aid = id;
        const img = document.createElement('img');
        img.src = dataUri;
        img.alt = name || 'attachment';
        const rm = document.createElement('button');
        rm.type = 'button';
        rm.className = 'attachment-remove';
        rm.textContent = '×';
        rm.title = 'Remove attachment';
        rm.addEventListener('click', () => {
            emitShEvent('sh://event?type=detach&id=' + encodeURIComponent(id));
        });
        chip.appendChild(img);
        chip.appendChild(rm);
        attachStrip.appendChild(chip);
    };
    removeAttachmentChip = function (id) {
        const chip = attachStrip.querySelector('.attachment-chip[data-aid="' + id + '"]');
        if (chip) chip.remove();
        if (!attachStrip.children.length) attachStrip.classList.add('hidden');
    };
    clearAttachments = function () {
        attachStrip.innerHTML = '';
        attachStrip.classList.add('hidden');
    };
}

/**
 * Mounts the vendored chat-ui toolkit on #chat-root and rewires the legacy
 * element ids the compat globals query. Also relocates the suggestions strip
 * and debug actions inside the toolkit chrome.
 */
function initChatUiShell() {
    const rootEl = document.getElementById('chat-root') || document.body;

    if (typeof ChatUI === 'undefined' || typeof ChatUI.createChatView !== 'function') {
        console.error('[JS] ChatUI toolkit missing; building fallback shell');
        buildFallbackShell(rootEl);
        return;
    }

    _view = ChatUI.createChatView(rootEl, {
        transport: bridgeTransport,
        placeholder: 'Type a message...',
        theme: 'cloud',            // stamps data-theme='cloud' -> css/chat-theme.css rules
        features: {
            queue: true,             // outgoing-message queue panel fed by setQueue
            contextCard: true,
            quoteChip: true,
            attachments: true,
            messageActions: true,
            modelPicker: true,       // .cui-model stays hidden until setModels feeds it
            attachmentAccept: 'image/*',
        },
        partRenderers: {
            html: renderHtmlPart,
        },
    });
    window._chatView = _view;

    const root = rootEl.querySelector('.chat-ui');
    if (!root) {
        console.error('[JS] chat-ui root not found after createChatView');
        return;
    }

    _els.root = root;
    _els.status = root.querySelector('.cui-status');
    _els.messages = root.querySelector('.cui-messages');
    _els.composer = root.querySelector('.cui-composer');
    _els.input = root.querySelector('.cui-input');
    _els.attach = root.querySelector('.cui-attach');
    _els.send = root.querySelector('.cui-send');
    _els.cancel = root.querySelector('.cui-cancel');
    _els.attachStrip = root.querySelector('.cui-attachments');

    // Re-establish the legacy element ids the compat globals query.
    if (_els.messages) _els.messages.id = 'chat-container';
    if (_els.input) _els.input.id = 'user-input';
    if (_els.attach) _els.attach.id = 'attach-button';
    if (_els.send) _els.send.id = 'send-button';
    if (_els.cancel) _els.cancel.id = 'cancel-button';

    // Spinner lives inside the toolkit status line; setStatus re-appends it
    // after each textContent update.
    if (_els.status) {
        const spinner = document.createElement('span');
        spinner.id = 'spinner';
        spinner.className = 'spinner hidden';
        _els.spinner = spinner;
        _els.status.appendChild(spinner);
    }

    // Relocate the suggestions strip inside the toolkit chrome (above composer).
    const suggestions = document.getElementById('suggestions-strip');
    if (suggestions && _els.composer) {
        root.insertBefore(suggestions, _els.composer);
    }

    // Relocate debug actions (regen/update, debug builds only) into the composer.
    const debug = document.getElementById('sh-debug-actions');
    if (debug && _els.composer) {
        _els.composer.appendChild(debug);
        debug.classList.toggle('hidden', debug.children.length === 0);
    }
    const regenBtn = document.getElementById('regen-button');
    if (regenBtn) {
        regenBtn.addEventListener('click', () => emitShEvent('sh://event?type=regen'));
    }
    const updateBtn = document.getElementById('update-button');
    if (updateBtn) {
        updateBtn.addEventListener('click', () => emitShEvent('sh://event?type=update'));
    }

    // Capture-phase detach delegation on the toolkit attachment strip.
    if (_els.attachStrip) {
        _els.attachStrip.addEventListener('click', onAttachStripClick, true);
    }

    // Autonomy overlay: extend the run's time/token budgets by 50% per click.
    const extendBtn = document.getElementById('autonomy-extend-btn');
    if (extendBtn) {
        extendBtn.addEventListener('click', () => {
            emitShEvent('sh://event?type=extend_autonomy');
        });
    }
}

// Full WebView UI wiring
document.addEventListener('DOMContentLoaded', function () {
    console.log('[JS] DOMContentLoaded event fired');
    try {
        initChatUiShell();
        console.log('[JS] DOMContentLoaded wiring completed successfully');

        // Signal host that the page is ready for injections
        window.SH_READY = true;
        console.log('[JS] SH_READY set to true');
    } catch (err) {
        console.error('[JS] DOMContentLoaded wiring error:', err);
    }
});
