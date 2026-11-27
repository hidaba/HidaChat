'use strict';

const fs = require('fs');
const path = require('path');
const vm = require('vm');
const assert = require('assert');

const scriptPath = path.join(__dirname, '..', 'Scripts', 'notification.js');
const rawScript = fs.readFileSync(scriptPath, 'utf8').replace('$$BRIDGE_TOKEN$$', JSON.stringify('test-token-123'));

function makeElement(spec) {
  const el = {
    tagName: (spec.tag || 'DIV').toUpperCase(),
    className: spec.className || '',
    id: spec.id || '',
    textContent: spec.textContent || '',
    outerHTML: spec.outerHTML || '<div></div>',
    attrs: spec.attrs || {},
    children: [],
    parentNode: null,
    parentElement: null,
    classList: {
      contains(cls) {
        return (el.className || '').split(/\s+/).includes(cls);
      }
    },
    getAttribute(name) {
      return Object.prototype.hasOwnProperty.call(el.attrs, name) ? el.attrs[name] : null;
    },
    querySelector(sel) {
      return el.querySelectorAll(sel)[0] || null;
    },
    querySelectorAll(sel) {
      const parts = sel.split(',').map(s => s.trim()).filter(Boolean);
      const out = [];
      function walk(node) {
        for (const child of node.children) {
          if (parts.some(p => matchesSelector(child, p))) out.push(child);
          walk(child);
        }
      }
      walk(el);
      return out;
    },
    closest(sel) {
      const parts = sel.split(',').map(s => s.trim()).filter(Boolean);
      let cur = el;
      while (cur) {
        if (parts.some(p => matchesSelector(cur, p))) return cur;
        cur = cur.parentElement;
      }
      return null;
    },
    contains(other) {
      let cur = other;
      while (cur) {
        if (cur === el) return true;
        cur = cur.parentElement;
      }
      return false;
    },
    cloneNode(deep) {
      const copy = makeElement({
        tag: el.tagName,
        className: el.className,
        id: el.id,
        textContent: el.textContent,
        attrs: Object.assign({}, el.attrs)
      });
      if (deep) {
        for (const c of el.children) {
          copy.appendChild(c.cloneNode(true));
        }
      }
      return copy;
    },
    appendChild(child) {
      child.parentNode = el;
      child.parentElement = el;
      el.children.push(child);
      return child;
    },
    removeChild(child) {
      const idx = el.children.indexOf(child);
      if (idx !== -1) el.children.splice(idx, 1);
      child.parentNode = null;
      child.parentElement = null;
      el.textContent = el.children.map(c => c.textContent).join(' ').trim();
      return child;
    }
  };
  return el;
}

function matchesSelector(el, sel) {
  if (!el) return false;
  if (sel.includes(' ')) {
    const tokens = sel.split(/\s+/);
    if (!matchesSimple(el, tokens[tokens.length - 1])) return false;
    let cur = el.parentElement;
    for (let i = tokens.length - 2; i >= 0; i--) {
      while (cur && !matchesSimple(cur, tokens[i])) cur = cur.parentElement;
      if (!cur) return false;
      cur = cur.parentElement;
    }
    return true;
  }
  return matchesSimple(el, sel);
}

function matchesSimple(el, sel) {
  if (sel === 'li' || sel === 'span' || sel === 'div' || sel === 'a' || sel === 'h3') {
    return el.tagName.toLowerCase() === sel;
  }
  if (sel.startsWith('.')) {
    const classes = sel.slice(1).split('.');
    return classes.every(c => el.classList.contains(c));
  }
  const attrContains = sel.match(/^([a-z0-9]*)?\[([^=\]]+)\*="([^"]+)"(?:\s*i)?\]$/i);
  if (attrContains) {
    if (attrContains[1] && el.tagName.toLowerCase() !== attrContains[1].toLowerCase()) return false;
    const val = attrContains[2] === 'class' ? el.className : (el.getAttribute(attrContains[2]) || '');
    return val.toLowerCase().includes(attrContains[3].toLowerCase());
  }
  const attrStarts = sel.match(/^([a-z0-9]*)?\[([^=\]]+)\^="([^"]+)"\]$/i);
  if (attrStarts) {
    if (attrStarts[1] && el.tagName.toLowerCase() !== attrStarts[1].toLowerCase()) return false;
    const val = attrStarts[2] === 'id' ? el.id : (el.getAttribute(attrStarts[2]) || '');
    return val.startsWith(attrStarts[3]);
  }
  const attrEq = sel.match(/^([a-z0-9]*)?\[([^=\]]+)="([^"]+)"\]$/i);
  if (attrEq) {
    if (attrEq[1] && el.tagName.toLowerCase() !== attrEq[1].toLowerCase()) return false;
    const val = el.getAttribute(attrEq[2]);
    return val === attrEq[3];
  }
  const attrPresent = sel.match(/^([a-z0-9]*)?\[([^=\]]+)\]$/i);
  if (attrPresent) {
    if (attrPresent[1] && el.tagName.toLowerCase() !== attrPresent[1].toLowerCase()) return false;
    return el.getAttribute(attrPresent[2]) !== null;
  }
  return false;
}

