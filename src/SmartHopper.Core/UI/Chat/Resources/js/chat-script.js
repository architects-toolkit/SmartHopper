/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

// Scroll thresholds (pixels)
const SCROLL_BOTTOM_THRESHOLD = 30; // consider near-bottom within this distance
const SCROLL_SHOW_BTN_THRESHOLD = 5; // show scroll-to-bottom button when farther than this

// Render limits and thresholds
const MAX_MESSAGE_HTML_LENGTH = 20000; // cap DOM insertion size to avoid huge paints
const PERF_LOG_THRESHOLD_MS = 16; // only log perf outliers (>1 frame)
const LRU_MAX_ENTRIES = 100; // recent DOM html cache size
const FLUSH_INTERVAL_MS = 50; // max wait before flushing queued DOM ops
const DIFF_SAMPLE_RATE = 0.25; // sample equality diffing (25%) to lower cost
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
    let frag = _templateCache.get(html);
    if (!frag) {
        // Guard against excessively large payloads
        if (html.length > MAX_MESSAGE_HTML_LENGTH) {
            console.warn(`[JS] ${context}: html length ${html.length} exceeds cap ${MAX_MESSAGE_HTML_LENGTH}, truncating`);
            html = html.slice(0, MAX_MESSAGE_HTML_LENGTH) + '…';
        }
        const temp = document.createElement('div');
        temp.innerHTML = html;
        frag = document.createDocumentFragment();
        while (temp.firstChild) {
            frag.appendChild(temp.firstChild);
        }
        _templateCache.set(html, frag.cloneNode(true));
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

function applyPatchToExisting(existing, patchObj, context) {
    if (!existing || !patchObj) return null;
    const content = existing.querySelector('.message-content') || existing;
    if (patchObj.patch === 'append') {
        const temp = document.createElement('div');
        temp.innerHTML = patchObj.html || '';
        // Append children to content
        while (temp.firstChild) {
            content.appendChild(temp.firstChild);
        }
        return existing;
    }
    if (patchObj.patch === 'replace-content') {
        content.innerHTML = patchObj.html || '';
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
    tempDiv.innerHTML = messageHtml || '';
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
    tempDiv.innerHTML = messageHtml || '';
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
 * Finds a message element inside container by its dataset.key value.
 * @param {HTMLElement} chatContainer
 * @param {string} key
 * @returns {HTMLElement|null}
 */
function findExistingMessageByKey(chatContainer, key) {
    const messages = Array.from(chatContainer.querySelectorAll('.message'));
    return messages.find(m => (m.dataset && m.dataset.key) === (key || '')) || null;
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
 * Adds a message to the chat container
 * @param {string} messageHtml - HTML content of the message
 */
function addMessage(messageHtml) {
    enqueueDomOp(() => {
        const start = (typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now();
        const { chatContainer, wasAtBottom } = getContainerWithBottom('addMessage');
        if (!chatContainer) return;

        const node = cloneFromTemplate(messageHtml, 'addMessage') || createNodeFromHtml(messageHtml, 'addMessage');
        if (!node) return;
        addWipeAnimation(node);

        // Insert above the persistent thinking message if present; otherwise append
        insertAboveThinkingIfPresent(chatContainer, node);
        // Finalize: reprocess dynamic features and auto-scroll if needed
        finalizeMessageInsertion(node, wasAtBottom);

        const dur = ((typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now()) - start;
        if (Math.random() <= PERF_SAMPLE_RATE) {
            _perfCounters.renders += 1;
            _perfCounters.renderMs += dur;
        }
        if (dur > PERF_LOG_THRESHOLD_MS) {
            _perfCounters.renderSlow += 1;
            console.debug('[JS] addMessage slow render', { ms: dur.toFixed(2), len: messageHtml ? messageHtml.length : 0 });
        }
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
        if (patchObj && existing) {
            const patched = applyPatchToExisting(existing, patchObj, 'upsertMessageAfter');
            if (patched) {
                // Patch updates: no animation (bubble already exists from first chunk)
                finalizeMessageInsertion(patched, wasAtBottom);
                recordHtmlCache(key, messageHtml);
                if (Math.random() <= PERF_SAMPLE_RATE) {
                    _perfCounters.renders += 1;
                }
                return true;
            }
        }

        const incoming = cloneFromTemplate(messageHtml, 'upsertMessageAfter') || createNodeFromHtml(messageHtml, 'upsertMessageAfter');
        if (!incoming) return false;
        setDatasetKeySafe(incoming, key);
        addWipeAnimation(incoming);

        if (existing) {
            existing.replaceWith(incoming);
        }

        insertAfterNode(chatContainer, incoming, follow, 'upsertMessageAfter');
        finalizeMessageInsertion(incoming, wasAtBottom);
        recordHtmlCache(key, messageHtml);

        const dur = ((typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now()) - start;
        if (Math.random() <= PERF_SAMPLE_RATE) {
            _perfCounters.renders += 1;
            _perfCounters.renderMs += dur;
        }
        if (dur > PERF_LOG_THRESHOLD_MS) {
            _perfCounters.renderSlow += 1;
            console.debug('[JS] upsertMessageAfter slow render', { ms: dur.toFixed(2), len: messageHtml ? messageHtml.length : 0 });
        }
        return true;
    });
}

/**
 * Upserts a message DOM node identified by a stable key. If a node with the same key exists,
 * it is replaced; otherwise, the node is appended (just above any persistent loading bubble).
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

        if (patchObj && existing) {
            const patched = applyPatchToExisting(existing, patchObj, 'upsertMessage');
            if (patched) {
                finalizeMessageInsertion(patched, wasAtBottom);
                recordHtmlCache(key, messageHtml);
                if (Math.random() <= PERF_SAMPLE_RATE) {
                    _perfCounters.renders += 1;
                }
                return true;
            }
        }

        const incoming = cloneFromTemplate(messageHtml, 'upsertMessage') || createNodeFromHtml(messageHtml, 'upsertMessage');
        if (!incoming) return false;
        setDatasetKeySafe(incoming, key);
        
        // Only animate on NEW bubble insertion (first chunk), not on updates to existing bubbles
        if (!existing) {
            addWipeAnimation(incoming);
        }

        if (existing) {
            chatContainer.replaceChild(incoming, existing);
        } else {
            insertAboveThinkingIfPresent(chatContainer, incoming);
        }

        finalizeMessageInsertion(incoming, wasAtBottom);
        recordHtmlCache(key, messageHtml);

        const dur = ((typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now()) - start;
        if (Math.random() <= PERF_SAMPLE_RATE) {
            _perfCounters.renders += 1;
            _perfCounters.renderMs += dur;
        }
        if (dur > PERF_LOG_THRESHOLD_MS) {
            _perfCounters.renderSlow += 1;
            console.debug('[JS] upsertMessage slow render', { ms: dur.toFixed(2), len: messageHtml ? messageHtml.length : 0 });
        }
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
        chatContainer.removeChild(lastLoader);
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
            chatContainer.replaceChild(incoming, lastMessage);
        } else {
            chatContainer.appendChild(incoming);
        }

        finalizeMessageInsertion(incoming, wasAtBottom);
        const dur = ((typeof performance !== 'undefined' && performance.now) ? performance.now() : Date.now()) - start;
        if (dur > PERF_LOG_THRESHOLD_MS) {
            console.debug('[JS] replaceLastMessageByRole slow render', { ms: dur.toFixed(2), len: messageHtml ? messageHtml.length : 0 });
        }
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
                console.debug("[chat-script] Triggering host copy", text);
                window.location.href = `clipboard://copy?text=${text}`;
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
 * Scrolls the window to the bottom
 */
function scrollToBottom() {
    const chatContainer = document.getElementById('chat-container');
    if (chatContainer && typeof chatContainer.scrollTop === 'number') {
        chatContainer.scrollTop = chatContainer.scrollHeight;
    } else {
        window.scrollTo(0, document.body.scrollHeight);
    }
    hideNewMessagesIndicator();
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
 * Shows the new messages indicator and updates scroll controls.
 */
function showNewMessagesIndicator() {
    const indicator = document.getElementById('new-messages-indicator');
    if (indicator) indicator.classList.remove('hidden');
    updateScrollControls();
}

/**
 * Hides the new messages indicator.
 */
function hideNewMessagesIndicator() {
    const indicator = document.getElementById('new-messages-indicator');
    if (indicator) indicator.classList.add('hidden');
}

/**
 * Updates visibility of scroll-to-bottom button based on current scroll position.
 */
function updateScrollControls() {
    const chatContainer = document.getElementById('chat-container');
    const btn = document.getElementById('scroll-bottom-btn');
    if (!btn || !chatContainer) return;
    if (isAtBottom(chatContainer, SCROLL_SHOW_BTN_THRESHOLD)) {
        btn.classList.add('hidden');
        hideNewMessagesIndicator();
    } else {
        btn.classList.remove('hidden');
    }
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
                console.log('[JS] finalizeMessageInsertion: binding collapsibles on', rootNode.className || '(no class)');
                setupCollapsibleHandlers(rootNode);
            } else {
                console.warn('[JS] finalizeMessageInsertion: setupCollapsibleHandlers is not defined');
            }
        }
    } finally {
        console.log('[JS] finalizeMessageInsertion: wasAtBottom =', !!wasAtBottom);
        // Auto-scroll decision
        if (wasAtBottom) {
            scrollToBottom();
        } else {
            showNewMessagesIndicator();
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

// Full WebView UI wiring
document.addEventListener('DOMContentLoaded', function () {
    console.log('[JS] DOMContentLoaded event fired');
    try {
        const input = document.getElementById('user-input');
        const sendBtn = document.getElementById('send-button');
        const cancelBtn = document.getElementById('cancel-button');
        const regenBtn = document.getElementById('regen-button'); // present only when host injects debug actions

        const chatContainer = document.getElementById('chat-container');
        const newIndicator = document.getElementById('new-messages-indicator');
        const scrollBtn = document.getElementById('scroll-bottom-btn');

        console.log('[JS] Element search results:', {
            input: !!input,
            sendBtn: !!sendBtn,
            cancelBtn: !!cancelBtn,
            regenBtn: !!regenBtn,
            chatContainer: !!chatContainer,
            newIndicator: !!newIndicator,
            scrollBtn: !!scrollBtn
        });

        if (sendBtn) {
            sendBtn.addEventListener('click', () => {
                console.log('[JS] Send button clicked');
                const text = (input && input.value || '').trim();
                console.log('[JS] Input text:', text);
                if (!text) {
                    console.log('[JS] No text to send, returning');
                    return;
                }

                // Host will append the user message; just notify and clear input for UX
                try { setProcessing(true); } catch {}
                const url = `sh://event?type=send&text=${encodeURIComponent(text)}`;
                console.log('[JS] Navigating to:', url);
                window.location.href = url;
                if (input) input.value = '';
                console.log('[JS] Input cleared');
            });
            console.log('[JS] Send button click handler attached');
        } else {
            console.error('[JS] Send button not found!');
        }

        if (cancelBtn) {
            cancelBtn.addEventListener('click', () => {
                console.log('[JS] Cancel button clicked');
                window.location.href = 'sh://event?type=cancel';
            });
            console.log('[JS] Cancel button click handler attached');
        } else {
            console.error('[JS] Cancel button not found!');
        }

        if (regenBtn) {
            regenBtn.addEventListener('click', () => {
                console.log('[JS] Regen button clicked');
                window.location.href = 'sh://event?type=regen';
            });
            console.log('[JS] Regen button click handler attached');
        }

        if (input) {
            input.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    console.log('[JS] Enter key pressed, triggering send');
                    e.preventDefault();
                    if (sendBtn) sendBtn.click();
                }
            });
            console.log('[JS] Input keydown handler attached');
        } else {
            console.error('[JS] Input element not found!');
        }

        // Scroll controls: click to jump to bottom from indicator or button
        if (newIndicator) {
            newIndicator.addEventListener('click', () => {
                scrollToBottom();
            });
        }
        if (scrollBtn) {
            scrollBtn.addEventListener('click', () => {
                scrollToBottom();
            });
        }

        // Update scroll controls on container scroll and window resize
        if (chatContainer) {
            chatContainer.addEventListener('scroll', () => updateScrollControls());
            // Initialize based on current position
            updateScrollControls();
        }
        window.addEventListener('resize', () => updateScrollControls());

        console.log('[JS] DOMContentLoaded wiring completed successfully');

        // Signal host that the page is ready for injections
        window.SH_READY = true;
        console.log('[JS] SH_READY set to true');

    } catch (err) {
        console.error('[JS] DOMContentLoaded wiring error:', err);
    }
});

// Host-controlled helpers
function setStatus(text) {
    console.log('[JS] setStatus called with text:', text);
    const el = document.getElementById('status-text');
    if (el) {
        el.textContent = text || '';
        console.log('[JS] setStatus: status updated');
    } else {
        console.error('[JS] setStatus: status-text element not found');
    }
}

function setProcessing(on) {
    console.log('[JS] setProcessing called with value:', on);
    const spinner = document.getElementById('spinner');
    const input = document.getElementById('user-input');
    const sendBtn = document.getElementById('send-button');
    const cancelBtn = document.getElementById('cancel-button');

    if (spinner) {
        spinner.classList.toggle('hidden', !on);

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
    } else {
        console.error('[JS] setProcessing: spinner element not found');
    }

    // Toggle controls according to processing state
    // When processing: disable input + send, enable cancel
    // When idle: enable input + send, disable cancel
    try {
        if (input) input.disabled = !!on;
        if (sendBtn) sendBtn.disabled = !!on;
        if (cancelBtn) cancelBtn.disabled = !on;

        // Optional: reflect disabled state via CSS class and ARIA for better a11y
        [sendBtn, cancelBtn].forEach(btn => {
            if (!btn) return;
            try {
                btn.classList.toggle('disabled', !!btn.disabled);
                btn.setAttribute('aria-disabled', btn.disabled ? 'true' : 'false');
            } catch {}
        });

        if (input) {
            try {
                input.setAttribute('aria-disabled', input.disabled ? 'true' : 'false');
            } catch {}
        }
    } catch (err) {
        console.warn('[JS] setProcessing: control toggle failed', err);
    }
}

function resetMessages() {
    console.log('[JS] resetMessages called');
    const chatContainer = document.getElementById('chat-container');
    if (!chatContainer) {
        console.error('[JS] resetMessages: chat-container element not found');
        return;
    }
    chatContainer.innerHTML = '';

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