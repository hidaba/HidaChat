(function() {
  try {
    const __bridgeToken = $$BRIDGE_TOKEN$$;
    if (window.__notificationOverrideInstalled) return;
    window.__notificationOverrideInstalled = true;

    const activeCustomNotifications = {};

    // 1. Intercetta ServiceWorkerRegistration.prototype.showNotification (usato da WhatsApp Web e Telegram PWA)
    if (window.ServiceWorkerRegistration && window.ServiceWorkerRegistration.prototype) {
      try {
        const originalShowNotification = window.ServiceWorkerRegistration.prototype.showNotification;
        window.ServiceWorkerRegistration.prototype.showNotification = function(title, options) {
          const id = Math.random().toString(36).substring(2, 9);
          try {
            if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
              window.chrome.webview.postMessage({
                channel: 'NotificationChannel',
                type: 'NOTIFICATION_RECEIVED',
                id: id,
                title: title,
                body: options ? (options.body || '') : '',
                icon: options ? (options.icon || '') : '',
                bridgeToken: __bridgeToken
              });
            }
          } catch(e) {}
          return Promise.resolve();
        };
      } catch(e) {}
    }

    // 2. Intercetta window.Notification preservando prototype, costruttore nativo e static members
    const OrigNotification = window.Notification;
    if (OrigNotification) {
      function CustomNotification(title, options) {
        var self = this;
        this.title = title;
        this.options = options || {};
        this.id = Math.random().toString(36).substring(2, 9);
        activeCustomNotifications[this.id] = this;
        this._listeners = {};

        try {
          if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
            window.chrome.webview.postMessage({
              channel: 'NotificationChannel',
              type: 'NOTIFICATION_RECEIVED',
              id: this.id,
              title: this.title,
              body: this.options.body || '',
              icon: this.options.icon || '',
              bridgeToken: __bridgeToken
            });
          }
        } catch(e) {}

        this.close = function() {
          if (activeCustomNotifications[self.id]) {
            delete activeCustomNotifications[self.id];
            try {
              if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
                window.chrome.webview.postMessage({
                  channel: 'NotificationChannel',
                  type: 'NOTIFICATION_CLOSED',
                  id: self.id,
                  bridgeToken: __bridgeToken
                });
              }
            } catch(e) {}
          }
        };

        this.addEventListener = function(event, callback) {
          if (!self._listeners[event]) {
            self._listeners[event] = [];
          }
          self._listeners[event].push(callback);
        };

        this.removeEventListener = function(event, callback) {
          if (self._listeners[event]) {
            const idx = self._listeners[event].indexOf(callback);
            if (idx !== -1) {
              self._listeners[event].splice(idx, 1);
            }
          }
        };
      }

      // Preserva la catena prototipale per instanceof ed ereditarietà
      try {
        CustomNotification.prototype = Object.create(OrigNotification.prototype);
        CustomNotification.prototype.constructor = CustomNotification;
        Object.setPrototypeOf(CustomNotification, OrigNotification);
      } catch(e) {}

      CustomNotification.permission = 'granted';
      CustomNotification.requestPermission = function(callback) {
        if (typeof callback === 'function') callback('granted');
        return Promise.resolve('granted');
      };

      try {
        Object.defineProperty(CustomNotification, 'permission', {
          get: function() { return 'granted'; },
          set: function() {}
        });
      } catch(e) {}

      window.Notification = CustomNotification;
    }

    // 3. Gestione permessi nativi con fallback sicuro
    if (navigator.permissions && typeof navigator.permissions.query === 'function') {
      try {
        const origQuery = navigator.permissions.query;
        navigator.permissions.query = function(parameters) {
          try {
            if (parameters && parameters.name === 'notifications') {
              return Promise.resolve({
                state: 'granted',
                name: 'notifications',
                onchange: null,
                addEventListener: function() {},
                removeEventListener: function() {},
                dispatchEvent: function() { return false; }
              });
            }
            return origQuery.apply(navigator.permissions, arguments);
          } catch(err) {
            return Promise.resolve({ state: 'granted', name: parameters ? parameters.name : 'unknown' });
          }
        };
      } catch(e) {}
    }
  } catch(topEx) {
    console.warn('[NotificationOverride] init error:', topEx);
  }

  // Monitoraggio unread count combinato (Title Observer + DOM Badge Scanner)
  let lastReportedUnreadCount = -1;
  let updateDebounceTimer = null;

  function scanDomUnreadCount() {
    let count = 0;
    try {
      // 1. Selettori Telegram Web K / Z / A
      const tgBadges = document.querySelectorAll('.badge.unread, .unread-count, .chatlist-chat .badge, .dialog-subtitle .badge, .chat-badge, .sidebar-header .badge');
      if (tgBadges && tgBadges.length > 0) {
        tgBadges.forEach(el => {
          const txt = (el.textContent || '').trim().replace(/[^\d]/g, '');
          if (txt) {
            const num = parseInt(txt, 10);
            if (!isNaN(num) && num > 0) count += num;
          } else {
            count += 1; // Pallino di notifica senza numero esplicito
          }
        });
      }

      // 2. Selettori WhatsApp Web
      const waBadges = document.querySelectorAll('[data-testid="unread-count"], span[aria-label*="unread"], span[aria-label*="non letto"], span[aria-label*="non letti"]');
      if (waBadges && waBadges.length > 0) {
        waBadges.forEach(el => {
          const txt = (el.textContent || '').trim().replace(/[^\d]/g, '');
          if (txt) {
            const num = parseInt(txt, 10);
            if (!isNaN(num) && num > 0) count += num;
          }
        });
      }
    } catch(e) {}
    return count;
  }

  function checkAndNotifyUnreadCount() {
    const title = document.title || '';
    const titleMatch = title.match(/^\((\d+)\)/);
    const titleCount = titleMatch ? parseInt(titleMatch[1], 10) : 0;
    const domCount = scanDomUnreadCount();

    // Preferisci il massimo tra il titolo e i badge DOM
    const effectiveCount = Math.max(titleCount, domCount);

    if (effectiveCount !== lastReportedUnreadCount) {
      lastReportedUnreadCount = effectiveCount;
      try {
        window.chrome.webview.postMessage({
          channel: 'NotificationChannel',
          type: 'UNREAD_COUNT_CHANGED',
          unreadCount: effectiveCount,
          title: title,
          bridgeToken: __bridgeToken
        });
      } catch(e) {}
    }
  }



  // Monitoraggio stato "Online" / "In linea" / "Sta scrivendo..." del contatto attivo (TODO #42)
  let lastReportedOnline = null;
  let lastReportedStatusText = '';

  function scanOnlineStatus() {
    try {
      // 1. WhatsApp Web: #main header o [data-testid="conversation-header"]
      const waHeader = document.querySelector('#main header, [data-testid="conversation-header"]');
      if (waHeader) {
        const subSpans = waHeader.querySelectorAll('span[title], span[dir="auto"], span');
        for (let i = 0; i < subSpans.length; i++) {
          const span = subSpans[i];
          const text = (span.textContent || '').trim().toLowerCase();
          const title = (span.getAttribute('title') || '').trim().toLowerCase();
          
          if (text === 'online' || text === 'in linea' || text === 'en línea' || text === 'en ligne' || text === 'conectado' ||
              title === 'online' || title === 'in linea' || title === 'en línea' || title === 'en ligne' || title === 'conectado') {
            return { isOnline: true, statusText: span.textContent.trim() || 'in linea' };
          }
          if (text.includes('sta scrivendo') || text.includes('typing') || text.includes('escribiendo') || text.includes('scrive...') || text.includes('registra audio') || text.includes('recording audio')) {
            return { isOnline: true, statusText: span.textContent.trim() || 'sta scrivendo...' };
          }
        }
      }

      // 2. Telegram Web K / Z / A
      const tgStatus = document.querySelector('.chat-info .person-status, .chat-info .status, .topbar .status, .chat-subtitle, .chat-info-status');
      if (tgStatus) {
        const text = (tgStatus.textContent || '').trim().toLowerCase();
        if (text === 'online' || text === 'in linea' || text === 'en ligne' || text === 'en línea' || tgStatus.classList.contains('online')) {
          return { isOnline: true, statusText: tgStatus.textContent.trim() || 'online' };
        }
        if (text.includes('typing') || text.includes('sta scrivendo') || text.includes('scrive') || tgStatus.classList.contains('typing')) {
          return { isOnline: true, statusText: tgStatus.textContent.trim() || 'typing...' };
        }
      }
    } catch(e) {}
    return { isOnline: false, statusText: '' };
  }

  function checkAndNotifyOnlineStatus() {
    const res = scanOnlineStatus();
    if (res.isOnline !== lastReportedOnline || (res.isOnline && res.statusText !== lastReportedStatusText)) {
      lastReportedOnline = res.isOnline;
      lastReportedStatusText = res.statusText;
      try {
        window.chrome.webview.postMessage({
          channel: 'NotificationChannel',
          type: 'ONLINE_STATUS_CHANGED',
          isOnline: res.isOnline,
          statusText: res.statusText,
          bridgeToken: __bridgeToken
        });
      } catch(e) {}
    }
  }

  function scheduleAllChecks() {
    if (updateDebounceTimer) clearTimeout(updateDebounceTimer);
    updateDebounceTimer = setTimeout(function() {
      checkAndNotifyUnreadCount();
      checkAndNotifyOnlineStatus();
    }, 400);
  }

  function initMonitoring() {
    const titleEl = document.querySelector('title');
    if (titleEl) {
      const titleObserver = new MutationObserver(scheduleAllChecks);
      titleObserver.observe(titleEl, { subtree: true, characterData: true, childList: true });
    }

    if (document.body) {
      const bodyObserver = new MutationObserver(scheduleAllChecks);
      bodyObserver.observe(document.body, { childList: true, subtree: true, attributes: false });
    }

    // Polling periodico per notifiche e stato online
    setInterval(function() {
      checkAndNotifyUnreadCount();
      checkAndNotifyOnlineStatus();
    }, 2000);

    checkAndNotifyUnreadCount();
    checkAndNotifyOnlineStatus();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initMonitoring);
  } else {
    initMonitoring();
  }

  window.onNotificationClicked = function(id) {
    const notification = activeCustomNotifications[id];
    if (notification) {
      if (typeof notification.onclick === 'function') {
        notification.onclick();
      }
      const listeners = notification._listeners['click'] || [];
      listeners.forEach(cb => {
        try { cb(); } catch(e) {}
      });
    }
  };

  window.onNotificationClosedFromServer = function(id) {
    const notification = activeCustomNotifications[id];
    if (notification) {
      if (typeof notification.onclose === 'function') {
        notification.onclose();
      }
      const listeners = notification._listeners['close'] || [];
      listeners.forEach(cb => {
        try { cb(); } catch(e) {}
      });
      delete activeCustomNotifications[id];
    }
  };
})();
