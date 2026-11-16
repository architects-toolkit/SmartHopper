'use strict';
const assert = require('node:assert/strict');
const { webcrypto } = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { test } = require('node:test');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname,
    '../src/SmartHopper.Core/UI/Chat/Resources/js/external-sources.js'), 'utf8');

function fixture(urls) {
    const emitted = [];
    let observer;
    class Element {
        constructor(tag, url) {
            this.tagName = tag;
            this.nodeType = 1;
            this.isConnected = true;
            this.attributes = new Map(url ? [['src', url]] : []);
            this.adjacent = [];
            this.listeners = {};
        }
        getAttribute(key) { return this.attributes.get(key) || null; }
        setAttribute(key, value) { this.attributes.set(key, value); }
        removeAttribute(key) { this.attributes.delete(key); }
        querySelectorAll() { return []; }
        insertAdjacentElement(_, value) { this.adjacent.push(value); }
        addEventListener(name, callback) { this.listeners[name] = callback; }
        remove() { this.isConnected = false; }
    }
    const images = urls.map(url => new Element('IMG', url));
    const body = new Element('BODY');
    body.querySelectorAll = selector => selector === 'img' ? images : [];
    const window = {};
    vm.runInNewContext(source, {
        window, crypto: webcrypto, Uint8Array, URL,
        document: {
            body, readyState: 'complete', baseURI: 'https://chat.invalid/',
            createElement: tag => new Element(tag.toUpperCase()),
        },
        MutationObserver: class {
            constructor(callback) { observer = callback; }
            observe() {}
        },
        emitShEvent: value => emitted.push(new URL(value)),
    });
    return {
        images, emitted,
        add(url) {
            const image = new Element('IMG', url);
            observer([{ type: 'childList', addedNodes: [image] }]);
            return image;
        },
        complete(data, id = emitted.at(-1).searchParams.get('id')) {
            window.completeExternalImage(id, data);
        },
    };
}

test('remote images queue host requests and never receive a remote replacement URL', () => {
    const f = fixture(['https://first.invalid/image', 'https://second.invalid/image']);
    assert.equal(f.emitted.length, 1);
    assert.equal(f.emitted[0].searchParams.get('type'), 'external_image');
    f.complete('data:image/png;base64,AQID');
    assert.equal(f.images[0].getAttribute('src'), 'data:image/png;base64,AQID');
    assert.equal(f.emitted.length, 2);
    f.complete('https://unapproved.invalid/replacement');
    assert.equal(f.images[1].getAttribute('src'), null);
});

test('streaming rerenders reuse resolved image data instead of retrieving again', () => {
    const f = fixture(['https://same.invalid/image', 'https://same.invalid/image']);
    f.complete('data:image/png;base64,AQID');
    assert.equal(f.emitted.length, 1);
    assert.equal(f.images[1].getAttribute('src'), 'data:image/png;base64,AQID');
    const rerender = f.add('https://same.invalid/image');
    assert.equal(rerender.getAttribute('src'), 'data:image/png;base64,AQID');
    assert.equal(f.emitted.length, 1);
});

test('denial is retained across streaming rerenders, with an explicit retry action', () => {
    const f = fixture(['https://same.invalid/image']);
    f.complete(null);
    const rerender = f.add('https://same.invalid/image');
    assert.equal(f.emitted.length, 1);
    assert.equal(rerender.getAttribute('src'), null);
    rerender.adjacent[0].listeners.click();
    assert.equal(f.emitted.length, 2);
});

test('data, file and credential-bearing images never trigger external host retrieval', () => {
    const f = fixture(['data:image/png;base64,AQID', 'file:///private/image.png',
        'https://user:password@server.invalid/image']);
    assert.equal(f.emitted.length, 0);
});

test('detached queued nodes are discarded before any host retrieval', () => {
    const f = fixture(['https://first.invalid/image', 'https://second.invalid/image']);
    f.images[1].isConnected = false;
    f.complete('data:image/png;base64,AQID');
    assert.equal(f.emitted.length, 1);
});

test('chat CSP prevents the browser bypassing host approval', () => {
    const template = fs.readFileSync(path.join(__dirname,
        '../src/SmartHopper.Core/UI/Chat/Resources/templates/chat-template.html'), 'utf8');
    assert.match(template, /img-src data: blob:;/);
    assert.match(template, /media-src data: blob:;/);
    assert.match(template, /connect-src 'none';/);
});
