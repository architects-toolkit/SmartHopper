"use strict";
var ChatUI = (() => {
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __export = (target, all) => {
    for (var name in all)
      __defProp(target, name, { get: all[name], enumerable: true });
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

  // src/iife.ts
  var iife_exports = {};
  __export(iife_exports, {
    ChatUIViewElement: () => ChatUIViewElement,
    DEFAULT_STYLES: () => DEFAULT_STYLES,
    MarkdownStreamRenderer: () => MarkdownStreamRenderer,
    attachmentKindOf: () => attachmentKindOf,
    attachmentNameOf: () => attachmentNameOf,
    createAttachmentPreview: () => createAttachmentPreview,
    createChatView: () => createChatView,
    defineChatUIElement: () => defineChatUIElement,
    findStreamingSplit: () => findStreamingSplit,
    interactionPatchToMessage: () => interactionPatchToMessage,
    interactionToMessage: () => interactionToMessage,
    interactionsToMessages: () => interactionsToMessages,
    isChatAttachment: () => isChatAttachment,
    isSafeUrl: () => isSafeUrl,
    normalizeInteraction: () => normalizeInteraction,
    renderMarkdown: () => renderMarkdown,
    safeEmbedUrl: () => safeEmbedUrl
  });

  // src/interactions.ts
  var CHAT_ROLES = /* @__PURE__ */ new Set([
    "user",
    "assistant",
    "system",
    "tool"
  ]);
  var STATUSES = /* @__PURE__ */ new Set([
    "queued",
    "streaming",
    "done",
    "error"
  ]);
  var toChatRole = (role) => {
    if (role === "background") return "system";
    return typeof role === "string" && CHAT_ROLES.has(role) ? role : "assistant";
  };
  var isRecord = (v) => typeof v === "object" && v !== null && !Array.isArray(v);
  var sanitizeParts = (parts) => parts.filter(
    (p) => isRecord(p) && typeof p["type"] === "string"
  );
  var validStatus = (v) => typeof v === "string" && STATUSES.has(v) ? v : void 0;
  var autoSeq = 0;
  var autoId = () => `cui-${++autoSeq}`;
  function normalizeInteraction(input) {
    const raw = isRecord(input) ? input : {};
    const id = typeof raw["id"] === "string" && raw["id"] !== "" ? raw["id"] : autoId();
    const domKey = typeof raw["domKey"] === "string" ? raw["domKey"] : void 0;
    const role = raw["role"] === "background" || typeof raw["role"] === "string" && CHAT_ROLES.has(raw["role"]) ? raw["role"] : "assistant";
    const parts = sanitizeParts(Array.isArray(raw["parts"]) ? raw["parts"] : []);
    const status = validStatus(raw["status"]);
    const meta = isRecord(raw["meta"]) ? raw["meta"] : void 0;
    return {
      id,
      ...domKey !== void 0 ? { domKey } : {},
      role,
      parts,
      ...status !== void 0 ? { status } : {},
      ...meta !== void 0 ? { meta } : {}
    };
  }
  function interactionToMessage(interaction) {
    if (!isRecord(interaction)) {
      throw new TypeError("CanonicalInteraction must be an object");
    }
    if (typeof interaction.id !== "string" || interaction.id === "") {
      throw new TypeError("CanonicalInteraction.id must be a non-empty string");
    }
    if (!Array.isArray(interaction.parts)) {
      throw new TypeError("CanonicalInteraction.parts must be an array");
    }
    const domKey = typeof interaction.domKey === "string" ? interaction.domKey : void 0;
    const status = validStatus(interaction.status);
    const meta = isRecord(interaction.meta) ? interaction.meta : void 0;
    return {
      id: interaction.id,
      role: toChatRole(interaction.role),
      parts: sanitizeParts(interaction.parts),
      ...domKey !== void 0 ? { domKey } : {},
      ...status !== void 0 ? { status } : {},
      ...meta !== void 0 ? { meta } : {}
    };
  }
  function interactionsToMessages(list) {
    return list.map(interactionToMessage);
  }
  function interactionPatchToMessage(patch) {
    const status = validStatus(patch.status);
    return {
      ...typeof patch.id === "string" && patch.id !== "" ? { id: patch.id } : {},
      ...patch.role !== void 0 ? { role: toChatRole(patch.role) } : {},
      ...Array.isArray(patch.parts) ? { parts: sanitizeParts(patch.parts) } : {},
      ...typeof patch.domKey === "string" ? { domKey: patch.domKey } : {},
      ...status !== void 0 ? { status } : {},
      ...isRecord(patch.meta) ? { meta: patch.meta } : {}
    };
  }

  // src/markdown.ts
  var esc = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
  var decodeEntities = (url) => url.replace(
    /&#(x[0-9a-f]+|\d+);?/gi,
    (_, code) => String.fromCodePoint(
      code[0].toLowerCase() === "x" ? parseInt(code.slice(1), 16) : parseInt(code, 10)
    )
  );
  function isSafeUrl(url, allowDataImage = false) {
    const str = Array.from(decodeEntities(url)).filter((char) => char.charCodeAt(0) > 32).join("").toLowerCase();
    const protocol = /^([a-z][a-z0-9+.-]*):/.exec(str)?.[1];
    if (!protocol) return true;
    if (protocol === "data") {
      return allowDataImage && str.startsWith("data:image/") && !str.startsWith("data:image/svg");
    }
    return !["javascript", "vbscript", "file"].includes(protocol);
  }
  function safeEmbedUrl(url) {
    const cleaned = Array.from(decodeEntities(url)).filter((char) => char.charCodeAt(0) > 32).join("");
    const protocol = /^([a-z][a-z0-9+.-]*):/i.exec(cleaned)?.[1]?.toLowerCase();
    if (!protocol) return url;
    if (protocol === "data") {
      const lower = cleaned.toLowerCase();
      const ok = /^data:(image|audio|video)\//.test(lower) && !lower.startsWith("data:image/svg");
      return ok ? url : null;
    }
    return ["javascript", "vbscript", "file"].includes(protocol) ? null : url;
  }
  var PLACEHOLDER = "\0";
  function stash(state, html) {
    state.stashed.push(html);
    return `${PLACEHOLDER}${state.stashed.length - 1}${PLACEHOLDER}`;
  }
  function restore(state, s) {
    return s.replace(
      new RegExp(`${PLACEHOLDER}(\\d+)${PLACEHOLDER}`, "g"),
      (_, i) => state.stashed[Number(i)] ?? ""
    );
  }
  function linkOrImage(state, bang, label, url, title) {
    const isImage = bang === "!";
    const cleanUrl = url.trim();
    if (!isSafeUrl(cleanUrl, isImage)) return `${bang}[${label}](${cleanUrl})`;
    const titleAttr = title ? ` title="${title}"` : "";
    if (isImage) return stash(state, `<img src="${cleanUrl}" alt="${label}"${titleAttr}>`);
    const inner = inlineEmphasis(label);
    return stash(state, `<a href="${cleanUrl}"${titleAttr} target="_blank" rel="noopener noreferrer">${inner}</a>`);
  }
  function inlineEmphasis(s) {
    return s.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>").replace(/(^|[^A-Za-z0-9_])__([^_]+)__/g, "$1<strong>$2</strong>").replace(/\*([^*\n]+)\*/g, "<em>$1</em>").replace(/(^|[^A-Za-z0-9_])_([^_]+)_(?![A-Za-z0-9_])/g, "$1<em>$2</em>").replace(/~~([^~]+)~~/g, "<del>$1</del>");
  }
  function inline(escaped) {
    const state = { stashed: [] };
    let s = escaped;
    s = s.replace(/``([^`]+)``/g, (_, code) => stash(state, `<code>${code}</code>`)).replace(/`([^`\n]+)`/g, (_, code) => stash(state, `<code>${code}</code>`));
    s = s.replace(
      /(!?)\[([^\]]*)\]\(([^)\s]+)(?:\s+&quot;([^&]*)&quot;)?\)/g,
      (_, bang, label, url, title) => linkOrImage(state, bang, label, url, title)
    );
    s = s.replace(/&lt;((?:https?|mailto):[^\s&]+)&gt;/g, (_, url) => {
      if (!isSafeUrl(url)) return `&lt;${url}&gt;`;
      return stash(state, `<a href="${url}" target="_blank" rel="noopener noreferrer">${url}</a>`);
    });
    s = inlineEmphasis(s);
    return restore(state, s);
  }
  var HR = /^\s*(?:-{3,}|\*{3,}|_{3,})\s*$/;
  var UL_ITEM = /^\s*[-*+]\s+(.*)$/;
  var OL_ITEM = /^\s*\d{1,9}[.)]\s+(.*)$/;
  var TASK_ITEM = /^\[( |x|X)\]\s+(.*)$/;
  var HEADING = /^#{1,6}\s+(.*)$/;
  var TABLE_SEP = /^\s*\|?[\s:|-]*-[\s:|-]*\|?\s*$/;
  var MAX_QUOTE_DEPTH = 4;
  var splitRow = (line) => line.trim().replace(/^\||\|$/g, "").split("|").map((c) => c.trim());
  function renderTable(lines, start) {
    const headerLine = lines[start] ?? "";
    const sepLine = lines[start + 1] ?? "";
    if (!headerLine.includes("|") || !TABLE_SEP.test(sepLine)) return null;
    const header = splitRow(headerLine);
    if (header.length === 0) return null;
    const aligns = splitRow(sepLine).map((cell) => {
      const l = cell.startsWith(":");
      const r = cell.endsWith(":");
      return l && r ? "center" : r ? "right" : l ? "left" : "";
    });
    const rows = [];
    let i = start + 2;
    while (i < lines.length && (lines[i] ?? "").includes("|") && (lines[i] ?? "").trim() !== "") {
      rows.push(splitRow(lines[i] ?? ""));
      i++;
    }
    const alignAttr = (n) => aligns[n] ? ` style="text-align:${aligns[n]}"` : "";
    const th = header.map((c, n) => `<th${alignAttr(n)}>${inline(esc(c))}</th>`).join("");
    const trs = rows.map((r) => `<tr>${r.map((c, n) => `<td${alignAttr(n)}>${inline(esc(c))}</td>`).join("")}</tr>`).join("");
    return { html: `<table><thead><tr>${th}</tr></thead><tbody>${trs}</tbody></table>`, next: i };
  }
  function renderMarkdown(markdown, options) {
    const quoteDepth = options?.quoteDepth ?? 0;
    const blocks = [];
    const lines = markdown.replace(/\r\n?/g, "\n").split("\n");
    let i = 0;
    const list = { tag: "ul", items: [] };
    const flushList = () => {
      if (!list.items.length) return;
      blocks.push(`<${list.tag}>${list.items.map((item) => `<li>${item}</li>`).join("")}</${list.tag}>`);
      list.items.length = 0;
    };
    while (i < lines.length) {
      const line = lines[i] ?? "";
      if (line.trimStart().startsWith("```")) {
        flushList();
        const lang = line.trimStart().slice(3).trim();
        const buf = [];
        i++;
        while (i < lines.length && !(lines[i] ?? "").trimStart().startsWith("```")) buf.push(lines[i++] ?? "");
        if (i < lines.length) i++;
        const cls = lang ? ` class="language-${esc(lang)}"` : "";
        blocks.push(`<pre><code${cls}>${esc(buf.join("\n"))}</code></pre>`);
        continue;
      }
      const trimmed = line.trim();
      if (trimmed === "") {
        flushList();
        i++;
        continue;
      }
      if (HR.test(trimmed)) {
        const isLastLine = i === lines.length - 1;
        if (options?.streamingTail && isLastLine) {
          flushList();
          blocks.push(`<p>${inline(esc(trimmed))}</p>`);
        } else {
          flushList();
          blocks.push("<hr>");
        }
        i++;
        continue;
      }
      const heading = HEADING.exec(trimmed);
      if (heading) {
        flushList();
        const level = Math.min(6, (trimmed.match(/^#+/)?.[0] ?? "#").length);
        blocks.push(`<h${level}>${inline(esc(heading[1] ?? ""))}</h${level}>`);
        i++;
        continue;
      }
      if (trimmed.startsWith(">")) {
        flushList();
        const inner = [];
        while (i < lines.length && (lines[i] ?? "").trimStart().startsWith(">")) {
          inner.push((lines[i] ?? "").trimStart().replace(/^>\s?/, ""));
          i++;
        }
        const html = quoteDepth < MAX_QUOTE_DEPTH ? renderMarkdown(inner.join("\n"), { ...options, quoteDepth: quoteDepth + 1 }) : esc(inner.join("\n"));
        blocks.push(`<blockquote>${html}</blockquote>`);
        continue;
      }
      const table = renderTable(lines, i);
      if (table) {
        flushList();
        blocks.push(table.html);
        i = table.next;
        continue;
      }
      const ulMatch = UL_ITEM.exec(line);
      const olMatch = ulMatch ? null : OL_ITEM.exec(line);
      if (ulMatch || olMatch) {
        const tag = ulMatch ? "ul" : "ol";
        if (list.tag !== tag) flushList();
        list.tag = tag;
        let item = (ulMatch ?? olMatch)?.[1] ?? "";
        const task = TASK_ITEM.exec(item);
        if (task) {
          const checked = task[1].toLowerCase() === "x" ? " checked" : "";
          item = `<input type="checkbox" disabled${checked}> ${inline(esc(task[2] ?? ""))}`;
          list.items.push(item);
        } else {
          list.items.push(inline(esc(item)));
        }
        i++;
        continue;
      }
      flushList();
      const para = [];
      while (i < lines.length) {
        const l = lines[i] ?? "";
        const t = l.trim();
        if (t === "" || t.startsWith("```") || t.startsWith(">") || HEADING.test(t) || HR.test(t) || UL_ITEM.test(l) || OL_ITEM.test(l) || l.includes("|") && TABLE_SEP.test(lines[i + 1] ?? "")) {
          break;
        }
        para.push(t);
        i++;
      }
      if (para.length) {
        blocks.push(`<p>${para.map((p) => inline(esc(p))).join("<br>")}</p>`);
      } else {
        i++;
      }
    }
    flushList();
    return blocks.join("");
  }
  var PARAGRAPH_MARK = "\n\n";
  function findStreamingSplit(text) {
    let lastSafe = 0;
    let searchFrom = 0;
    let markIndex = text.indexOf(PARAGRAPH_MARK, searchFrom);
    while (markIndex !== -1) {
      const fences = text.substring(0, markIndex).match(/```/g);
      const isInsideCodeBlock = !!fences && fences.length % 2 === 1;
      const textAfterMark = text.substring(markIndex + PARAGRAPH_MARK.length);
      const isHorizontalRule = textAfterMark.startsWith("---") || "---".startsWith(textAfterMark);
      if (!isInsideCodeBlock && !isHorizontalRule) lastSafe = markIndex + PARAGRAPH_MARK.length;
      searchFrom = markIndex + 1;
      markIndex = text.indexOf(PARAGRAPH_MARK, searchFrom);
    }
    return lastSafe;
  }

  // src/markdown-stream.ts
  var SEGMENT_CLASS = "cui-md-seg";
  var TAIL_CLASS = "cui-md-tail";
  var MarkdownStreamRenderer = class {
    constructor(container, options) {
      this.text = "";
      /** Length of the prefix already committed to frozen segments. */
      this.frozen = 0;
      this.finished = false;
      this.container = container;
      if (options?.render) this.customRender = options.render;
      const doc = container.ownerDocument;
      this.tailEl = doc.createElement("div");
      this.tailEl.className = TAIL_CLASS;
      container.appendChild(this.tailEl);
    }
    /** Raw text accumulated so far. */
    get markdown() {
      return this.text;
    }
    /** Append a delta; completed paragraphs freeze, the tail re-renders. */
    append(delta) {
      if (!delta) return;
      if (this.finished) {
        const prior = this.text;
        this.reset();
        this.text = prior;
        this.finished = false;
      }
      this.text += delta;
      let split = findStreamingSplit(this.text);
      while (split > this.frozen) {
        const segment = this.text.substring(this.frozen, split - 2);
        this.freezeSegment(segment);
        this.frozen = split;
        split = findStreamingSplit(this.text);
      }
      this.renderTail();
    }
    /**
     * Final render: collapses frozen segments + tail into a single
     * canonical render of the full text (fixes constructs that spanned
     * segment boundaries, e.g. loose lists).
     */
    finish() {
      const full = this.text;
      this.container.replaceChildren();
      const doc = this.container.ownerDocument;
      this.tailEl = doc.createElement("div");
      this.tailEl.className = TAIL_CLASS;
      this.tailEl.innerHTML = this.render(full);
      this.container.appendChild(this.tailEl);
      this.finished = true;
    }
    /** Drop all rendered content and reset accumulation. */
    reset() {
      this.text = "";
      this.frozen = 0;
      this.finished = false;
      this.container.replaceChildren();
      const doc = this.container.ownerDocument;
      this.tailEl = doc.createElement("div");
      this.tailEl.className = TAIL_CLASS;
      this.container.appendChild(this.tailEl);
    }
    freezeSegment(markdown) {
      const doc = this.container.ownerDocument;
      const seg = doc.createElement("div");
      seg.className = SEGMENT_CLASS;
      seg.innerHTML = this.render(markdown);
      this.container.insertBefore(seg, this.tailEl);
    }
    renderTail() {
      const tail = this.text.substring(this.frozen);
      this.tailEl.innerHTML = tail === "" ? "" : this.renderTailHtml(tail);
    }
    render(markdown) {
      return this.customRender ? this.customRender(markdown) : renderMarkdown(markdown);
    }
    renderTailHtml(tail) {
      if (this.customRender) return this.customRender(tail);
      return renderMarkdown(tail, { streamingTail: true });
    }
  };

  // src/attachments.ts
  function isChatAttachment(item) {
    return typeof item === "object" && item !== null && !(item instanceof File) && typeof item.kind === "string" && typeof item.name === "string";
  }
  function attachmentKindOf(item) {
    if (isChatAttachment(item)) return item.kind;
    const mime = item.type ?? "";
    if (mime.startsWith("image/")) return "image";
    if (mime.startsWith("audio/")) return "audio";
    if (mime.startsWith("video/")) return "video";
    return "file";
  }
  function attachmentNameOf(item) {
    return isChatAttachment(item) ? item.name : item.name || "file";
  }
  function createAttachmentPreview(item) {
    if (isChatAttachment(item)) {
      if (item.url && isSafeUrl(item.url, true)) return Promise.resolve({ url: item.url, revoke: () => {
      } });
      if (!item.file) return Promise.resolve(null);
      return previewFromFile(item.file);
    }
    if (attachmentKindOf(item) !== "image") return Promise.resolve(null);
    return previewFromFile(item);
  }
  function previewFromFile(file) {
    if (!file.type.startsWith("image/")) return Promise.resolve(null);
    const URL_ = globalThis.URL;
    if (URL_ && typeof URL_.createObjectURL === "function") {
      try {
        const url = URL_.createObjectURL(file);
        return Promise.resolve({ url, revoke: () => URL_.revokeObjectURL(url) });
      } catch {
      }
    }
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onload = () => resolve(typeof reader.result === "string" ? { url: reader.result, revoke: () => {
      } } : null);
      reader.onerror = () => resolve(null);
      reader.readAsDataURL(file);
    });
  }

  // src/styles.ts
  var DEFAULT_STYLES = `
.chat-ui {
  --cui-bg: #ffffff;
  --cui-fg: #1c1c1e;
  --cui-muted: #6e6e73;
  --cui-border: #e3e3e6;
  --cui-accent: #4f7cff;
  --cui-accent-fg: #ffffff;
  --cui-user-bg: #eef2ff;
  --cui-assistant-bg: #f6f6f7;
  --cui-system-bg: transparent;
  --cui-error: #d34545;
  --cui-radius: 10px;
  --cui-font: inherit;
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  background: var(--cui-bg);
  color: var(--cui-fg);
  font: var(--cui-font);
}
.chat-ui[data-theme='dark'] {
  --cui-bg: #16161a;
  --cui-fg: #ececf1;
  --cui-muted: #9a9aa3;
  --cui-border: #2c2c32;
  --cui-user-bg: #26324f;
  --cui-assistant-bg: #202027;
}
.cui-status {
  flex: none;
  padding: 4px 12px;
  font-size: 12px;
  color: var(--cui-muted);
  border-bottom: 1px solid var(--cui-border);
  min-height: 20px;
}
.cui-context-card {
  display: none;
  flex: none;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  font-size: 11px;
  color: var(--cui-muted);
  border-bottom: 1px solid var(--cui-border);
}
.cui-context-card.visible { display: flex; }
.cui-context-meter { flex: 1; height: 4px; background: var(--cui-border); border-radius: 2px; overflow: hidden; }
.cui-context-meter > i { display: block; height: 100%; background: var(--cui-accent); }
.cui-messages {
  flex: 1 1 auto;
  min-height: 0;
  overflow-y: auto;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.cui-msg { max-width: 85%; }
.cui-msg .cui-bubble {
  padding: 8px 12px;
  border-radius: var(--cui-radius);
  background: var(--cui-assistant-bg);
  word-break: break-word;
}
.cui-msg[data-role='user'] { align-self: flex-end; }
.cui-msg[data-role='user'] .cui-bubble { background: var(--cui-user-bg); }
.cui-msg[data-role='system'] { align-self: center; }
.cui-msg[data-role='system'] .cui-bubble { background: var(--cui-system-bg); color: var(--cui-muted); font-size: 12px; }
.cui-msg[data-role='tool'] { align-self: stretch; max-width: 100%; }
.cui-msg[data-role='tool'] .cui-bubble { font-size: 12px; }
.cui-msg[data-status='error'] .cui-bubble { outline: 1px solid var(--cui-error); }
.cui-msg pre { overflow-x: auto; }
.cui-msg code { font-family: ui-monospace, monospace; font-size: 0.92em; }
.cui-msg table { border-collapse: collapse; font-size: 0.92em; }
.cui-msg th, .cui-msg td { border: 1px solid var(--cui-border); padding: 3px 8px; }
.cui-msg blockquote { margin: 4px 0; padding-left: 10px; border-left: 3px solid var(--cui-border); color: var(--cui-muted); }
.cui-part-tool_call .cui-tool-name { font-weight: 600; }
.cui-part-tool_result { border-left: 2px solid var(--cui-border); padding-left: 8px; margin-top: 4px; white-space: pre-wrap; }
.cui-part-tool_result[data-error='true'] { border-color: var(--cui-error); color: var(--cui-error); }
.cui-part-image img { max-width: 100%; border-radius: calc(var(--cui-radius) - 2px); display: block; }
.cui-part-media audio, .cui-part-media video { max-width: 100%; border-radius: calc(var(--cui-radius) - 2px); }
.cui-media-name { font-size: 11px; color: var(--cui-muted); margin-top: 2px; }
.cui-part-quote { margin: 0; padding-left: 10px; border-left: 3px solid var(--cui-accent); }
.cui-quote-body { white-space: pre-wrap; }
.cui-quote-source { display: block; margin-top: 4px; font-size: 11px; color: var(--cui-muted); font-style: normal; }
.cui-usage { display: inline-block; margin-top: 6px; padding: 2px 8px; font-size: 11px; color: var(--cui-muted); border: 1px solid var(--cui-border); border-radius: 999px; }
.cui-evidence { display: flex; gap: 6px; font-size: 12px; color: var(--cui-muted); }
.cui-evidence[data-level='warning'] { color: #a07a1f; }
.cui-evidence[data-level='error'] { color: var(--cui-error); }
.cui-evidence-body { display: flex; flex-direction: column; min-width: 0; }
.cui-evidence-detail { font-size: 11px; opacity: 0.75; white-space: pre-wrap; }
.cui-suggestions { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 6px; }
.cui-suggestion {
  border: 1px solid var(--cui-border);
  background: transparent;
  color: var(--cui-fg);
  border-radius: 999px;
  padding: 4px 12px;
  cursor: pointer;
  font-size: 12px;
}
.cui-suggestion:hover:not(:disabled) { border-color: var(--cui-accent); color: var(--cui-accent); }
.cui-suggestion:disabled { opacity: 0.55; cursor: default; }
.cui-card {
  border: 1px solid var(--cui-border);
  border-radius: var(--cui-radius);
  padding: 8px 10px;
  margin: 4px 0;
  font-size: 13px;
}
.cui-card-title { font-weight: 600; margin-bottom: 4px; }
.cui-card-desc { color: var(--cui-muted); margin-bottom: 6px; white-space: pre-wrap; }
.cui-card-actions { display: flex; gap: 6px; }
.cui-approve { border: none; border-radius: 6px; padding: 4px 12px; cursor: pointer; background: var(--cui-accent); color: var(--cui-accent-fg); font-weight: 600; }
.cui-reject { border: 1px solid var(--cui-border); border-radius: 6px; padding: 4px 12px; cursor: pointer; background: transparent; color: var(--cui-fg); }
.cui-approve:disabled, .cui-reject:disabled { opacity: 0.55; cursor: default; }
.cui-card[data-state='approved'] { border-color: #2f9e57; }
.cui-card[data-state='rejected'] { border-color: var(--cui-error); }
.cui-card-state { font-size: 12px; color: var(--cui-muted); font-style: italic; }
.cui-card-answer { margin-top: 4px; font-size: 12px; color: var(--cui-muted); font-style: italic; }
.cui-card[data-answered='true'] { opacity: 0.85; }
.cui-card-options { display: flex; flex-wrap: wrap; gap: 6px; margin: 4px 0; }
.cui-card-answer-row { display: flex; gap: 6px; margin-top: 6px; }
.cui-card-input {
  flex: 1; min-width: 0;
  border: 1px solid var(--cui-border);
  border-radius: 6px;
  padding: 4px 8px;
  font: inherit;
  color: var(--cui-fg);
  background: var(--cui-bg);
}
.cui-card-answer-row button {
  border: none; border-radius: 6px; padding: 4px 10px; cursor: pointer;
  background: var(--cui-accent); color: var(--cui-accent-fg);
}
.cui-card-answer-row button:disabled { opacity: 0.55; cursor: default; }
.cui-card-modelrow { display: flex; align-items: center; gap: 8px; }
.cui-card-label { color: var(--cui-muted); font-size: 12px; }
.cui-task-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 3px; }
.cui-task-step { display: flex; gap: 8px; align-items: baseline; }
.cui-task-icon { flex: none; width: 1em; text-align: center; }
.cui-task-step[data-status='done'] .cui-task-icon { color: #2f9e57; }
.cui-task-step[data-status='active'] .cui-task-icon { color: var(--cui-accent); }
.cui-task-step[data-status='error'] .cui-task-icon { color: var(--cui-error); }
.cui-task-step[data-status='done'] .cui-task-label { text-decoration: line-through; color: var(--cui-muted); }
.cui-task-detail { color: var(--cui-muted); font-size: 11px; }
.cui-messages-wrap { position: relative; flex: 1 1 auto; min-height: 0; display: flex; flex-direction: column; }
.cui-jump {
  display: none;
  position: absolute;
  right: 16px; bottom: 12px;
  border: 1px solid var(--cui-border);
  background: var(--cui-bg);
  color: var(--cui-fg);
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
  box-shadow: 0 1px 4px rgba(0,0,0,0.18);
}
.cui-jump.visible { display: block; }
.cui-attachments {
  display: none;
  flex: none;
  flex-wrap: wrap;
  gap: 8px;
  padding: 8px 12px 0;
}
.cui-attachments.visible { display: flex; }
.cui-attachment {
  display: flex; align-items: center; gap: 6px;
  border: 1px solid var(--cui-border);
  border-radius: var(--cui-radius);
  padding: 4px 8px;
  font-size: 12px;
  max-width: 180px;
}
.cui-attachment-thumb { width: 32px; height: 32px; object-fit: cover; border-radius: 6px; }
.cui-attachment-icon { font-size: 16px; }
.cui-attachment-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.cui-attachment-rm { border: none; background: none; color: var(--cui-muted); cursor: pointer; padding: 0 2px; }
.cui-attachment-rm:hover { color: var(--cui-fg); }
.cui-model {
  flex: none; max-width: 140px;
  border: 1px solid var(--cui-border);
  border-radius: 6px;
  padding: 4px 6px;
  font: inherit; font-size: 12px;
  color: var(--cui-fg);
  background: var(--cui-bg);
}
.cui-actions { display: flex; gap: 6px; margin-top: 6px; }
.cui-actions button {
  border: 1px solid var(--cui-border);
  background: transparent;
  color: var(--cui-fg);
  border-radius: 6px;
  padding: 3px 10px;
  cursor: pointer;
  font-size: 12px;
}
.cui-queue {
  display: none;
  flex: none;
  border-top: 1px solid var(--cui-border);
  max-height: 140px;
  overflow-y: auto;
}
.cui-queue.visible { display: block; }
.cui-queue-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border-bottom: 1px solid var(--cui-border);
  font-size: 13px;
}
.cui-queue-item:last-child { border-bottom: none; }
.cui-queue-text { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.cui-queue-item button {
  flex: none;
  border: none;
  background: transparent;
  color: var(--cui-muted);
  cursor: pointer;
  font-size: 12px;
}
.cui-queue-item button:hover { color: var(--cui-fg); }
.cui-quote {
  display: none;
  flex: none;
  align-items: flex-start;
  gap: 8px;
  margin: 8px 12px 0;
  padding: 6px 10px;
  border-left: 3px solid var(--cui-accent);
  background: var(--cui-assistant-bg);
  border-radius: 0 var(--cui-radius) var(--cui-radius) 0;
  font-size: 12px;
}
.cui-quote.visible { display: flex; }
.cui-quote-text { flex: 1; white-space: pre-wrap; max-height: 64px; overflow: hidden; }
.cui-quote button { border: none; background: none; color: var(--cui-muted); cursor: pointer; }
.cui-composer {
  flex: none;
  display: flex;
  align-items: flex-end;
  gap: 8px;
  padding: 10px 12px;
  border-top: 1px solid var(--cui-border);
}
.cui-input {
  flex: 1;
  resize: none;
  border: 1px solid var(--cui-border);
  border-radius: var(--cui-radius);
  padding: 8px 10px;
  font: inherit;
  color: var(--cui-fg);
  background: var(--cui-bg);
  min-height: 20px;
  max-height: 160px;
}
.cui-send, .cui-cancel {
  flex: none;
  border: none;
  border-radius: var(--cui-radius);
  padding: 8px 14px;
  cursor: pointer;
  background: var(--cui-accent);
  color: var(--cui-accent-fg);
  font-weight: 600;
}
.cui-cancel { display: none; background: var(--cui-error); }
.chat-ui[data-processing='true'] .cui-send { display: none; }
.chat-ui[data-processing='true'] .cui-cancel { display: block; }
.cui-send:disabled { opacity: 0.5; cursor: default; }
@media (prefers-reduced-motion: reduce) {
  .chat-ui *, .chat-ui *::before, .chat-ui *::after {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
  }
}
`;

  // src/chat-view.ts
  var NS = "chat-ui";
  var NEAR_BOTTOM_PX = 24;
  function el(doc, tag, cls, text) {
    const node = doc.createElement(tag);
    if (cls) node.className = cls;
    if (text !== void 0) node.textContent = text;
    return node;
  }
  var partKey = (message) => message.domKey ?? message.id;
  var compactNumber = (n) => n >= 1e6 ? `${(n / 1e6).toFixed(1)}M` : n >= 1e3 ? `${(n / 1e3).toFixed(1)}k` : String(n);
  function usageText(usage) {
    const bits = [];
    if (usage.label) bits.push(usage.label);
    if (usage.tokensIn != null) bits.push(`${compactNumber(usage.tokensIn)} in`);
    if (usage.tokensOut != null) bits.push(`${compactNumber(usage.tokensOut)} out`);
    if (usage.cost != null) bits.push(`${usage.currency ?? "$"}${usage.cost.toFixed(4)}`);
    if (usage.durationMs != null) bits.push(`${(usage.durationMs / 1e3).toFixed(1)}s`);
    return bits.join(" \xB7 ");
  }
  function createChatView(container, options) {
    const doc = container.ownerDocument;
    const win = doc.defaultView;
    const features = options.features ?? {};
    const customRenderers = options.partRenderers ?? {};
    const transport = options.transport;
    const mdRender = (md) => (options.renderMarkdown ?? renderMarkdown)(md);
    const attachmentsEnabled = features.attachments === true && (!features.inputModes || features.inputModes.includes("attachments"));
    if (!options.unstyled) {
      const style = doc.createElement("style");
      style.textContent = DEFAULT_STYLES;
      doc.head.appendChild(style);
    }
    const root = el(doc, "div", NS);
    root.dataset.processing = "false";
    const statusBar = el(doc, "div", "cui-status");
    const contextCard = el(doc, "div", "cui-context-card");
    const contextLabel = el(doc, "span", "", "Context");
    const contextMeter = el(doc, "div", "cui-context-meter");
    const contextFill = el(doc, "i");
    contextMeter.appendChild(contextFill);
    const contextValue = el(doc, "span");
    contextCard.append(contextLabel, contextMeter, contextValue);
    const messagesWrap = el(doc, "div", "cui-messages-wrap");
    const messages = el(doc, "div", "cui-messages");
    messages.setAttribute("role", "log");
    messages.setAttribute("aria-live", "polite");
    const jump = el(doc, "button", "cui-jump", "\u2193 Latest");
    jump.type = "button";
    messagesWrap.append(messages, jump);
    const queuePanel = el(doc, "div", "cui-queue");
    const quote = el(doc, "div", "cui-quote");
    const quoteText = el(doc, "div", "cui-quote-text");
    const quoteDismiss = el(doc, "button", "", "\u2715");
    quoteDismiss.setAttribute("aria-label", "Remove quote");
    quote.append(quoteText, quoteDismiss);
    const attachStrip = el(doc, "div", "cui-attachments");
    const composer = el(doc, "div", "cui-composer");
    const input = doc.createElement("textarea");
    input.className = "cui-input";
    input.rows = 1;
    input.placeholder = options.placeholder ?? "Type a message\u2026";
    const modelSelect = doc.createElement("select");
    modelSelect.className = "cui-model";
    modelSelect.hidden = true;
    modelSelect.setAttribute("aria-label", "Model");
    const attach = el(doc, "button", "cui-attach", "\u{1F4CE}");
    attach.type = "button";
    attach.hidden = !attachmentsEnabled;
    const send = el(doc, "button", "cui-send", "Send");
    send.type = "button";
    const cancel = el(doc, "button", "cui-cancel", "Stop");
    cancel.type = "button";
    const fileInput = transport.requestAttachments ? null : doc.createElement("input");
    if (fileInput) {
      fileInput.type = "file";
      fileInput.multiple = true;
      fileInput.hidden = true;
      if (features.attachmentAccept) fileInput.accept = features.attachmentAccept;
    }
    composer.append(modelSelect, input, attach, send, cancel, ...fileInput ? [fileInput] : []);
    root.append(statusBar, contextCard, messagesWrap, queuePanel, quote, attachStrip, composer);
    container.appendChild(root);
    const byKey = /* @__PURE__ */ new Map();
    const streams = /* @__PURE__ */ new Map();
    let stagedQuote = null;
    let staged = [];
    let previews = [];
    let destroyed = false;
    let pinned = true;
    const isNearBottom = () => messages.scrollHeight - messages.clientHeight - messages.scrollTop <= NEAR_BOTTOM_PX;
    const applyScroll = (force = false) => {
      if (force || pinned) {
        messages.scrollTop = messages.scrollHeight;
        pinned = true;
      }
      jump.classList.toggle("visible", !pinned);
    };
    messages.addEventListener("scroll", () => {
      pinned = isNearBottom();
      jump.classList.toggle("visible", !pinned);
    });
    jump.addEventListener("click", () => applyScroll(true));
    const autosize = () => {
      input.style.height = "auto";
      const max = parseFloat(win?.getComputedStyle(input).maxHeight ?? "") || 160;
      const next = Math.min(input.scrollHeight, max);
      input.style.height = `${next}px`;
      input.style.overflowY = input.scrollHeight > max ? "auto" : "hidden";
    };
    const revokePreviews = () => {
      for (const p of previews) {
        try {
          p.revoke();
        } catch {
        }
      }
      previews = [];
    };
    let previewGen = 0;
    const renderAttachmentStrip = () => {
      revokePreviews();
      attachStrip.replaceChildren();
      attachStrip.classList.toggle("visible", staged.length > 0);
      const gen = ++previewGen;
      staged.forEach((item, i) => {
        const kind = attachmentKindOf(item);
        const chip = el(doc, "div", "cui-attachment");
        chip.dataset.kind = kind;
        if (kind === "image") {
          const img = doc.createElement("img");
          img.className = "cui-attachment-thumb";
          img.alt = attachmentNameOf(item);
          void createAttachmentPreview(item).then((p) => {
            if (!p || gen !== previewGen) {
              p?.revoke();
              return;
            }
            previews.push(p);
            img.src = p.url;
          });
          chip.appendChild(img);
        } else {
          const icon = kind === "audio" ? "\u{1F50A}" : kind === "video" ? "\u{1F3AC}" : "\u{1F4C4}";
          chip.appendChild(el(doc, "span", "cui-attachment-icon", icon));
        }
        chip.appendChild(el(doc, "span", "cui-attachment-name", attachmentNameOf(item)));
        const rm = el(doc, "button", "cui-attachment-rm", "\u2715");
        rm.type = "button";
        rm.setAttribute("aria-label", `Remove ${attachmentNameOf(item)}`);
        rm.addEventListener("click", () => {
          staged.splice(i, 1);
          renderAttachmentStrip();
        });
        chip.appendChild(rm);
        attachStrip.appendChild(chip);
      });
    };
    const stageItems = (items) => {
      if (!attachmentsEnabled || items.length === 0) return;
      staged.push(...items);
      renderAttachmentStrip();
    };
    const clearStaged = () => {
      staged = [];
      renderAttachmentStrip();
    };
    fileInput?.addEventListener("change", () => {
      const files = fileInput.files ? Array.from(fileInput.files) : [];
      fileInput.value = "";
      stageItems(files);
    });
    input.addEventListener("paste", (ev) => {
      if (!attachmentsEnabled) return;
      const files = ev.clipboardData?.files;
      if (files && files.length) {
        ev.preventDefault();
        stageItems(Array.from(files));
      }
    });
    const setModels = (models, selected) => {
      modelSelect.replaceChildren();
      for (const m of models) {
        const opt = doc.createElement("option");
        opt.value = m.id;
        opt.textContent = m.hint ? `${m.label ?? m.id} \u2014 ${m.hint}` : m.label ?? m.id;
        modelSelect.appendChild(opt);
      }
      if (selected !== void 0) modelSelect.value = selected;
      modelSelect.hidden = !(features.modelPicker && models.length > 0);
    };
    modelSelect.addEventListener("change", () => transport.selectModel?.(modelSelect.value));
    const interactive = () => features.messageActions === true && !!transport.messageAction;
    const reportAction = (message, action, payload) => transport.messageAction?.(message.id, action, payload);
    const renderUsage = (usage) => el(doc, "div", "cui-usage", usageText(usage) || "usage");
    const renderSuggestions = (part, message) => {
      const row = el(doc, "div", "cui-suggestions");
      for (const prompt of part.prompts) {
        const chip = el(doc, "button", "cui-suggestion", prompt);
        chip.type = "button";
        chip.addEventListener("click", () => {
          if (interactive()) {
            reportAction(message, "suggestion", { text: prompt });
          } else {
            input.value = prompt;
            autosize();
            input.focus();
          }
        });
        row.appendChild(chip);
      }
      return row;
    };
    const renderTaskPlan = (data) => {
      const card = el(doc, "div", "cui-card");
      card.dataset.kind = "task_plan";
      if (data?.title) card.appendChild(el(doc, "div", "cui-card-title", data.title));
      const list = el(doc, "ol", "cui-task-list");
      for (const step of data?.steps ?? []) {
        const item = el(doc, "li", "cui-task-step");
        item.dataset.status = step.status ?? "pending";
        const glyphs = { pending: "\u25CB", active: "\u25B8", done: "\u2713", error: "\u2715" };
        item.appendChild(el(doc, "span", "cui-task-icon", glyphs[item.dataset.status] ?? "\u25CB"));
        const labelWrap = el(doc, "span", "cui-task-label", step.label);
        item.appendChild(labelWrap);
        if (step.detail) item.appendChild(el(doc, "span", "cui-task-detail", step.detail));
        list.appendChild(item);
      }
      card.appendChild(list);
      return card;
    };
    const renderQuestion = (data, message) => {
      const card = el(doc, "div", "cui-card");
      card.dataset.kind = "question";
      card.appendChild(el(doc, "div", "cui-card-title", data?.question ?? ""));
      const action = data?.action ?? "question";
      const answered = data?.answer !== void 0;
      const canAnswer = interactive() && !answered;
      const markAnswered = (answer) => {
        card.dataset.answered = "true";
        card.querySelectorAll("button, input, select").forEach((c) => c.disabled = true);
        card.appendChild(el(doc, "div", "cui-card-answer", answer));
        reportAction(message, action, { answer });
      };
      if (answered) {
        card.dataset.answered = "true";
        card.appendChild(el(doc, "div", "cui-card-answer", data.answer));
      }
      if (data?.options?.length && !answered) {
        const opts = el(doc, "div", "cui-card-options");
        for (const opt of data.options) {
          const chip = el(doc, "button", "cui-suggestion", opt);
          chip.type = "button";
          chip.disabled = !canAnswer;
          chip.addEventListener("click", () => markAnswered(opt));
          opts.appendChild(chip);
        }
        card.appendChild(opts);
      }
      const showFreeText = !answered && (data?.allowFreeText ?? !data?.options?.length);
      if (showFreeText) {
        const row = el(doc, "div", "cui-card-answer-row");
        const field = doc.createElement("input");
        field.type = "text";
        field.className = "cui-card-input";
        field.placeholder = data?.placeholder ?? "Type an answer\u2026";
        field.disabled = !canAnswer;
        const btn = el(doc, "button", "", data?.submitLabel ?? "Send");
        btn.type = "button";
        btn.disabled = !canAnswer;
        const submitAnswer = () => {
          const answer = field.value.trim();
          if (answer) markAnswered(answer);
        };
        btn.addEventListener("click", submitAnswer);
        field.addEventListener("keydown", (ev) => {
          const inComposition = composing || ev.isComposing || ev.keyCode === 229;
          if (ev.key === "Enter" && !inComposition) submitAnswer();
        });
        row.append(field, btn);
        card.appendChild(row);
      }
      return card;
    };
    const renderApproval = (data, message) => {
      const card = el(doc, "div", "cui-card");
      card.dataset.kind = "approval";
      const action = data?.action ?? "approval";
      const state = data?.state ?? "pending";
      card.dataset.state = state;
      if (data?.title) card.appendChild(el(doc, "div", "cui-card-title", data.title));
      if (data?.description) card.appendChild(el(doc, "div", "cui-card-desc", data.description));
      if (state === "pending") {
        const row = el(doc, "div", "cui-card-actions");
        const canAct = interactive();
        const decide = (decision) => {
          card.dataset.state = decision;
          row.replaceChildren(el(doc, "span", "cui-card-state", decision === "approved" ? "Approved" : "Rejected"));
          reportAction(message, action, { decision });
        };
        const yes = el(doc, "button", "cui-approve", data?.approveLabel ?? "Approve");
        yes.type = "button";
        yes.disabled = !canAct;
        yes.addEventListener("click", () => decide("approved"));
        const no = el(doc, "button", "cui-reject", data?.rejectLabel ?? "Reject");
        no.type = "button";
        no.disabled = !canAct;
        no.addEventListener("click", () => decide("rejected"));
        row.append(yes, no);
        card.appendChild(row);
      } else {
        card.appendChild(el(doc, "div", "cui-card-state", state === "approved" ? "Approved" : "Rejected"));
      }
      return card;
    };
    const normalizeModel = (m) => typeof m === "string" ? { id: m } : m;
    const renderModelSelect = (data, message) => {
      const card = el(doc, "div", "cui-card");
      card.dataset.kind = "model_select";
      const action = data?.action ?? "model_select";
      const row = el(doc, "div", "cui-card-modelrow");
      if (data?.label) row.appendChild(el(doc, "span", "cui-card-label", data.label));
      const select = doc.createElement("select");
      select.className = "cui-model";
      for (const raw of data?.options ?? []) {
        const m = normalizeModel(raw);
        const opt = doc.createElement("option");
        opt.value = m.id;
        opt.textContent = m.hint ? `${m.label ?? m.id} \u2014 ${m.hint}` : m.label ?? m.id;
        select.appendChild(opt);
      }
      if (data?.selected !== void 0) select.value = data.selected;
      select.disabled = !interactive();
      select.addEventListener("change", () => reportAction(message, action, { model: select.value }));
      row.appendChild(select);
      card.appendChild(row);
      return card;
    };
    const renderCard = (part, message) => {
      const custom = customRenderers[`card:${part.kind}`];
      if (custom) return custom(part, { message, ownerDocument: doc });
      switch (part.kind) {
        case "task_plan":
          return renderTaskPlan(part.data);
        case "question":
          return renderQuestion(part.data, message);
        case "approval":
          return renderApproval(part.data, message);
        case "model_select":
          return renderModelSelect(part.data, message);
        default:
          return customRenderers["card"]?.(part, { message, ownerDocument: doc }) ?? null;
      }
    };
    const renderPart = (part, message) => {
      if (part.type === "card") return renderCard(part, message);
      const custom = customRenderers[part.type];
      if (custom) return custom(part, { message, ownerDocument: doc });
      switch (part.type) {
        case "text":
          return el(doc, "div", "cui-part-text", part.text);
        case "markdown": {
          const node = el(doc, "div", "cui-part-markdown");
          node.innerHTML = mdRender(part.text);
          return node;
        }
        case "html": {
          const node = el(doc, "div", "cui-part-html");
          node.innerHTML = part.html;
          return node;
        }
        case "image": {
          const src = safeEmbedUrl(part.src);
          if (!src) return null;
          const wrap = el(doc, "div", "cui-part-image");
          const img = doc.createElement("img");
          img.src = src;
          img.alt = part.alt ?? part.name ?? "";
          img.loading = "lazy";
          wrap.appendChild(img);
          return wrap;
        }
        case "audio": {
          const src = safeEmbedUrl(part.src);
          if (!src) return null;
          const wrap = el(doc, "div", "cui-part-media");
          const audio = doc.createElement("audio");
          audio.controls = true;
          audio.preload = "metadata";
          audio.src = src;
          wrap.appendChild(audio);
          if (part.name) wrap.appendChild(el(doc, "div", "cui-media-name", part.name));
          return wrap;
        }
        case "video": {
          const src = safeEmbedUrl(part.src);
          if (!src) return null;
          const wrap = el(doc, "div", "cui-part-media");
          const video = doc.createElement("video");
          video.controls = true;
          video.preload = "metadata";
          video.src = src;
          if (part.poster) {
            const poster = safeEmbedUrl(part.poster);
            if (poster) video.poster = poster;
          }
          wrap.appendChild(video);
          if (part.name) wrap.appendChild(el(doc, "div", "cui-media-name", part.name));
          return wrap;
        }
        case "quote": {
          const node = el(doc, "blockquote", "cui-part-quote");
          node.appendChild(el(doc, "div", "cui-quote-body", part.text));
          if (part.source) node.appendChild(el(doc, "cite", "cui-quote-source", part.source));
          return node;
        }
        case "suggestions":
          return renderSuggestions(part, message);
        case "usage":
          return renderUsage(part.usage);
        case "evidence": {
          const node = el(doc, "div", "cui-evidence");
          const level = part.level ?? "info";
          node.dataset.level = level;
          const icons = { info: "\u2139", warning: "\u26A0", error: "\u2715" };
          node.appendChild(el(doc, "span", "cui-evidence-icon", icons[level]));
          const body = el(doc, "span", "cui-evidence-body");
          body.appendChild(el(doc, "span", "cui-evidence-text", part.text));
          if (part.detail) body.appendChild(el(doc, "span", "cui-evidence-detail", part.detail));
          node.appendChild(body);
          return node;
        }
        case "tool_call": {
          const node = el(doc, "div", "cui-part-tool_call");
          node.appendChild(el(doc, "div", "cui-tool-name", `\u2699 ${part.name}`));
          if (part.args) node.appendChild(el(doc, "pre", "", part.args));
          if (part.status) node.dataset.status = part.status;
          return node;
        }
        case "tool_result": {
          const node = el(doc, "div", "cui-part-tool_result", part.content);
          node.dataset.error = String(Boolean(part.isError));
          return node;
        }
        case "divider":
          return el(doc, "hr");
        default:
          return null;
      }
    };
    const renderActions = (message) => {
      if (!features.messageActions || !transport.messageAction) return null;
      const actions = message.meta?.["actions"];
      if (!Array.isArray(actions) || actions.length === 0) return null;
      const row = el(doc, "div", "cui-actions");
      for (const action of actions) {
        const a = action;
        if (typeof a?.id !== "string" || typeof a?.label !== "string") continue;
        const btn = el(doc, "button", "", a.label);
        btn.type = "button";
        btn.addEventListener("click", () => transport.messageAction?.(message.id, a.id, a.payload));
        row.appendChild(btn);
      }
      return row.childElementCount ? row : null;
    };
    const renderCostChip = (message) => {
      if (!features.costChip) return null;
      const usage = message.meta?.["usage"];
      if (!usage || typeof usage !== "object") return null;
      if (message.parts.some((p) => p.type === "usage")) return null;
      return renderUsage(usage);
    };
    const addMessage = (message) => {
      const key = partKey(message);
      const existing = byKey.get(key);
      if (existing) existing.node.remove();
      streams.delete(key);
      const node = el(doc, "div", "cui-msg");
      node.dataset.role = message.role;
      if (message.status) node.dataset.status = message.status;
      const body = el(doc, "div", "cui-bubble");
      const partEls = [];
      for (const part of message.parts) {
        const rendered = renderPart(part, message);
        partEls.push(rendered);
        if (rendered) body.appendChild(rendered);
      }
      const chip = renderCostChip(message);
      if (chip) body.appendChild(chip);
      const actions = renderActions(message);
      if (actions) body.appendChild(actions);
      node.appendChild(body);
      messages.appendChild(node);
      byKey.set(key, { message, node, body, partEls });
      applyScroll(message.role === "user");
    };
    const renderQueue = (items) => {
      queuePanel.replaceChildren();
      queuePanel.classList.toggle("visible", items.length > 0);
      for (const item of items) {
        const row = el(doc, "div", "cui-queue-item");
        const text = el(doc, "span", "cui-queue-text", item.text);
        row.appendChild(text);
        const mkBtn = (label, title, onClick) => {
          const b = el(doc, "button", "", label);
          b.type = "button";
          b.title = title;
          b.addEventListener("click", onClick);
          row.appendChild(b);
        };
        if (transport.editQueued) mkBtn("\u270E", "Edit", () => {
          const next = doc.defaultView?.prompt("Edit message", item.text);
          if (next != null && next !== item.text) transport.editQueued?.(item.id, next);
        });
        if (transport.sendQueuedNow) mkBtn("\u27A4", "Send now", () => transport.sendQueuedNow?.(item.id));
        if (transport.removeQueued) mkBtn("\u2715", "Remove", () => transport.removeQueued?.(item.id));
        queuePanel.appendChild(row);
      }
    };
    const submit = () => {
      const text = input.value.trim();
      if (!text && staged.length === 0) return;
      const submission = staged.length ? { text, attachments: [...staged] } : { text };
      input.value = "";
      if (fileInput) fileInput.value = "";
      clearStaged();
      autosize();
      void transport.send(submission);
    };
    send.addEventListener("click", () => {
      submit();
      applyScroll(true);
    });
    cancel.addEventListener("click", () => transport.cancel?.());
    attach.addEventListener("click", () => {
      if (transport.requestAttachments) void transport.requestAttachments();
      else fileInput?.click();
    });
    quoteDismiss.addEventListener("click", () => handle.clearQuote());
    let composing = false;
    input.addEventListener("compositionstart", () => composing = true);
    input.addEventListener("compositionend", () => composing = false);
    input.addEventListener("keydown", (ev) => {
      const inComposition = composing || ev.isComposing || ev.keyCode === 229;
      if (ev.key === "Enter" && !ev.shiftKey) {
        if (!inComposition) {
          ev.preventDefault();
          submit();
        }
      } else if (ev.key === "Escape" && !inComposition && stagedQuote != null) {
        handle.clearQuote();
      }
    });
    input.addEventListener("input", autosize);
    const streamOpts = () => options.renderMarkdown ? { render: options.renderMarkdown } : void 0;
    const lastStreamablePart = (parts) => {
      for (let i = parts.length - 1; i >= 0; i--) {
        const t = parts[i]?.type;
        if (t === "text" || t === "markdown") return i;
      }
      return -1;
    };
    const handle = {
      addMessage,
      updateMessage(domKey, patch) {
        const entry = byKey.get(domKey);
        if (!entry) return;
        const merged = { ...entry.message, ...patch };
        addMessage(merged);
      },
      addInteraction(interaction) {
        addMessage(interactionToMessage(interaction));
      },
      updateInteraction(domKey, patch) {
        const entry = byKey.get(domKey);
        if (!entry) return;
        const merged = { ...entry.message, ...interactionPatchToMessage(patch) };
        addMessage(merged);
      },
      appendDelta(domKey, delta) {
        const entry = byKey.get(domKey);
        if (!entry || !delta) return;
        const parts = [...entry.message.parts];
        const partEls = [...entry.partEls];
        let idx = lastStreamablePart(parts);
        if (idx === -1) {
          const fmt = options.deltaFormat ?? "text";
          const part = fmt === "markdown" ? { type: "markdown", text: delta } : { type: "text", text: delta };
          parts.push(part);
          const rendered = renderPart(part, entry.message);
          partEls.push(rendered);
          if (rendered) {
            entry.body.appendChild(rendered);
            if (part.type === "markdown") {
              rendered.replaceChildren();
              const renderer = new MarkdownStreamRenderer(rendered, streamOpts());
              renderer.append(delta);
              streams.set(domKey, { renderer, el: rendered });
            }
          }
        } else {
          const part = parts[idx];
          const text = (part.type === "text" || part.type === "markdown" ? part.text : "") + delta;
          parts[idx] = { ...part, text };
          const target = partEls[idx];
          if (part.type === "markdown" && target) {
            let st = streams.get(domKey);
            if (!st || st.el !== target) {
              target.replaceChildren();
              const renderer = new MarkdownStreamRenderer(target, streamOpts());
              renderer.append(text.slice(0, text.length - delta.length));
              st = { renderer, el: target };
              streams.set(domKey, st);
            }
            st.renderer.append(delta);
          } else if (part.type === "text" && target) {
            target.textContent = text;
          }
        }
        byKey.set(domKey, { ...entry, message: { ...entry.message, parts }, partEls });
        applyScroll();
      },
      removeMessage(domKey) {
        const entry = byKey.get(domKey);
        if (entry) {
          entry.node.remove();
          byKey.delete(domKey);
          streams.delete(domKey);
        }
      },
      clearMessages() {
        byKey.clear();
        streams.clear();
        messages.replaceChildren();
      },
      setQueue(items) {
        if (features.queue) renderQueue(items);
      },
      setProcessing(processing) {
        root.dataset.processing = String(processing);
        input.disabled = false;
      },
      setStatus(status) {
        statusBar.textContent = status;
      },
      setContextUsage(usage) {
        contextCard.classList.toggle("visible", usage != null);
        if (!usage) return;
        const pct = Math.max(0, Math.min(100, Math.round(usage.percent)));
        contextFill.style.width = `${pct}%`;
        contextValue.textContent = usage.elapsedMs != null ? `${pct}% \xB7 ${Math.round(usage.elapsedMs / 1e3)}s` : `${pct}%`;
      },
      stageQuote(text) {
        stagedQuote = text;
        quoteText.textContent = text;
        quote.classList.add("visible");
      },
      getQuote: () => stagedQuote,
      clearQuote() {
        stagedQuote = null;
        quoteText.textContent = "";
        quote.classList.remove("visible");
      },
      setModels(models, selected) {
        if (!features.modelPicker) return;
        setModels(models, selected);
      },
      getSelectedModel() {
        return modelSelect.hidden ? null : modelSelect.value;
      },
      stageAttachments(items) {
        stageItems(items);
      },
      getAttachments: () => [...staged],
      clearAttachments() {
        clearStaged();
      },
      scrollToBottom(force) {
        applyScroll(force !== false);
      },
      destroy() {
        if (destroyed) return;
        destroyed = true;
        byKey.clear();
        streams.clear();
        revokePreviews();
        root.remove();
      }
    };
    return handle;
  }

  // src/adapters/element.ts
  var HTMLElementBase = globalThis.HTMLElement ?? class {
  };
  var ChatUIViewElement = class extends HTMLElementBase {
    constructor() {
      super(...arguments);
      this.view = null;
      this.pending = null;
    }
    /** The live view handle once `init` (or `options` + connect) ran. */
    get handle() {
      return this.view;
    }
    /**
     * Initialize the view. Calling this is required before `handle` exists;
     * safe to call lazily once transport/features are known.
     */
    init(options) {
      if (this.view) this.view.destroy();
      this.view = createChatView(this, options);
      return this.view;
    }
    disconnectedCallback() {
      this.view?.destroy();
      this.view = null;
    }
  };
  function defineChatUIElement(tag = "chat-ui-view") {
    const registry = globalThis.customElements;
    if (!registry || registry.get(tag)) return;
    registry.define(tag, ChatUIViewElement);
  }
  return __toCommonJS(iife_exports);
})();
