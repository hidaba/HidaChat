(function() {
  if (window.top !== window.self) return;
  const __bridgeToken = $$BRIDGE_TOKEN$$;
  if (window.__notificationOverrideInstalled) return;
  window.__notificationOverrideInstalled = true;

  const activeCustomNotifications = {};

  try {
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

  // Hook per Badging API nativa (navigator.setAppBadge / clearAppBadge usata da Telegram PWA e web moderni)
  let appBadgeCount = 0;
  try {
    if (typeof navigator !== 'undefined') {
      const origSetAppBadge = typeof navigator.setAppBadge === 'function' ? navigator.setAppBadge.bind(navigator) : null;
      navigator.setAppBadge = function(count) {
        try {
          if (count === undefined || count === null) {
            appBadgeCount = 1;
          } else {
            const num = typeof count === 'number' ? count : (parseInt(count, 10) || 0);
            appBadgeCount = Math.max(0, num);
          }
          scheduleAllChecks();
        } catch(e) {}
        if (origSetAppBadge) return origSetAppBadge(count);
        return Promise.resolve();
      };

      const origClearAppBadge = typeof navigator.clearAppBadge === 'function' ? navigator.clearAppBadge.bind(navigator) : null;
      navigator.clearAppBadge = function() {
        try {
          appBadgeCount = 0;
          scheduleAllChecks();
        } catch(e) {}
        if (origClearAppBadge) return origClearAppBadge();
        return Promise.resolve();
      };
    }
  } catch(e) {}

  // Monitoraggio unread count combinato (Title Observer + Title Setter Interceptor + DOM Badge Scanner + Badging API)
  let lastReportedUnreadCount = -1;
  let lastHeartbeatTime = 0;
  let updateDebounceTimer = null;

  function scanTelegramUnreadCount() {
    let count = 0;
    const debugItems = [];
    try {
      // 1. Badge chat Telegram Web (Web A, Web K, Web Z)
      const tgChatSelectors = [
        '.ChatBadge.unread',
        '.ChatBadge .Badge',
        '.Badge.unread',
        '.dialog-subtitle-badge',
        '.rp .badge',
        '.rp .unread',
        '.chatlist-chat .badge',
        '.chat-badge',
        '.unread-count',
        '[class*="unread-count"]',
        '[class*="unread_count"]',
        '.ListItem .Badge'
      ];
      const tgElements = document.querySelectorAll(tgChatSelectors.join(', '));
      if (tgElements && tgElements.length > 0) {
        const seen = new Set();
        tgElements.forEach(el => {
          // Ignora icone di fissaggio in alto (pin 📌) o elementi decorativi
          if (el.querySelector('.icon-pin, [class*="pin"], .icon-reaction') || el.classList.contains('icon-pin')) return;

          const container = el.closest('.ListItem, .rp, .chatlist-chat, [data-peer-id], li') || el;
          if (seen.has(container)) return;
          seen.add(container);

          const rawText = (el.textContent || '').trim();
          const digits = rawText.replace(/[^\d]/g, '');
          const isExplicitUnread = el.classList.contains('unread') || el.classList.contains('Badge') || el.classList.contains('dialog-subtitle-badge') || (el.getAttribute('class') || '').includes('unread');

          // Nome chat per il report di debug
          let chatTitle = '';
          try {
            const titleEl = container.querySelector('.peer-title, .title, .dialog-title, .user-title, h3, .name, [class*="title"]');
            if (titleEl) chatTitle = titleEl.textContent.trim();
          } catch(e) {}

          if (digits) {
            const n = parseInt(digits, 10);
            if (!isNaN(n) && n > 0) {
              count += n;
              debugItems.push({
                chatTitle: chatTitle || 'Chat',
                count: n,
                rawText: rawText,
                classes: el.className,
                html: el.outerHTML.substring(0, 150)
              });
            }
          } else if (isExplicitUnread && (el.classList.contains('unread') || rawText === '•' || rawText === '*')) {
            // Solo se è un contrassegno unread esplicito senza numero (es. pallino non letto)
            count += 1;
            debugItems.push({
              chatTitle: chatTitle || 'Chat',
              count: 1,
              rawText: rawText,
              classes: el.className,
              html: el.outerHTML.substring(0, 150)
            });
          }
        });
      }

      // 2. Se non ci sono badge nelle chat visibili (chat list virtualizzata o chiusa), fallback sui tab cartella (solo se contengono numeri > 0)
      if (count === 0) {
        const tgFolderSelectors = [
          '.folders-tabs .Tab .Badge',
          '.folders-tabs .Badge',
          '.tabs-tab .badge',
          '.tabs-tab .dialog-subtitle-badge',
          '.sidebar-header .Badge',
          '.sidebar-header .badge'
        ];
        const tgFolders = document.querySelectorAll(tgFolderSelectors.join(', '));
        if (tgFolders && tgFolders.length > 0) {
          const firstFolder = tgFolders[0];
          const rawText = (firstFolder.textContent || '').trim();
          const digits = rawText.replace(/[^\d]/g, '');
          if (digits) {
            const n = parseInt(digits, 10);
            if (!isNaN(n) && n > 0) {
              count = n;
              debugItems.push({
                chatTitle: 'Tab Cartella Principale',
                count: n,
                rawText: rawText,
                classes: firstFolder.className,
                html: firstFolder.outerHTML.substring(0, 150)
              });
            }
          }
        }
      }

      // 3. Controllo favicon per indicatore non letti di Telegram (solo se presente icona specifica unread)
      if (count === 0) {
        const iconEl = document.querySelector('link[rel*="icon"]');
        if (iconEl && iconEl.href) {
          const href = iconEl.href.toLowerCase();
          if (href.includes('unread') || href.includes('badge')) {
            count = 1;
            debugItems.push({
              chatTitle: 'Favicon Unread Indicator',
              count: 1,
              rawText: iconEl.href,
              classes: 'link-icon',
              html: iconEl.outerHTML.substring(0, 150)
            });
          }
        }
      }
    } catch(e) {}
    return { count: count, items: debugItems };
  }

  function scanWhatsAppUnreadCount() {
    let count = 0;
    try {
      const waSelectors = [
        '[data-testid="unread-count"]', '[data-testid="icon-unread-count"]',
        'span[data-icon="unread-count"]', 'span[aria-label*="unread" i]',
        'span[aria-label*="non lett" i]', 'span[aria-label*="ungelesen" i]',
        'span[aria-label*="no leí" i]', 'span[aria-label*="non lu" i]',
        'span[aria-label*="não lida" i]', 'div[aria-label*="unread" i]',
        'div[aria-label*="non lett" i]'
      ];
      const waElements = document.querySelectorAll(waSelectors.join(', '));
      if (waElements && waElements.length > 0) {
        const seenWa = new Set();
        waElements.forEach(el => {
          if (seenWa.has(el)) return;
          seenWa.add(el);
          const txt = (el.textContent || '').trim().replace(/[^\d]/g, '');
          const aria = (el.getAttribute('aria-label') || '').trim();
          let num = 0;
          if (txt) {
            num = parseInt(txt, 10);
          } else if (aria) {
            const m = aria.match(/(\d+)\s*(?:non lett|unread|messagg)/i) || aria.match(/(\d+)/);
            if (m) num = parseInt(m[1], 10);
          }
          if (!isNaN(num) && num > 0) {
            count += num;
          } else {
            count += 1;
          }
        });
      }
    } catch(e) {}
    return count;
  }

  function checkAndNotifyUnreadCount() {
    const isTelegram = (location.hostname || '').includes('telegram');
    const title = document.title || '';
    const titleMatch = title.match(/[\(\[](\d+)\+?[\)\]]/);
    const hasBullet = /[\(•\*\)]/.test(title) && (title.includes('•') || title.includes('*') || /\(\s*\)/.test(title));
    const titleCount = titleMatch ? parseInt(titleMatch[1], 10) : (hasBullet ? 1 : 0);

    let domCount = 0;
    let debugItems = [];
    if (isTelegram) {
      const tgRes = scanTelegramUnreadCount();
      domCount = tgRes.count;
      debugItems = tgRes.items;
    } else {
      domCount = scanWhatsAppUnreadCount();
    }

    // Preferisci il massimo tra il conteggio estratto dal titolo, i badge del DOM e la Badging API
    const effectiveCount = Math.max(titleCount, domCount, appBadgeCount);
    const now = Date.now();

    if (effectiveCount !== lastReportedUnreadCount || (effectiveCount > 0 && (now - lastHeartbeatTime > 3000))) {
      lastReportedUnreadCount = effectiveCount;
      lastHeartbeatTime = now;
      try {
        if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
          window.chrome.webview.postMessage({
            channel: 'NotificationChannel',
            type: 'UNREAD_COUNT_CHANGED',
            id: 'unread_' + now,
            unreadCount: effectiveCount,
            title: title,
            domCount: domCount,
            appBadgeCount: appBadgeCount,
            titleCount: titleCount,
            debugItems: debugItems,
            bridgeToken: __bridgeToken
          });
        }
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
        if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
          window.chrome.webview.postMessage({
            channel: 'NotificationChannel',
            type: 'ONLINE_STATUS_CHANGED',
            id: 'online_' + Date.now(),
            isOnline: res.isOnline,
            statusText: res.statusText,
            bridgeToken: __bridgeToken
          });
        }
      } catch(e) {}
    }
  }

  function scheduleAllChecks() {
    if (updateDebounceTimer) clearTimeout(updateDebounceTimer);
    updateDebounceTimer = setTimeout(function() {
      checkAndNotifyUnreadCount();
      checkAndNotifyOnlineStatus();
    }, 250);
  }

  function initMonitoring() {
    // Intercetta la modifica diretta a document.title (WhatsApp Web e Telegram SPA)
    try {
      const titleDesc = Object.getOwnPropertyDescriptor(Document.prototype, 'title') ||
                        Object.getOwnPropertyDescriptor(HTMLDocument.prototype, 'title');
      if (titleDesc && titleDesc.set) {
        const origTitleSet = titleDesc.set;
        Object.defineProperty(document, 'title', {
          get: function() {
            return titleDesc.get ? titleDesc.get.call(this) : '';
          },
          set: function(val) {
            origTitleSet.call(this, val);
            scheduleAllChecks();
          },
          configurable: true
        });
      }
    } catch(e) {}

    const titleEl = document.querySelector('title');
    if (titleEl) {
      const titleObserver = new MutationObserver(scheduleAllChecks);
      titleObserver.observe(titleEl, { subtree: true, characterData: true, childList: true });
    }

    if (document.head) {
      const headObserver = new MutationObserver(scheduleAllChecks);
      headObserver.observe(document.head, { subtree: true, characterData: true, childList: true });
    }

    if (document.body) {
      const bodyObserver = new MutationObserver(scheduleAllChecks);
      bodyObserver.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'aria-label', 'href'] });
    }

    // Polling periodico continuo per notifiche e stato online
    setInterval(function() {
      checkAndNotifyUnreadCount();
      checkAndNotifyOnlineStatus();
    }, 1500);

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
