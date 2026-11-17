/*
 * chat-fallback-controls.cjs — dependency-free vm test for the raw-DOM
 * (fallback) control toggles in both chat shells.
 *
 * Extracts setAttachmentPreparation/setProcessing from chat-bridge.js and
 * chat-script.js, evaluates them in a vm sandbox against a fake document,
 * then asserts the queue/staging state machine:
 *   idle                -> input/send editable, "Send", cancel disabled
 *   processing          -> input/send editable, "Queue", cancel enabled
 *   staging (processing)-> input/send disabled, cancel still enabled
 *   staging done        -> input/send editable, "Queue", cancel enabled
 *   idle                -> input/send editable, "Send", cancel disabled
 *
 * Usage: node tools\chat-fallback-controls.cjs
 * Exits non-zero when a check fails.
 */
'use strict';

const fs = require('fs');
const path = require('path');
const vm = require('vm');

const RES = path.resolve(__dirname, '..', 'src', 'SmartHopper.Core', 'UI', 'Chat', 'Resources', 'js');

let failures = 0;
const check = (label, cond) => {
    console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}`);
    if (!cond) failures++;
};

/** Extracts the control-toggle slice (state lets + both setters) from a shell source. */
function slice(source, marker) {
    const start = source.indexOf('let _attachmentPreparationActive = false;');
    if (start < 0) throw new Error('state lets not found');
    const end = source.indexOf(marker, start);
    if (end < 0) throw new Error(`end marker not found: ${marker}`);
    return source.slice(start, end);
}

function makeElement() {
    return {
        disabled: false,
        textContent: 'Send',
        attrs: {},
        dataset: {},
        style: {},
        classList: { toggle() {}, add() {}, remove() {}, contains() { return false; } },
        setAttribute(k, v) { this.attrs[k] = v; },
        getAttribute(k) { return this.attrs[k]; },
    };
}

function makeSandbox() {
    const els = {
        'user-input': makeElement(),
        'send-button': makeElement(),
        'cancel-button': makeElement(),
        'spinner': makeElement(),
    };
    const sandbox = {
        console: { log() {}, warn() {}, error() {} },
        document: { getElementById: id => els[id] || null },
        // Toolkit view absent: the raw-DOM fallback branch is exercised.
        _view: null,
        _els: {},
        freezeAutonomyOverlay() {},
        hideAutonomyOverlay() {},
        removeThinkingMessage() { return false; },
        _autonomyAwaitingRun: false,
    };
    return { sandbox, els };
}

function runShell(name, file, endMarker) {
    const source = fs.readFileSync(path.join(RES, file), 'utf8');
    const code = slice(source, endMarker);
    const { sandbox, els } = makeSandbox();
    vm.createContext(sandbox);
    vm.runInContext(code, sandbox, { filename: file });
    sandbox.setProcessing(false); // normalize: cancel button disabled at idle

    const input = els['user-input'];
    const send = els['send-button'];
    const cancel = els['cancel-button'];

    // idle
    check(`${name}: idle — input editable`, input.disabled === false);
    check(`${name}: idle — send editable + "Send"`, send.disabled === false && send.textContent === 'Send');
    check(`${name}: idle — cancel disabled`, cancel.disabled === true);

    // processing: composer stays editable, send relabels "Queue", cancel enabled
    sandbox.setProcessing(true);
    check(`${name}: processing — input editable`, input.disabled === false);
    check(`${name}: processing — send editable + "Queue"`, send.disabled === false && send.textContent === 'Queue');
    check(`${name}: processing — cancel enabled`, cancel.disabled === false);

    // staging while processing: input/send locked, cancel stays enabled
    sandbox.setAttachmentPreparation(true);
    check(`${name}: staging — input disabled`, input.disabled === true);
    check(`${name}: staging — send disabled`, send.disabled === true);
    check(`${name}: staging — cancel still enabled`, cancel.disabled === false);

    // staging finished while still processing: composer editable again, "Queue"
    sandbox.setAttachmentPreparation(false);
    check(`${name}: staging done — input editable`, input.disabled === false);
    check(`${name}: staging done — send editable + "Queue"`, send.disabled === false && send.textContent === 'Queue');
    check(`${name}: staging done — cancel enabled`, cancel.disabled === false);

    // back to idle
    sandbox.setProcessing(false);
    check(`${name}: back to idle — input editable`, input.disabled === false);
    check(`${name}: back to idle — send editable + "Send"`, send.disabled === false && send.textContent === 'Send');
    check(`${name}: back to idle — cancel disabled`, cancel.disabled === true);

    // staging alone (no turn): composer locked, cancel enabled for abort
    sandbox.setAttachmentPreparation(true);
    check(`${name}: staging-only — input disabled`, input.disabled === true);
    check(`${name}: staging-only — send disabled`, send.disabled === true);
    check(`${name}: staging-only — cancel enabled`, cancel.disabled === false);
    sandbox.setAttachmentPreparation(false);
    check(`${name}: staging-only done — input editable`, input.disabled === false);
    check(`${name}: staging-only done — cancel disabled`, cancel.disabled === true);
}

// chat-bridge.js keeps the same contract; its slice ends where setQueue starts.
runShell('chat-bridge', 'chat-bridge.js', 'function setQueue(items)');
runShell('chat-script', 'chat-script.js', 'function addAttachmentChip(');

console.log(failures === 0 ? 'ALL CHECKS PASSED' : `${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
