/*
 * chat-bridge-smoke.cjs — jsdom smoke test for the WebChat shell.
 *
 * Composes the page exactly like ChatResourceManager.GetCompleteHtml()
 * (CSS + DOMPurify + vendored chat-ui.iife.js + chat-bridge.js), mounts it in
 * jsdom, then asserts:
 *   - every compatibility global the C# host drives via ExecuteScript exists
 *   - the toolkit shell mounted and legacy element ids were rewired
 *   - keyed upsert/patch/ordering pipeline, loaders, cards, attachments,
 *     autonomy overlay + context card, and every sh:// event emission
 *
 * Usage (no project dependency — jsdom can live in any scratch dir):
 *   npm install jsdom --prefix %TEMP%\sh-smoke   (one time)
 *   set NODE_PATH=%TEMP%\sh-smoke\node_modules
 *   node tools\chat-bridge-smoke.cjs
 *
 * Exits non-zero when a required global is missing or a check fails.
 */
'use strict';

const fs = require('fs');
const path = require('path');

let JSDOM;
try {
    ({ JSDOM } = require('jsdom'));
} catch {
    console.error('jsdom is not installed. Install it in a scratch dir, e.g.:');
    console.error('  npm install jsdom --prefix %TEMP%\\sh-smoke');
    console.error('  set NODE_PATH=%TEMP%\\sh-smoke\\node_modules  (or export NODE_PATH=...)');
    process.exit(2);
}

const RES = path.resolve(__dirname, '..', 'src', 'SmartHopper.Core', 'UI', 'Chat', 'Resources');
const vendor = fs.readFileSync(path.join(RES, 'vendor', 'chat-ui.iife.js'), 'utf8');
const bridge = fs.readFileSync(path.join(RES, 'js', 'chat-bridge.js'), 'utf8');
const purify = fs.readFileSync(path.join(RES, 'js', 'purify.min.js'), 'utf8');
const theme = fs.readFileSync(path.join(RES, 'css', 'chat-theme.css'), 'utf8');
const template = fs.readFileSync(path.join(RES, 'templates', 'chat-template.html'), 'utf8');

const html = template
    .replace('{{cssChat}}', '#x{}')
    .replace('{{cssTheme}}', theme)
    .replace('<script type="text/javascript" src="{{jsSanitizer}}"></script>', `<script>${purify}</script>`)
    .replace('{{jsVendor}}', vendor)
    .replace('{{jsChat}}', bridge)
    .replace('{{debugActionsLeft}}', '');

const dom = new JSDOM(html, {
    runScripts: 'dangerously',
    pretendToBeVisual: true,
    url: 'https://localhost/',
    beforeParse(window) { window.__nav = []; },
});

const events = [];
dom.virtualConsole.on('jsdomError', (e) => events.push('jsdomError: ' + String(e.message || e)));
dom.virtualConsole.on('error', (m) => events.push('console.error: ' + m));