function runScenario(hostname, title, rootEl) {
  const messages = [];
  function FakeNotification() {}
  FakeNotification.permission = 'granted';

  const doc = {
    title,
    hidden: false,
    readyState: 'complete',
    head: null,
    querySelector(sel) { return rootEl.querySelector(sel); },
    querySelectorAll(sel) { return rootEl.querySelectorAll(sel); },
    addEventListener() {}
  };

  const sandbox = {
    window: null,
    self: null,
    top: null,
    document: doc,
    Document: { prototype: {} },
    HTMLDocument: { prototype: {} },
    location: { hostname },
    navigator: {},
    console: { log() {}, warn() {}, error() {} },
    setTimeout() { return 1; },
    clearTimeout() {},
    MutationObserver: function() { this.observe = function() {}; },
    Notification: FakeNotification,
    ServiceWorkerRegistration: { prototype: { showNotification() {} } },
    chrome: { webview: { postMessage(msg) { messages.push(msg); } } }
  };
  sandbox.window = sandbox;
  sandbox.self = sandbox;
  sandbox.top = sandbox;
  sandbox.addEventListener = function() {};

  vm.runInNewContext(rawScript, sandbox);
  return { sandbox, messages };
}

// 1. WhatsApp Web unread extraction
{
  const root = makeElement({ tag: 'div' });
  const row = makeElement({ tag: 'div', attrs: { 'data-testid': 'cell-frame-container' } });
  const titleWrap = makeElement({ tag: 'div', attrs: { 'data-testid': 'cell-frame-title' } });
  titleWrap.appendChild(makeElement({ tag: 'span', textContent: 'Mario Rossi', attrs: { title: 'Mario Rossi' } }));
  const secWrap = makeElement({ tag: 'div', attrs: { 'data-testid': 'cell-frame-secondary' } });
  secWrap.appendChild(makeElement({ tag: 'span', textContent: 'Ciao!\nCi vediamo alle 15?', attrs: { dir: 'ltr' } }));
  const badge = makeElement({ tag: 'span', textContent: '2', attrs: { 'data-testid': 'unread-count' } });
  row.appendChild(titleWrap);
  row.appendChild(secWrap);
  row.appendChild(badge);
  root.appendChild(row);

  const { messages } = runScenario('web.whatsapp.com', '(2) WhatsApp', root);
  const unread = messages.find(m => m.type === 'UNREAD_COUNT_CHANGED');
  assert(unread, 'WhatsApp UNREAD_COUNT_CHANGED emitted');
  assert.strictEqual(unread.unreadCount, 2);
  assert.strictEqual(unread.latestChatTitle, 'Mario Rossi');
  assert.strictEqual(unread.latestMessageText, 'Ciao! Ci vediamo alle 15?');
}

// 2. Telegram Web unread extraction (strips trailing badge digits from subtitle)
{
  const root = makeElement({ tag: 'div' });
  const item = makeElement({ tag: 'div', className: 'ListItem' });
  item.appendChild(makeElement({ tag: 'div', className: 'peer-title', textContent: 'Gruppo Dev' }));
  const sub = makeElement({ tag: 'div', className: 'dialog-subtitle', textContent: 'Build completata 4' });
  sub.appendChild(makeElement({ tag: 'span', textContent: 'Build completata' }));
  sub.appendChild(makeElement({ tag: 'span', className: 'Badge unread', textContent: '4' }));
  item.appendChild(sub);
  root.appendChild(item);

  const { messages } = runScenario('web.telegram.org', '(4) Telegram', root);
  const unread = messages.find(m => m.type === 'UNREAD_COUNT_CHANGED');
  assert(unread, 'Telegram UNREAD_COUNT_CHANGED emitted');
  assert.strictEqual(unread.unreadCount, 4);
  assert.strictEqual(unread.latestChatTitle, 'Gruppo Dev');
  assert.strictEqual(unread.latestMessageText, 'Build completata');
}

// 3. Discord unread mention + message preview extraction
{
  const root = makeElement({ tag: 'div' });
  const link = makeElement({ tag: 'a', attrs: { 'data-list-item-id': 'channels___123', 'aria-label': '#announcements, 2 mentions' } });
  link.appendChild(makeElement({ tag: 'div', className: 'numberBadge__40d6f', textContent: '2' }));
  root.appendChild(link);
  const msgLi = makeElement({ tag: 'li', id: 'chat-messages-999' });
  msgLi.appendChild(makeElement({ tag: 'div', id: 'message-content-999', textContent: 'Nuova release v1.5.14 disponibile!' }));
  root.appendChild(msgLi);

  const { messages, sandbox } = runScenario('discord.com', '(2) Discord', root);
  const unread = messages.find(m => m.type === 'UNREAD_COUNT_CHANGED');
  assert(unread, 'Discord UNREAD_COUNT_CHANGED emitted');
  assert.strictEqual(unread.unreadCount, 2);
  assert.strictEqual(unread.latestChatTitle, '#announcements');
  assert.strictEqual(unread.latestMessageText, 'Nuova release v1.5.14 disponibile!');

  // Also test CustomNotification fallback when body is empty
  new sandbox.Notification('Discord Alert', { body: '' });
  const notif = messages.find(m => m.type === 'NOTIFICATION_RECEIVED');
  assert(notif, 'CustomNotification emitted NOTIFICATION_RECEIVED');
  assert.strictEqual(notif.title, 'Discord Alert');
  assert.strictEqual(notif.body, 'Nuova release v1.5.14 disponibile!');
}

console.log('PASS: notification.test.cjs (WhatsApp, Telegram, Discord, and CustomNotification fallback)');
