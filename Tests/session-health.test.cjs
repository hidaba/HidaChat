'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const raw = fs.readFileSync(path.join(__dirname, '../Scripts/session-health.js'), 'utf8');
const script = raw.replaceAll('$$BRIDGE_TOKEN$$', JSON.stringify('test-bridge'));
const rawNotif = fs.readFileSync(path.join(__dirname, '../Scripts/notification.js'), 'utf8');
const notifScript = rawNotif.replaceAll('$$BRIDGE_TOKEN$$', JSON.stringify('test-bridge'));
const flush = () => new Promise(resolve => setImmediate(resolve));
function harness(options = {}) {
  const messages = [], events = {}, intervals = new Map();
  const state = { qr: false, chats: true };
  const timeouts = new Map();
  let id = 0;
  class MockMutationObserver {
    observe() {}
    disconnect() {}
  }
  const window = {
    chrome: { webview: { postMessage: value => messages.push(value) } },
    setInterval: (callback, ms) => { intervals.set(++id, callback); assert.equal(ms, 60000); return id; },
    clearInterval: timer => intervals.delete(timer),
    setTimeout: (callback, ms) => { const tid = ++id; timeouts.set(tid, callback); return tid; },
    clearTimeout: timer => timeouts.delete(timer),
    addEventListener: (name, callback) => { (events[name] ||= []).push(callback); },
    removeEventListener: () => {},
    WebSocket: function NativeWebSocket() {},
    indexedDB: { deleteDatabase: function nativeDeleteDatabase() {} },
    MutationObserver: MockMutationObserver
  };
  window.self = window;
  window.top = options.frame ? {} : window;
  const document = {
    visibilityState: 'visible',
    addEventListener: window.addEventListener,
    removeEventListener: () => {},
    querySelector: selector => selector.includes('#pane-side') ? (state.chats ? {} : null) : (state.qr ? {} : null),
    querySelectorAll: () => []
  };
  const navigator = { onLine: true, storage: options.storage ?? {
    persist: async () => true, persisted: async () => true,
    estimate: async () => ({ usage: 2345, quota: 1234567 })
  } };
  const location = new URL(options.origin || 'https://web.whatsapp.com');
  const mockSetTimeout = (callback, ms) => { const tid = ++id; timeouts.set(tid, callback); return tid; };
  const mockClearTimeout = timer => timeouts.delete(timer);
  const context = vm.createContext({ window, document, navigator, location, console, MutationObserver: MockMutationObserver, setTimeout: mockSetTimeout, clearTimeout: mockClearTimeout });
  const socket = window.WebSocket, deleteDb = window.indexedDB.deleteDatabase;
  const run = () => vm.runInContext(script, context);
  run();
  return { messages, events, intervals, state, window, socket, deleteDb, run, context,
    tick: async () => { for (const callback of intervals.values()) await callback(); await flush(); },
    emit: async name => { for (const callback of events[name] || []) callback({}); await flush(); }
  };
}
test('storage permission and estimates are reported, not assumed', async () => {
  const h = harness(); await flush();
  assert.equal(h.messages.find(m => m.type === 'STORAGE_PERSIST').details.persisted, true);
  const health = h.messages.find(m => m.type === 'STORAGE_HEALTH');
  assert.equal(health.details.usageBytes, 2345);
  assert.equal(health.details.quotaBytes, 1234567);
  assert.equal(health.bridgeToken, 'test-bridge');
});
test('denied persistence remains visible as false', async () => {
  const h = harness({ storage: { persist: async () => false, persisted: async () => false } });
  await flush();
  assert.equal(h.messages.find(m => m.type === 'STORAGE_HEALTH').details.persisted, false);
});
test('untrusted origins and subframes are ignored', async () => {
  for (const options of [{ origin: 'https://evil.example' }, { origin: 'http://web.whatsapp.com' }, { frame: true }]) {
    const h = harness(options); await flush();
    assert.equal(h.messages.length, 0); assert.equal(h.intervals.size, 0);
  }
});
test('double injection does not create duplicate timers', async () => {
  const h = harness(); h.run(); await flush();
  assert.equal(h.intervals.size, 1);
});
test('QR transition logs only state, never DOM content or QR payload', async () => {
  const h = harness(); await flush();
  h.state.chats = false; h.state.qr = true; await h.tick();
  const transitions = h.messages.filter(m => m.type === 'SESSION_UI_STATE');
  assert.equal(transitions.length, 2);
  assert.equal(transitions[1].details.state, 'qr-visible');
  assert.deepEqual(Object.keys(transitions[1].details).sort(), ['online', 'previous', 'state']);
  await h.tick();
  assert.equal(h.messages.filter(m => m.type === 'SESSION_UI_STATE').length, 2);
});
test('native transport and database methods remain untouched across all injected scripts', async () => {
  const h = harness(); await flush();
  assert.equal(h.window.WebSocket, h.socket);
  assert.equal(h.window.indexedDB.deleteDatabase, h.deleteDb);
  vm.runInContext(notifScript, h.context);
  await flush();
  assert.equal(h.window.WebSocket, h.socket);
  assert.equal(h.window.indexedDB.deleteDatabase, h.deleteDb);
});
test('storage errors are caught and expose only the error category', async () => {
  const error = Object.assign(new Error('SECRET MUST NOT APPEAR'), { name: 'SecurityError' });
  const h = harness({ storage: { persist: async () => { throw error; }, persisted: async () => { throw error; } } });
  await flush();
  assert.ok(h.messages.some(m => m.type === 'STORAGE_HEALTH_ERROR'));
  assert.ok(!JSON.stringify(h.messages).includes('SECRET'));
});
test('leaving the page cancels sampling and ignores pending callbacks', async () => {
  const h = harness(); await flush(); await h.emit('pagehide');
  const count = h.messages.length;
  await h.emit('online');
  assert.equal(h.intervals.size, 0); assert.equal(h.messages.length, count);
});
test('unknown UI is not labeled as logout', async () => {
  const h = harness(); await flush(); h.state.chats = false; await h.tick();
  assert.equal(h.messages.filter(m => m.type === 'SESSION_UI_STATE').at(-1).details.state, 'unknown');
});
