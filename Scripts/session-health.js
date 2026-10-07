(function () {
  'use strict';
  if (window.top !== window.self) return;
  if (location.origin !== 'https://web.whatsapp.com' && location.origin !== 'https://web.telegram.org') return;
  if (window.__hidaSessionHealthInstalled) return;
  window.__hidaSessionHealthInstalled = true;
  const bridgeToken = $$BRIDGE_TOKEN$$;
  let stopped = false;
  let sampling = false;
  let previousState = null;

  function emit(type, details) {
    if (stopped) return;
    try {
      window.chrome.webview.postMessage({ channel: 'DiagnosticChannel', type, details, bridgeToken });
    } catch (_) {}
  }

  function uiState() {
    if (location.hostname !== 'web.whatsapp.com') return 'not-whatsapp';
    // Presence-only hints; never read QR data, phone numbers, HTML, or chat content.
    if (document.querySelector('#pane-side, [data-testid="chat-list"]')) return 'chat-list-visible';
    if (document.querySelector('[data-testid="qrcode"], [data-testid="link-device-qrcode"], div[data-ref] canvas')) return 'qr-visible';
    return 'unknown'; // DOM selectors can change; not a declaration of logout.
  }

  async function sample() {
    if (stopped || sampling) return;
    sampling = true;
    try {
      const state = uiState();
      if (state !== previousState) {
        emit('SESSION_UI_STATE', { previous: previousState, state, online: navigator.onLine });
        previousState = state;
      }
      const storage = navigator.storage;
      if (!storage) {
        emit('STORAGE_HEALTH', { supported: false });
        return;
      }
      const persisted = typeof storage.persisted === 'function' ? await storage.persisted() : null;
      const estimate = typeof storage.estimate === 'function' ? await storage.estimate() : {};
      emit('STORAGE_HEALTH', {
        persisted,
        usageBytes: Number.isFinite(estimate.usage) ? estimate.usage : null,
        quotaBytes: Number.isFinite(estimate.quota) ? estimate.quota : null,
        online: navigator.onLine,
        visibility: document.visibilityState
      });
    } catch (error) {
      emit('STORAGE_HEALTH_ERROR', { name: error && error.name ? error.name : 'Error' });
    } finally {
      sampling = false;
    }
  }

  // Permission is scoped to trusted origins by the host. Log the actual outcome;
  // persistent storage protects against eviction, NOT revocation by WhatsApp.
  try {
    if (navigator.storage && typeof navigator.storage.persist === 'function') {
      navigator.storage.persist().then(function (persisted) {
        emit('STORAGE_PERSIST', { persisted });
        sample();
      }).catch(function (error) {
        emit('STORAGE_PERSIST_ERROR', { name: error && error.name ? error.name : 'Error' });
        sample();
      });
    } else {
      sample();
    }
  } catch (_) { sample(); }

  const timer = window.setInterval(sample, 60000);
  window.addEventListener('online', sample);
  window.addEventListener('offline', sample);
  window.addEventListener('pageshow', sample);
  document.addEventListener('DOMContentLoaded', sample);
  window.addEventListener('pagehide', function () {
    stopped = true;
    window.clearInterval(timer);
  }, { once: true });
})();
