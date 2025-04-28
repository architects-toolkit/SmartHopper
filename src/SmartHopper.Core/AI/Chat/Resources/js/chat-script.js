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
    
    // Enable collapsible for tool messages
    const lastMsg = chatContainer.lastElementChild;
    if (lastMsg && lastMsg.classList.contains('tool')) {
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
    tooltip.innerHTML = `
        <div><strong>Provider:</strong> ${provider || 'Unknown'}</div>
        <div><strong>Model:</strong> ${model || 'Unknown'}</div>
        <div><strong>Tokens In:</strong> ${inTokens || '0'}</div>
        <div><strong>Tokens Out:</strong> ${outTokens || '0'}</div>
        <div><strong>Finish Reason:</strong> ${reason || 'Unknown'}</div>
    `;
    
    // Position tooltip
    const iconRect = icon.getBoundingClientRect();
    tooltip.style.left = `${iconRect.left}px`;
    tooltip.style.top = `${iconRect.bottom - tooltip.offsetHeight}px`;
    
    // Add tooltip to document
    document.body.appendChild(tooltip);
    
    // Adjust position if tooltip is cut off at the top
    const tooltipRect = tooltip.getBoundingClientRect();
    if (tooltipRect.top < 0) {
        tooltip.style.top = `${iconRect.bottom + 5}px`;
    }
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
