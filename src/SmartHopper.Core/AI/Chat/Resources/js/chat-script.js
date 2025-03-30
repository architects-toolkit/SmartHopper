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
    
    // Scroll to the bottom of the chat
    scrollToBottom();
}

/**
 * Processes code blocks for potential syntax highlighting
 */
function processCodeBlocks() {
    // If you want to add syntax highlighting, you could add that here
    // This is a placeholder for future enhancements
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
 * @returns {string} The HTML for the message
 */
function createMessageFromTemplate(role, displayName, content) {
    if (typeof MESSAGE_TEMPLATE !== 'undefined') {
        return MESSAGE_TEMPLATE
            .replace('{{role}}', role)
            .replace('{{displayName}}', displayName)
            .replace('{{content}}', content);
    }
    
    return "error";   
}