const w = dom.window;
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
let failures = 0;
const check = (label, cond) => {
    console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}`);
    if (!cond) failures++;
};

(async () => {
    await wait(60);

    // All JS->C# emissions route through emitShEvent (== location.href): spy it.
    w.emitShEvent = function (url) { w.__nav.push(String(url)); };

    const REQUIRED = [
        'addMessage', 'upsertMessage', 'upsertMessageAfter', 'removeThinkingMessage',
        'addLoadingMessage', 'replaceLastMessageByRole', 'processCodeBlocks', 'processLinks',
        'scrollToBottom', 'setStatus', 'setProcessing', 'addAttachmentChip',
        'removeAttachmentChip', 'clearAttachments', 'setAttachEnabled', 'updateAutonomyUsage',
        'showUserQuestion', 'resolveUserQuestion', 'updateTaskPlan', 'showSuggestedPrompts',
        'showCanvasPointer', 'showToast', 'showPlanConsent', 'resolvePlanConsent',
        'resetMessages', 'getAllMessageKeys',
    ];
    const missing = REQUIRED.filter((g) => typeof w[g] !== 'function');
    check(`compat globals present (${REQUIRED.length})`, missing.length === 0);
    if (missing.length) console.log('   missing:', missing.join(', '));
    check('SH_READY set', w.SH_READY === true);
    check('ChatUI global + _chatView handle', typeof w.ChatUI === 'object' && !!w._chatView);
    check('cloud theme stamped on .chat-ui root',
        (w.document.querySelector('.chat-ui') || { dataset: {} }).dataset.theme === 'cloud');

    const cc = w.document.getElementById('chat-container');
    const input = w.document.getElementById('user-input');
    check('chat-container is the toolkit message list', !!cc && cc.classList.contains('cui-messages'));
    check('user-input is the toolkit composer input', !!input && input.classList.contains('cui-input'));
    check('attach/send/cancel buttons rewired',
        !!w.document.getElementById('attach-button')
        && !!w.document.getElementById('send-button')
        && !!w.document.getElementById('cancel-button'));

    w.upsertMessage('k1', '<div class="message assistant"><div class="message-content"><p>Hello</p></div></div>');
    await wait(80);
    const msg1 = cc.querySelector('.cui-msg .message.assistant');
    check('upsertMessage renders .message inside .cui-msg', !!msg1);
    check('data-key stamped from domKey', !!msg1 && msg1.dataset.key === 'k1');

    w.addMessage('<div class="message user"><div class="message-content">hi</div></div>');
    await wait(80);
    const userMsg = cc.querySelector('.cui-msg .message.user');
    check('addMessage renders anonymous bubble (unstamped)', !!userMsg && !userMsg.dataset.key);
    check('getAllMessageKeys returns keyed ids only',
        JSON.stringify(w.getAllMessageKeys()) === '["k1"]');

    w.upsertMessage('k1', JSON.stringify({ patch: 'append', html: '<p>tail</p>' }));
    await wait(80);
    check('patch payload appends in place',
        cc.querySelector('.message.assistant .message-content').textContent.includes('tail'));

    w.upsertMessage('k3', '<div class="message system"><div class="message-content">sys</div></div>');
    await wait(60);
    w.upsertMessageAfter('k1', 'k2', '<div class="message assistant"><div class="message-content">between</div></div>');
    await wait(80);
    const order = Array.from(cc.querySelectorAll('.message[data-key]')).map((m) => m.dataset.key);
    check('upsertMessageAfter honors followKey order', JSON.stringify(order) === '["k1","k2","k3"]');

    w.addLoadingMessage('assistant', 'Thinking...');
    await wait(60);
    const loader = cc.querySelector('.message.loading');
    check('loading bubble appended raw and last', !!loader && cc.lastElementChild === loader);
    w.addMessage('<div class="message assistant"><div class="message-content">x</div></div>');
    await wait(80);
    check('new message hoisted above trailing loader', cc.lastElementChild === loader);
    w.removeThinkingMessage();
    check('removeThinkingMessage removes loader', !cc.querySelector('.message.loading'));

    input.value = 'hello host';
    input.dispatchEvent(new w.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await wait(30);
    check('send emits sh://event?type=send&text=',
        (w.__nav || []).includes('sh://event?type=send&text=hello%20host'));

    w.addAttachmentChip('a1', 'pic.png', 'data:image/png;base64,AAAA');
    await wait(30);
    check('attachment chip renders with preview',
        !!w.document.querySelector('.cui-attachment img'));
    w.clearAttachments();
    check('clearAttachments empties strip', !w.document.querySelector('.cui-attachment'));

    w.setStatus('Working');
    w.setProcessing(true);
    check('status + processing reflected in toolkit shell',
        w.document.querySelector('.cui-status').textContent.includes('Working')
        && w.document.querySelector('.chat-ui').dataset.processing === 'true');
    w.setProcessing(false);

    w.updateAutonomyUsage({
        IsRunning: true, ElapsedSeconds: 62, MaxSeconds: 300,
        Tokens: 1234, MaxTokens: 50000, Interactions: 20,
    });
    await wait(30);
    check('autonomy overlay visible', !w.document.getElementById('autonomy-overlay').classList.contains('hidden'));
    check('context card fed with percent+elapsed',
        (w.document.querySelector('.cui-context-card') || { textContent: '' }).textContent.includes('21%'));

    w.showPlanConsent({ id: 'c1', goal: 'G', steps: [{ id: 's1', description: 'd' }] });
    const consent = cc.querySelector('.plan-consent-card');
    check('plan consent card renders', !!consent);
    consent.querySelectorAll('button')[1].click();
    check('consent approve emits consent&id&decision=approve',
        (w.__nav || []).includes('sh://event?type=consent&id=c1&decision=approve'));
    w.resolvePlanConsent('c1');
    check('resolvePlanConsent freezes card', consent.classList.contains('resolved'));

    w.showUserQuestion({ id: 'q1', question: 'Pick', options: ['A', 'B'] });
    cc.querySelectorAll('.user-question-option')[1].click();
    check('question option emits question&id&choice=1',
        (w.__nav || []).includes('sh://event?type=question&id=q1&choice=1'));

    w.showCanvasPointer({ id: 'p1', message: 'look' });
    cc.querySelector('.canvas-pointer-card button').click();
    check('canvas pointer emits canvas_pointer&id',
        (w.__nav || []).includes('sh://event?type=canvas_pointer&id=p1'));

    w.updateTaskPlan({ id: 't1', goal: 'G', tasks: [{ id: 't', description: 'x', status: 'in_progress' }] });
    check('task plan panel shown', !w.document.getElementById('task-plan-panel').classList.contains('hidden'));

    w.showSuggestedPrompts(['one', 'two']);
    const chips = w.document.querySelectorAll('#suggestions-strip .suggestion-chip');
    check('suggestion chips render', chips.length === 2);
    chips[0].click();
    check('chip fills composer without sending', input.value === 'one');

    w.resetMessages();
    check('resetMessages clears the list', cc.children.length === 0);

    const errs = events.filter((e) => !e.includes('navigation'));
    check('no console/jsdom errors', errs.length === 0);
    if (errs.length) console.log('   errors:', errs.join(' | '));

    console.log(failures === 0 ? '\nSMOKE OK' : `\n${failures} FAILURES`);
    process.exit(failures === 0 ? 0 : 1);
})();
