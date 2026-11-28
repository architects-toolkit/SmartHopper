/* Remote images are loaded by the consent-gated host, never by the WebView.
 * CSP must block HTTP(S)/file image sources before nodes reach the document. */
(function () {
    'use strict';
    const seen = new WeakMap();
    const pending = new Map();
    const resolved = new Map();
    let cachedCharacters = 0;
    const queue = [];
    let active = null;

    function next() {
        if (active || queue.length === 0) return;
        while (queue.length) {
            const candidate = queue.shift();
            if (candidate.image.isConnected && candidate.image.getAttribute('src') === candidate.raw) {
                if (resolved.has(candidate.url)) {
                    display(candidate.image, resolved.get(candidate.url), candidate.raw);
                    pending.delete(candidate.id);
                    continue;
                }
                active = candidate;
                break;
            }
            pending.delete(candidate.id);
        }
        if (!active) return;
        emitShEvent('sh://event?type=external_image&id=' + encodeURIComponent(active.id) +
            '&url=' + encodeURIComponent(active.url));
    }

    function remember(url, data) {
        resolved.set(url, data);
        cachedCharacters += data ? data.length : 0;
        while (resolved.size > 64 || cachedCharacters > 32 * 1024 * 1024) {
            const oldest = resolved.keys().next().value;
            cachedCharacters -= (resolved.get(oldest) || '').length;
            resolved.delete(oldest);
        }
    }

    function display(image, data, original) {
        if (data) {
            image.setAttribute('src', data);
            image.title = '';
            return;
        }
        image.removeAttribute('src');
        image.title = 'Image not retrieved: source permission was denied or retrieval failed.';
        const retry = document.createElement('button');
        retry.type = 'button';
        retry.textContent = 'Retry image';
        retry.addEventListener('click', () => {
            resolved.delete(new URL(original, document.baseURI).href);
            seen.delete(image);
            retry.remove();
            image.setAttribute('src', original);
            inspect(image);
        });
        image.insertAdjacentElement('afterend', retry);
    }

    function inspect(image) {
        if (!image || image.tagName !== 'IMG') return;
        // A picture/srcset candidate must not override the host-approved data URI.
        image.removeAttribute('srcset');
        const raw = image.getAttribute('src');
        if (!raw || /^(data:|blob:)/i.test(raw) || seen.get(image) === raw) return;
        seen.set(image, raw);
        let url;
        try {
            url = new URL(raw, document.baseURI);
            if (!/^https?:$/.test(url.protocol) || url.username || url.password) return;
        } catch (_) { return; }
        if (resolved.has(url.href)) {
            display(image, resolved.get(url.href), raw);
            return;
        }
        if (pending.size >= 64) {
            image.title = 'External image retrieval queue is full.';
            return;
        }
        const bytes = crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 15) | 64;
        bytes[8] = (bytes[8] & 63) | 128;
        const hex = Array.from(bytes, value => value.toString(16).padStart(2, '0')).join('');
        const id = [hex.slice(0, 8), hex.slice(8, 12), hex.slice(12, 16), hex.slice(16, 20), hex.slice(20)].join('-');
        const request = { id, url: url.href, image, raw };
        pending.set(id, request);
        queue.push(request);
        image.title = 'Waiting for source permission.';
        next();
    }

    function scan(root) {
        if (!root || root.nodeType !== 1) return;
        if (root.tagName === 'SOURCE') root.removeAttribute('srcset');
        inspect(root);
        root.querySelectorAll('source[srcset]').forEach(source => source.removeAttribute('srcset'));
        root.querySelectorAll('img').forEach(inspect);
    }

    window.completeExternalImage = function (id, dataUri) {
        const request = pending.get(id);
        if (!request || !active || active.id !== id) return;
        pending.delete(id);
        active = null;
        const validData = typeof dataUri === 'string' && /^data:image\/[a-z0-9.+-]+;base64,/i.test(dataUri) ? dataUri : null;
        remember(request.url, validData);
        if (request.image.isConnected && request.image.getAttribute('src') === request.raw) {
            display(request.image, validData, request.raw);
        }
        next();
    };

    function start() {
        scan(document.body);
        new MutationObserver(records => {
            records.forEach(record => {
                if (record.type === 'attributes') scan(record.target);
                else record.addedNodes.forEach(scan);
            });
        }).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['src', 'srcset'] });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
}());
