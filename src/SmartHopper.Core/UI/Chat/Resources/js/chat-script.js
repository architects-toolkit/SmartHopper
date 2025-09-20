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

/**
 * Adds a message to the chat container
 * @param {string} messageHtml - HTML content of the message
 */
function addMessage(messageHtml) {
    console.log('[JS] addMessage called with HTML length:', messageHtml ? messageHtml.length : 0);
    const chatContainer = document.getElementById('chat-container');
    if (!chatContainer) {
        console.error('[JS] addMessage: chat-container element not found');
        return;
    }
    const wasAtBottom = isAtBottom(chatContainer, SCROLL_BOTTOM_THRESHOLD);
    
    // Create a temporary div to parse the HTML
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = messageHtml;

    // Use firstElementChild to avoid appending a text node from leading whitespace
    const node = tempDiv.firstElementChild || tempDiv.firstChild;
    if (node) {
        // Insert above the persistent thinking message if present; otherwise append
        insertAboveThinkingIfPresent(chatContainer, node);
        console.log('[JS] addMessage: node appended successfully, role classes:', node.className);
    } else {
        console.error('[JS] addMessage: no valid node found in HTML');
    }

    // Finalize: reprocess dynamic features and auto-scroll if needed
    finalizeMessageInsertion(node, wasAtBottom);
}

/**
 * Adds a temporary loading bubble for a given role (defaults to assistant).
 * The bubble carries the 'loading' class so CSS shows a spinner via ::before.
 * @param {string} role
 * @param {string} text
 */
function addLoadingMessage(role, text) {
    console.log('[JS] addLoadingMessage called, role:', role, 'text:', text);
    const chatContainer = document.getElementById('chat-container');
    if (!chatContainer) {
        console.error('[JS] addLoadingMessage: chat-container element not found');
        return;
    }
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
    const wasAtBottom = isAtBottom(chatContainer, SCROLL_BOTTOM_THRESHOLD);
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
    const chatContainer = document.getElementById('chat-container');
    const messages = Array.from(chatContainer.querySelectorAll(`.message.${role}`));
    console.log('[JS] replaceLastMessageByRole: found', messages.length, 'existing messages for role:', role);

    // Parse incoming HTML into an element
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = messageHtml || '';
    const incoming = tempDiv.firstElementChild;
    if (!incoming) {
        console.error('[JS] replaceLastMessageByRole: no valid element in HTML');
        return false;
    }

    const wasAtBottom = isAtBottom(chatContainer, SCROLL_BOTTOM_THRESHOLD);

    if (messages.length > 0) {
        const lastMessage = messages[messages.length - 1];
        chatContainer.replaceChild(incoming, lastMessage);
        console.log('[JS] replaceLastMessageByRole: replaced existing message');
    } else {
        chatContainer.appendChild(incoming);
        console.log('[JS] replaceLastMessageByRole: appended new message');
    }

    // Finalize: reprocess dynamic features and auto-scroll if needed
    finalizeMessageInsertion(incoming, wasAtBottom);
    return true;
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
        const clearBtn = document.getElementById('clear-button');
        const cancelBtn = document.getElementById('cancel-button');
        const chatContainer = document.getElementById('chat-container');
        const newIndicator = document.getElementById('new-messages-indicator');
        const scrollBtn = document.getElementById('scroll-bottom-btn');

        console.log('[JS] Element search results:', {
            input: !!input,
            sendBtn: !!sendBtn,
            clearBtn: !!clearBtn,
            cancelBtn: !!cancelBtn,
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

        if (clearBtn) {
            clearBtn.addEventListener('click', () => {
                console.log('[JS] Clear button clicked');
                window.location.href = 'sh://event?type=clear';
            });
            console.log('[JS] Clear button click handler attached');
        } else {
            console.error('[JS] Clear button not found!');
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
    if (spinner) {
        spinner.classList.toggle('hidden', !on);
        console.log('[JS] setProcessing: spinner', on ? 'shown' : 'hidden');
    } else {
        console.error('[JS] setProcessing: spinner element not found');
    }
}

/**
 * Clears all messages from the chat container
 */
function clearMessages() {
    console.log('[JS] clearMessages called');
    const chatContainer = document.getElementById('chat-container');
    if (!chatContainer) {
        console.error('[JS] clearMessages: chat-container element not found');
        return;
    }
    chatContainer.innerHTML = '';
    console.log('[JS] clearMessages: all messages cleared');
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