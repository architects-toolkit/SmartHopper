/*
 * chat-bridge-smoke.cjs — jsdom smoke test for the WebChat shell.
 *
 * Composes the page exactly like ChatResourceManager.GetCompleteHtml()
 * (CSS + DOMPurify + vendored chat-ui.iife.js + chat-bridge.js), mounts it in
 * jsdom, then asserts:
 *   - every compatibility global the C# host drives via ExecuteScript exists
 *   - the toolkit shell mounted and legacy element ids were rewired
 *   - keyed upsert/patch/ordering pipeline, loaders, attachments,
 *     autonomy overlay + context card, and every sh:// event emission
 *   - each structured legacy payload maps to its canonical chat-ui part/card:
 *     consent -> card:approval, task plan -> card:task_plan, question ->
 *     card:question, suggested prompts -> suggestions, canvas pointer ->
 *     evidence + meta.actions, notice-role .message bubbles -> evidence,
 *     metrics footer -> usage, model picker -> setModels/selectModel
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
        'showAutonomyContinue', 'resolveAutonomyContinue',
        'resetMessages', 'getAllMessageKeys', 'setModels', 'setQueue',
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
    check('.cui-msg chrome stamped with data-key + data-iid',
        !!cc.querySelector('.cui-msg[data-key="k1"][data-iid="k1"]'));

    w.addMessage('<div class="message user"><div class="message-content">hi</div></div>');
    await wait(80);
    const userMsg = cc.querySelector('.cui-msg .message.user');
    check('addMessage renders anonymous bubble (unstamped)', !!userMsg && !userMsg.dataset.key);
    check('anonymous .cui-msg gets data-iid but no data-key',
        !!cc.querySelector('.cui-msg[data-iid] .message.user')
        && !userMsg.closest('.cui-msg').dataset.key);
    check('getAllMessageKeys returns keyed ids only',
        JSON.stringify(w.getAllMessageKeys()) === '["k1"]');

    w.upsertMessage('k1', JSON.stringify({ patch: 'append', html: '<p>tail</p>' }));
    await wait(80);
    check('patch payload appends in place',
        cc.querySelector('.message.assistant .message-content').textContent.includes('tail'));

    // Notice-role bubbles map to native `evidence` parts; the keyed wrapper
    // keeps the domKey so the keyed pipeline still addresses the message.
    w.upsertMessage('k3', '<div class="message system"><div class="message-content">sys</div></div>');
    await wait(60);
    const ev3 = cc.querySelector('.cui-msg[data-key="k3"] .cui-evidence');
    check('short system notice maps to evidence part', !!ev3 && ev3.dataset.level === 'info');
    check('evidence message keyed on .cui-msg (role system)',
        !!cc.querySelector('.cui-msg[data-key="k3"][data-role="system"]'));

    w.upsertMessageAfter('k1', 'k2', '<div class="message assistant"><div class="message-content">between</div></div>');
    await wait(80);
    check('upsertMessageAfter honors followKey order',
        JSON.stringify(w.getAllMessageKeys()) === '["k1","k2","k3"]');

    // Same-key upsert refreshes the evidence text through the toolkit.
    w.upsertMessage('k3', '<div class="message warning"><div class="message-content">now warning</div></div>');
    await wait(80);
    const ev3b = cc.querySelector('.cui-msg[data-key="k3"] .cui-evidence');
    check('notice re-upsert maps warning level + swaps text',
        !!ev3b && ev3b.dataset.level === 'warning' && ev3b.textContent.includes('now warning'));

    // Long / block-structured system content keeps the collapsible .message path.
    const longBody = 'x'.repeat(700);
    w.upsertMessage('k4', `<div class="message system"><div class="message-content"><p>${longBody}</p></div></div>`);
    await wait(60);
    check('long system message stays html part',
        !!cc.querySelector('.cui-msg[data-key="k4"] .message.system')
        && !cc.querySelector('.cui-msg[data-key="k4"] .cui-evidence'));
    w.upsertMessage('k5', '<div class="message system"><div class="message-content"><ul><li>a</li><li>b</li></ul></div></div>');
    await wait(60);
    check('block-structured system message stays html part',
        !!cc.querySelector('.cui-msg[data-key="k5"] .message.system'));

    // Metrics footer -> `usage` part; the footer chrome is stripped from the html part.
    w.upsertMessage('k6', '<div class="message assistant"><div class="message-content"><p>r</p></div>' +
        '<div class="message-footer"><span class="metrics-icon" data-in="1234" data-out="56" ' +
        'data-provider="prov" data-model="m1" data-reason="stop" data-context-usage="12.3%"></span></div></div>');
    await wait(80);
    const usage6 = cc.querySelector('.cui-msg[data-key="k6"] .cui-usage');
    check('metrics footer maps to usage part',
        !!usage6 && usage6.textContent.includes('1.2k in') && usage6.textContent.includes('56 out'));
    check('usage part label carries provider/model/ctx',
        !!usage6 && usage6.textContent.includes('prov') && usage6.textContent.includes('m1')
        && usage6.textContent.includes('12.3%'));
    check('message-footer chrome stripped from html part',
        !cc.querySelector('.cui-msg[data-key="k6"] .metrics-icon'));

    w.upsertMessage('k7', '<div class="message assistant"><div class="message-content"><p>r2</p></div>' +
        '<div class="message-footer"><span class="metrics-icon hidden" data-in="0" data-out="0"></span></div></div>');
    await wait(80);
    check('hidden metrics icon emits no usage part',
        !cc.querySelector('.cui-msg[data-key="k7"] .cui-usage'));

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

    // Plan consent -> card:approval; approve button reports through messageAction.
    w.showPlanConsent({ id: 'c1', goal: 'G', summary: 'S', steps: [{ id: 's1', description: 'd', tool: 't', mutatesCanvas: true }], assumptions: ['a1'], successCriteria: ['ok'] });
    const consentMsg = cc.querySelector('.cui-msg[data-iid="consent-c1"]');
    const consentCard = consentMsg && consentMsg.querySelector('.cui-card[data-kind="approval"]');
    check('plan consent maps to card:approval', !!consentCard);
    check('consent card composes steps + sections into description',
        !!consentCard && consentCard.textContent.includes('1. d') && consentCard.textContent.includes('- a1'));
    const consentBtns = consentCard ? consentCard.querySelectorAll('button') : [];
    check('approval card renders approve+reject buttons', consentBtns.length === 2);
    if (consentBtns.length) consentBtns[0].click();
    check('consent approve emits consent&id&decision=approve',
        (w.__nav || []).includes('sh://event?type=consent&id=c1&decision=approve'));
    w.resolvePlanConsent('c1');
    check('resolvePlanConsent freezes card',
        !!consentCard && consentCard.dataset.resolved === 'true'
        && Array.from(consentCard.querySelectorAll('button')).every((b) => b.disabled));

    // Autonomy-continue -> card:approval with autonomy-<n> refId, same consent routing.
    w.showAutonomyContinue({ id: 'autonomy-1', title: 'Autonomous run limit reached', description: 'The agent stopped because it reached the 10:00 time limit for autonomous runs.', approveLabel: 'Continue working', rejectLabel: 'Stop' });
    const autoMsg = cc.querySelector('.cui-msg[data-iid="consent-autonomy-1"]');
    const autoCard = autoMsg && autoMsg.querySelector('.cui-card[data-kind="approval"]');
    check('autonomy-continue maps to card:approval', !!autoCard);
    check('autonomy card carries custom labels + description',
        !!autoCard && autoCard.textContent.includes('Continue working')
        && autoCard.textContent.includes('10:00 time limit'));
    const autoBtns = autoCard ? autoCard.querySelectorAll('button') : [];
    if (autoBtns.length) autoBtns[0].click();
    check('autonomy approve emits consent&id=autonomy-1&decision=approve',
        (w.__nav || []).includes('sh://event?type=consent&id=autonomy-1&decision=approve'));
    w.resolveAutonomyContinue('autonomy-1');
    check('resolveAutonomyContinue freezes card',
        !!autoCard && autoCard.dataset.resolved === 'true'
        && Array.from(autoCard.querySelectorAll('button')).every((b) => b.disabled));

    // User question -> card:question; chip answers map back to choice index.
    w.showUserQuestion({ id: 'q1', question: 'Pick', options: ['A', 'B'] });
    const qMsg = cc.querySelector('.cui-msg[data-iid="question-q1"]');
    const qCard = qMsg && qMsg.querySelector('.cui-card[data-kind="question"]');
    check('user question maps to card:question', !!qCard);
    const qOpts = qCard ? qCard.querySelectorAll('.cui-card-options .cui-suggestion') : [];
    if (qOpts.length > 1) qOpts[1].click();
    check('question chip emits question&id&choice=1',
        (w.__nav || []).includes('sh://event?type=question&id=q1&choice=1'));

    w.showUserQuestion({ id: 'q2', question: 'Say', options: ['X'] });
    const q2Card = cc.querySelector('.cui-msg[data-iid="question-q2"] .cui-card[data-kind="question"]');
    const q2Input = q2Card && q2Card.querySelector('.cui-card-input');
    const q2Send = q2Card && q2Card.querySelector('.cui-card-answer-row button');
    check('question keeps free-text field', !!q2Input && !!q2Send);
    if (q2Input && q2Send) {
        q2Input.value = 'custom answer';
        q2Send.click();
    }
    check('question free text emits question&id&text=',
        (w.__nav || []).includes('sh://event?type=question&id=q2&text=custom%20answer'));
    w.resolveUserQuestion('q2');
    check('resolveUserQuestion freezes card',
        !!q2Card && q2Card.dataset.resolved === 'true');

    // Canvas pointer -> evidence line + meta.actions button -> same event.
    w.showCanvasPointer({ id: 'p1', message: 'look' });
    const ptrMsg = cc.querySelector('.cui-msg[data-iid="pointer-p1"]');
    check('canvas pointer maps to evidence + action row',
        !!ptrMsg && !!ptrMsg.querySelector('.cui-evidence') && !!ptrMsg.querySelector('.cui-actions button'));
    const ptrBtn = ptrMsg && ptrMsg.querySelector('.cui-actions button');
    if (ptrBtn) ptrBtn.click();
    check('canvas pointer emits canvas_pointer&id',
        (w.__nav || []).includes('sh://event?type=canvas_pointer&id=p1'));

    // Task plan -> card:task_plan in the list (one plan at a time).
    w.updateTaskPlan({ id: 't1', goal: 'G', tasks: [{ id: 't', description: 'x', status: 'in_progress' }] });
    const tpMsg = cc.querySelector('.cui-msg[data-iid="sh-task-plan"]');
    const tpCard = tpMsg && tpMsg.querySelector('.cui-card[data-kind="task_plan"]');
    check('task plan maps to card:task_plan', !!tpCard);
    check('task status maps in_progress -> active',
        !!tpCard && !!tpCard.querySelector('.cui-task-step[data-status="active"]'));
    w.updateTaskPlan({ id: 't2', goal: 'G2', tasks: [{ id: 't', description: 'y', status: 'completed' }] });
    check('task plan upsert keeps a single card',
        cc.querySelectorAll('.cui-card[data-kind="task_plan"]').length === 1
        && !!cc.querySelector('.cui-task-step[data-status="done"]'));

    // Suggested prompts -> `suggestions` part; chip fills composer, never sends.
    const navBeforeSuggest = (w.__nav || []).length;
    w.showSuggestedPrompts(['one', 'two']);
    const sugMsg = cc.querySelector('.cui-msg[data-iid="sh-suggestions"]');
    const chips = sugMsg ? sugMsg.querySelectorAll('.cui-suggestions .cui-suggestion') : [];
    check('suggested prompts map to suggestions part', chips.length === 2);
    if (chips.length) chips[0].click();
    check('chip fills composer without sending',
        input.value === 'one' && (w.__nav || []).length === navBeforeSuggest);

    // Model picker -> setModels global + selectModel transport -> sh://event?type=model.
    w.setModels([{ id: 'm1', label: 'Model One' }, 'm2'], 'm2');
    await wait(20);
    const modelSel = w.document.querySelector('.cui-model');
    check('setModels reveals composer model picker',
        !!modelSel && modelSel.hidden === false && modelSel.options.length === 2);
    if (modelSel) {
        modelSel.value = 'm1';
        modelSel.dispatchEvent(new w.Event('change', { bubbles: true }));
    }
    check('model change emits sh://event?type=model',
        (w.__nav || []).includes('sh://event?type=model&model=m1'));

    // Session queue: while a turn runs the composer stays editable (type-ahead)
    // and the send button relabels to "Queue" — mid-turn sends travel to the
    // host which enqueues them FIFO instead of rejecting them.
    w.setProcessing(true);
    await wait(20);
    const sendBtn = w.document.getElementById('send-button');
    const cancelBtn = w.document.getElementById('cancel-button');
    check('busy turn keeps composer editable (queue type-ahead)',
        input.disabled === false && sendBtn.disabled === false);
    check('busy turn relabels send to Queue + enables cancel',
        sendBtn.textContent === 'Queue' && cancelBtn.disabled === false
        && w.document.querySelector('.chat-ui').dataset.processing === 'true');
    input.value = 'mid-turn send';
    input.dispatchEvent(new w.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await wait(30);
    check('busy-turn send still emits sh://event?type=send',
        (w.__nav || []).includes('sh://event?type=send&text=mid-turn%20send'));

    // The host mirrors the merged outgoing queue through setQueue; the toolkit
    // renders one row per item with per-row actions routed to type=queue events.
    w.setQueue([
        { id: 'mq1', text: 'queued while busy' },
        { id: 'mq2', text: 'agent injected', meta: { agent: '→ scraper' } },
    ]);
    await wait(20);
    const queuePanel = w.document.querySelector('.cui-queue');
    const queueRows = queuePanel ? queuePanel.querySelectorAll('.cui-queue-item') : [];
    check('setQueue renders the queue panel rows', !!queuePanel && queueRows.length === 2);
    const byline = queueRows[1] && queueRows[1].querySelector('.cui-queue-meta');
    check('queue meta.agent renders as row byline',
        !!byline && byline.textContent.includes('scraper'));
    const rowBtns = queueRows[0] ? Array.from(queueRows[0].querySelectorAll('button')) : [];
    check('queue row renders edit/send-now/remove actions', rowBtns.length === 3);
    if (rowBtns[2]) rowBtns[2].click();
    await wait(20);
    check('queue remove emits type=queue&action=remove',
        (w.__nav || []).includes('sh://event?type=queue&action=remove&id=mq1'));
    const row2Btns = queueRows[1] ? Array.from(queueRows[1].querySelectorAll('button')) : [];
    if (row2Btns[1]) row2Btns[1].click();
    await wait(20);
    check('queue send-now emits type=queue&action=submit-now',
        (w.__nav || []).includes('sh://event?type=queue&action=submit-now&id=mq2'));
    w.prompt = () => 'edited body';
    if (row2Btns[0]) row2Btns[0].click();
    await wait(20);
    check('queue edit emits type=queue&action=edit with text',
        (w.__nav || []).includes('sh://event?type=queue&action=edit&id=mq2&text=edited%20body'));
    w.setQueue([]);
    await wait(20);
    check('setQueue([]) hides the queue panel',
        !w.document.querySelector('.cui-queue.visible'));
    w.setProcessing(false);
    await wait(20);
    check('idle restores Send label + editable composer',
        sendBtn.textContent === 'Send' && input.disabled === false
        && w.document.querySelector('.chat-ui').dataset.processing === 'false');

    w.resetMessages();
    check('resetMessages clears the list', cc.children.length === 0);

    // Plugin registry cleared with the list: a fresh consent must render again.
    w.showPlanConsent({ id: 'c9', goal: 'after reset' });
    check('plugin card renders after resetMessages',
        !!cc.querySelector('.cui-msg[data-iid="consent-c9"] .cui-card[data-kind="approval"]'));

    const errs = events.filter((e) => !e.includes('navigation'));
    check('no console/jsdom errors', errs.length === 0);
    if (errs.length) console.log('   errors:', errs.join(' | '));

    console.log(failures === 0 ? '\nSMOKE OK' : `\n${failures} FAILURES`);
    process.exit(failures === 0 ? 0 : 1);
})();
