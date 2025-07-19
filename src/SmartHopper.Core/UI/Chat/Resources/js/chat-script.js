/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/**
 * Adds a message to the chat container
 * @param {string} messageHtml - HTML content of the message
 */
function addMessage(messageHtml) {
    const chatContainer = document.getElementById('chat-container');
    
    // Create a temporary div to parse the HTML
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = messageHtml;
    
    // Append the parsed HTML to the chat container
    chatContainer.appendChild(tempDiv.firstChild);
    
    // Process any code blocks for syntax highlighting
    processCodeBlocks();
    
    // Make links open in a new window
    processLinks();
    
    // Setup metrics tooltips
    setupMetricsTooltip();
    
    // Scroll to the bottom of the chat
    scrollToBottom();
    
    // Enable collapsible for tool messages and system messages
    const lastMsg = chatContainer.lastElementChild;
    if (lastMsg && (lastMsg.classList.contains('tool') || lastMsg.classList.contains('system'))) {
        lastMsg.addEventListener('click', () => lastMsg.classList.toggle('expanded'));
    }
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
    window.scrollTo(0, document.body.scrollHeight);
}

/**
 * Creates a new message using a template
 * @param {string} role - The role of the message sender (user, assistant, system)
 * @param {string} displayName - The display name of the sender
 * @param {string} content - The HTML content of the message
 * @param {string} timestamp - The formatted date-time of the message
 * @returns {string} The HTML for the message
 */
function createMessageFromTemplate(role, displayName, content, timestamp) {
    if (typeof MESSAGE_TEMPLATE !== 'undefined') {
        return MESSAGE_TEMPLATE
            .replace('{{role}}', role)
            .replace('{{displayName}}', displayName)
            .replace('{{content}}', content)
            .replace('{{timestamp}}', timestamp);
    }
    
    return "error";
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